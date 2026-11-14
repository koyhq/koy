using System.Runtime.InteropServices;
using Koy.Core.Audio;
using Windows.Win32;
using Windows.Win32.Media.Audio;
using Windows.Win32.System.Com;

namespace Koy.Platform.Audio;

/// <summary>
/// Varsayılan çıkış aygıtında çalan sesi (WASAPI loopback) okuyup müzik kartının çubuklarını hesaplar.
/// Yalnız <see cref="Start"/> ile <see cref="Stop"/> arasında çalışır (çentik açık, Ana görünür, müzik çalıyor);
/// o sırada ayrı bir iş parçacığı saniyede ~30 kez paketleri boşaltır. Ses inip çubuklar sıfırlanınca olay kesilir,
/// arayüz boşuna çizilmez. Ses kaydedilmez, diske ya da ağa hiçbir şey gitmez.
/// </summary>
public sealed class SpectrumCaptureService : IDisposable
{
    public const int BandCount = 24;

    private static readonly Guid ClsidMMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);
    private const long BufferDuration100ns = 2_000_000; // 200 ms paylaşımlı arabellek
    private const uint LoopbackFlag = 0x00020000;        // AUDCLNT_STREAMFLAGS_LOOPBACK
    private const uint SilentFlag = 0x2;                 // AUDCLNT_BUFFERFLAGS_SILENT
    private const ushort FormatPcm = 1;
    private const ushort FormatFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;
    private static readonly Guid SubtypeFloat = new("00000003-0000-0010-8000-00aa00389b71");

    private readonly object _gate = new();
    private Thread? _thread;
    private volatile bool _stopRequested;
    private bool _disposed;

    /// <summary>Yeni çubuk düzeyleri (0..1, <see cref="BandCount"/> tane). Yakalama iş parçacığından çağrılır; dizi yeniden kullanılır, kopyalanmalı.</summary>
    public event Action<float[]>? LevelsUpdated;

    public bool IsRunning
    {
        get { lock (_gate) return _thread is not null; }
    }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thread is not null)
                return;

            _stopRequested = false;
            _thread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = "Koy müzik çubukları",
                Priority = ThreadPriority.BelowNormal
            };
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }
    }

    public void Stop()
    {
        Thread? thread;
        lock (_gate)
        {
            thread = _thread;
            _thread = null;
            _stopRequested = true;
        }

        // Döngü en geç bir kare (33 ms) içinde çıkar; arayüzü uzun bekletmemek için sınırlı beklenir.
        if (thread is not null && thread != Thread.CurrentThread)
            thread.Join(TimeSpan.FromMilliseconds(500));
    }

    private void CaptureLoop()
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioClient? client = null;
        IAudioCaptureClient? capture = null;
        var levels = new float[BandCount];
        try
        {
            if (!TryOpen(out enumerator, out device, out client, out capture, out var format) || client is null || capture is null)
                return;

            var analyzer = new SpectrumAnalyzer(BandCount);
            var mono = new float[4096];
            client.Start();
            var wasSilent = true;
            var lastFrame = DateTime.UtcNow;

            while (!_stopRequested)
            {
                Thread.Sleep(FrameInterval);
                var received = Drain(capture, format, analyzer, ref mono);

                // Hiç ses çalınmıyorsa ses motoru loopback paketi göndermez: geçen süre kadar sessizlik eklenir.
                var now = DateTime.UtcNow;
                if (received == 0)
                    analyzer.AddSilence((int)Math.Min(SpectrumAnalyzer.FftSize, (now - lastFrame).TotalSeconds * format.SampleRate));
                lastFrame = now;

                analyzer.Update(format.SampleRate);
                var silent = analyzer.IsSilent;
                if (silent && wasSilent)
                    continue;

                wasSilent = silent;
                analyzer.Levels.CopyTo(levels);
                LevelsUpdated?.Invoke(levels);
            }

            client.Stop();
        }
        catch (Exception ex)
        {
            // Aygıt çıkarıldı, biçim desteklenmiyor, COM hatası: çubuklar durur, uygulama çalışmaya devam eder.
            Koy.Core.Diagnostics.CrashLog.Write("Müzik çubukları: ses yakalama", ex);
        }
        finally
        {
            Array.Clear(levels);
            try { LevelsUpdated?.Invoke(levels); } catch { }
            Release(capture);
            Release(client);
            Release(device);
            Release(enumerator);
        }
    }

    private readonly record struct SampleFormat(int SampleRate, int Channels, int BitsPerSample, bool IsFloat)
    {
        public int BlockAlign => Channels * BitsPerSample / 8;
    }

    private static unsafe bool TryOpen(out IMMDeviceEnumerator? enumerator, out IMMDevice? device, out IAudioClient? client,
        out IAudioCaptureClient? capture, out SampleFormat format)
    {
        enumerator = null;
        device = null;
        client = null;
        capture = null;
        format = default;

        var type = Type.GetTypeFromCLSID(ClsidMMDeviceEnumerator);
        if (type is null || Activator.CreateInstance(type) is not IMMDeviceEnumerator e)
            return false;
        enumerator = e;

        e.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out device);
        if (device is null)
            return false;

        var clientIid = typeof(IAudioClient).GUID;
        device.Activate(&clientIid, CLSCTX.CLSCTX_ALL, null, out var clientObj);
        client = clientObj as IAudioClient;
        if (client is null)
            return false;

        WAVEFORMATEX* mix = null;
        try
        {
            client.GetMixFormat(&mix);
            if (mix is null || !TryReadFormat(mix, out format))
                return false;

            client.Initialize(AUDCLNT_SHAREMODE.AUDCLNT_SHAREMODE_SHARED, LoopbackFlag, BufferDuration100ns, 0, mix, null);
        }
        finally
        {
            if (mix is not null)
                Marshal.FreeCoTaskMem((nint)mix);
        }

        var captureIid = typeof(IAudioCaptureClient).GUID;
        client.GetService(&captureIid, out var capturePtr);
        if (capturePtr is null)
            return false;

        capture = (IAudioCaptureClient)Marshal.GetObjectForIUnknown((nint)capturePtr);
        Marshal.Release((nint)capturePtr);
        return true;
    }

    private static unsafe bool TryReadFormat(WAVEFORMATEX* mix, out SampleFormat format)
    {
        format = default;
        var tag = mix->wFormatTag;
        var isFloat = tag == FormatFloat;
        if (tag == FormatExtensible && mix->cbSize >= 22)
            isFloat = ((WAVEFORMATEXTENSIBLE*)mix)->SubFormat == SubtypeFloat;
        else if (tag != FormatFloat && tag != FormatPcm && tag != FormatExtensible)
            return false;

        int bits = mix->wBitsPerSample;
        if (mix->nChannels == 0 || mix->nSamplesPerSec == 0)
            return false;
        if (isFloat ? bits != 32 : bits is not (16 or 24 or 32))
            return false;

        format = new SampleFormat((int)mix->nSamplesPerSec, mix->nChannels, bits, isFloat);
        return true;
    }

    // Bekleyen bütün paketleri tek kanala indirip analizöre verir; okunan çerçeve sayısını döner.
    private static unsafe int Drain(IAudioCaptureClient capture, SampleFormat format, SpectrumAnalyzer analyzer, ref float[] mono)
    {
        var total = 0;
        capture.GetNextPacketSize(out var packet);
        while (packet > 0)
        {
            byte* data;
            capture.GetBuffer(&data, out var frames, out var flags, null, null);
            try
            {
                if (frames > 0)
                {
                    if (mono.Length < frames)
                        mono = new float[frames];

                    var span = mono.AsSpan(0, (int)frames);
                    if ((flags & SilentFlag) != 0 || data is null)
                        span.Clear();
                    else
                        ToMono(data, format, span);

                    analyzer.AddSamples(span);
                    total += (int)frames;
                }
            }
            finally
            {
                capture.ReleaseBuffer(frames);
            }

            capture.GetNextPacketSize(out packet);
        }

        return total;
    }

    private static unsafe void ToMono(byte* data, SampleFormat format, Span<float> mono)
    {
        var channels = format.Channels;
        var bytes = format.BitsPerSample / 8;
        var stride = format.BlockAlign;
        var inv = 1f / channels;
        for (var frame = 0; frame < mono.Length; frame++)
        {
            var p = data + frame * stride;
            var sum = 0f;
            for (var c = 0; c < channels; c++, p += bytes)
            {
                sum += format.IsFloat
                    ? *(float*)p
                    : bytes switch
                    {
                        2 => *(short*)p / 32768f,
                        3 => ((p[0] << 8) | (p[1] << 16) | (p[2] << 24)) / 2147483648f,
                        _ => *(int*)p / 2147483648f
                    };
            }

            mono[frame] = sum * inv;
        }
    }

    private static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
        {
            try { Marshal.ReleaseComObject(com); } catch { }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        Stop();
        LevelsUpdated = null;
    }
}
