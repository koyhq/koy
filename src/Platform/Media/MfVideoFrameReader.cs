using System.Runtime.InteropServices;
using Koy.Core.Media;
using Windows.Win32;
using Windows.Win32.Media.MediaFoundation;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;

namespace Koy.Platform.Media;

/// <summary>
/// Media Foundation SourceReader ile kare kare video çözer, <b>yalnız yazılımla</b>. DXVA açıkça kapatılır ve okuyucuya
/// Direct3D aygıt yöneticisi verilmez. WPF MediaElement ise (WMP motoru + EVR) videoyu zaman zaman donanımla çözüyor ve
/// bu sırada tam Direct3D yığınını (d3d9on12, d3d12, NVIDIA / Intel sürücüleri; ~150 MB) sürece kalıcı olarak yüklüyordu
/// (izleme 2026-11-20, arastirma/ui-hareket-2026-11/08). Çıktı RGB32'dir, renk dönüşümünü Media Foundation'ın video
/// işlemcisi yapar.
/// </summary>
public sealed class MfVideoFrameReaderFactory : IVideoFrameReaderFactory
{
    public IVideoFrameReader? Open(string path) => MfVideoFrameReader.TryOpen(path);
}

internal sealed unsafe class MfVideoFrameReader : IVideoFrameReader
{
    private const uint MfVersion = 0x00020070; // MF_VERSION (MF_SDK_VERSION 2, MF_API_VERSION 0x70)
    private const uint MfStartupLite = 1;      // MFSTARTUP_LITE: soket / ağ kaynakları kurulmaz
    private const uint AllStreams = 0xFFFFFFFE;  // MF_SOURCE_READER_ALL_STREAMS
    private const uint FirstVideoStream = 0xFFFFFFFC; // MF_SOURCE_READER_FIRST_VIDEO_STREAM
    private const uint EndOfStreamFlag = 0x2;  // MF_SOURCE_READERF_ENDOFSTREAM

    private IMFSourceReader? _reader;
    private bool _started;

    public int Width { get; }
    public int Height { get; }

    private readonly bool _bottomUp;

    private MfVideoFrameReader(IMFSourceReader reader, int width, int height, bool bottomUp)
    {
        _reader = reader;
        _started = true;
        _bottomUp = bottomUp;
        Width = width;
        Height = height;
    }

    public static MfVideoFrameReader? TryOpen(string path)
    {
        if (PInvoke.MFStartup(MfVersion, MfStartupLite).Failed)
            return null;

        IMFSourceReader? reader = null;
        try
        {
            PInvoke.MFCreateAttributes(out var attributes, 2).ThrowOnFailure();
            var enableProcessing = PInvoke.MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING;
            var disableDxva = PInvoke.MF_SOURCE_READER_DISABLE_DXVA;
            attributes.SetUINT32(&enableProcessing, 1);
            attributes.SetUINT32(&disableDxva, 1);

            PInvoke.MFCreateSourceReaderFromURL(path, attributes, out reader).ThrowOnFailure();
            reader.SetStreamSelection(AllStreams, false);
            reader.SetStreamSelection(FirstVideoStream, true);

            PInvoke.MFCreateMediaType(out var type).ThrowOnFailure();
            var majorKey = PInvoke.MF_MT_MAJOR_TYPE;
            var major = PInvoke.MFMediaType_Video;
            var subtypeKey = PInvoke.MF_MT_SUBTYPE;
            var subtype = PInvoke.MFVideoFormat_RGB32;
            type.SetGUID(&majorKey, &major);
            type.SetGUID(&subtypeKey, &subtype);
            reader.SetCurrentMediaType(FirstVideoStream, null, type);

            reader.GetCurrentMediaType(FirstVideoStream, out var current);
            var sizeKey = PInvoke.MF_MT_FRAME_SIZE;
            current.GetUINT64(&sizeKey, out var size);
            var width = (int)(size >> 32);
            var height = (int)(size & 0xFFFFFFFF);
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
                throw new InvalidDataException("Geçersiz video boyutu.");

            // Varsayılan adım negatifse kareler alttan üste gelir (RGB'nin geleneksel düzeni).
            var strideKey = PInvoke.MF_MT_DEFAULT_STRIDE;
            var bottomUp = false;
            try
            {
                current.GetUINT32(&strideKey, out var stride);
                bottomUp = (int)stride < 0;
            }
            catch (COMException)
            {
            }

            return new MfVideoFrameReader(reader, width, height, bottomUp);
        }
        catch (Exception ex) when (ex is COMException or InvalidDataException or InvalidCastException or ArgumentException)
        {
            if (reader is not null)
                Marshal.ReleaseComObject(reader);
            PInvoke.MFShutdown();
            return null;
        }
    }

    public bool ReadNextFrame(Span<byte> bgra, out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;
        var reader = _reader ?? throw new ObjectDisposedException(nameof(MfVideoFrameReader));
        if (bgra.Length < Width * Height * 4)
            throw new ArgumentException("Tampon küçük.", nameof(bgra));

        // Boş örnek (akış boşluğu) ya da dosya sonu gelirse birkaç kez daha denenir; sonsuz döngüye girmez.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            uint flags = 0;
            long stamp = 0;
            IMFSample_unmanaged* sample = null;
            try
            {
                reader.ReadSample(FirstVideoStream, 0, null, &flags, &stamp, &sample);
            }
            catch (COMException)
            {
                return false;
            }

            if ((flags & EndOfStreamFlag) != 0)
            {
                if (sample is not null)
                    sample->Release();
                Rewind(reader);
                continue;
            }

            if (sample is null)
                continue;

            try
            {
                CopySample(sample, bgra);
                timestamp = TimeSpan.FromTicks(stamp); // 100 ns birim
                return true;
            }
            catch (COMException)
            {
                return false;
            }
            finally
            {
                sample->Release();
            }
        }
        return false;
    }

    private static void Rewind(IMFSourceReader reader)
    {
        var position = default(PROPVARIANT);
        position.vt = VARENUM.VT_I8;
        position.hVal = 0;
        var timeFormat = Guid.Empty; // GUID_NULL: 100 ns
        reader.SetCurrentPosition(&timeFormat, position);
    }

    private void CopySample(IMFSample_unmanaged* sample, Span<byte> bgra)
    {
        IMFMediaBuffer_unmanaged* buffer;
        sample->ConvertToContiguousBuffer(out buffer);
        try
        {
            var rowBytes = Width * 4;
            byte* scan0;
            int pitch;

            uint length;
            buffer->Lock(&scan0, null, &length);
            try
            {
                // Bitişik tamponda satır adımı = uzunluk / yükseklik; RGB32 alttan üste (negatif adım) ise son satır başa gelir.
                pitch = (int)(length / (uint)Height);
                if (_bottomUp)
                {
                    scan0 += (long)(Height - 1) * pitch;
                    pitch = -pitch;
                }
                CopyRows(scan0, pitch, rowBytes, bgra);
            }
            finally
            {
                buffer->Unlock();
            }
        }
        finally
        {
            buffer->Release();
        }
    }

    // RGB32'nin dördüncü baytı tanımsız (genelde 0); BGRA'da opak olsun diye 255 yazılır.
    private void CopyRows(byte* scan0, int pitch, int rowBytes, Span<byte> bgra)
    {
        for (var y = 0; y < Height; y++)
        {
            var source = new ReadOnlySpan<byte>(scan0 + (long)y * pitch, rowBytes);
            var target = bgra.Slice(y * rowBytes, rowBytes);
            source.CopyTo(target);
            for (var x = 3; x < rowBytes; x += 4)
                target[x] = 255;
        }
    }

    public void Dispose()
    {
        if (_reader is { } reader)
        {
            _reader = null;
            Marshal.ReleaseComObject(reader);
        }
        if (_started)
        {
            _started = false;
            PInvoke.MFShutdown();
        }
    }
}
