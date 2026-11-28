using Koy.Core.Dictation;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace Koy.Platform.Dictation;

/// <summary>
/// Mikrofon kaydı: WinRT <see cref="MediaCapture"/>, 16 kHz tek kanal 16 bit WAV'ı bellekteki akışa yazar.
/// Elle P/Invoke ve ek paket yok. Mikrofon yalnız kayıt sürerken açıktır (AGENTS #5): <see cref="StartAsync"/> açar,
/// <see cref="StopAsync"/> / <see cref="DiscardAsync"/> bırakır. Windows'un mikrofon gizlilik göstergesi kendiliğinden yanar.
/// Canlı düzey (dalga formu) vermez; blok düzeyleri kayıt bitince WAV'dan okunur (sessizlik denetimi için yeter).
/// </summary>
public sealed class MediaCaptureRecorder : IAudioRecorder, IDisposable
{
    /// <summary>Bundan kısa kayıt bir sözcük bile değildir; hiç gönderilmez.</summary>
    private const double MinSeconds = 0.25;

    /// <summary>Sessizlik denetiminin blok süresi (1024 örnek, 16 kHz).</summary>
    private const double ChunkSeconds = 0.064;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private MediaCapture? _capture;
    private InMemoryRandomAccessStream? _stream;
    private bool _paused;

    public bool IsRecording { get; private set; }

    // Bu kayıtçı canlı düzey vermez; olay arayüzün gereği.
    public event Action<double>? LevelChanged
    {
        add { }
        remove { }
    }

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (IsRecording)
                return;

            var capture = new MediaCapture();
            try
            {
                await capture.InitializeAsync(new MediaCaptureInitializationSettings { StreamingCaptureMode = StreamingCaptureMode.Audio });

                var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Medium);
                profile.Audio.SampleRate = WavAudio.SampleRate;
                profile.Audio.ChannelCount = WavAudio.Channels;
                profile.Audio.BitsPerSample = WavAudio.BitsPerSample;
                profile.Audio.Bitrate = (uint)(WavAudio.SampleRate * WavAudio.Channels * WavAudio.BitsPerSample);

                var stream = new InMemoryRandomAccessStream();
                await capture.StartRecordToStreamAsync(profile, stream);
                _capture = capture;
                _stream = stream;
                _paused = false;
                IsRecording = true;
            }
            catch
            {
                capture.Dispose();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RecordedAudio?> StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!IsRecording || _capture is null || _stream is null)
                return null;

            var capture = _capture;
            var stream = _stream;
            _capture = null;
            _stream = null;
            IsRecording = false;

            byte[] wav;
            try
            {
                await capture.StopRecordAsync();
                wav = await ReadAllAsync(stream);
            }
            finally
            {
                stream.Dispose();
                capture.Dispose();
            }

            var (seconds, levels) = WavAudio.ChunkLevels(wav, ChunkSeconds);
            return seconds < MinSeconds ? null : new RecordedAudio(wav, seconds, levels, ChunkSeconds);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetPausedAsync(bool paused)
    {
        await _gate.WaitAsync();
        try
        {
            if (!IsRecording || _capture is null || _paused == paused)
                return;

            if (paused)
                await _capture.PauseRecordAsync(MediaCapturePauseBehavior.ReleaseHardwareResources);
            else
                await _capture.ResumeRecordAsync();
            _paused = paused;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DiscardAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var capture = _capture;
            var stream = _stream;
            _capture = null;
            _stream = null;
            IsRecording = false;
            if (capture is null)
                return;

            try
            {
                await capture.StopRecordAsync();
            }
            finally
            {
                stream?.Dispose();
                capture.Dispose();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _capture?.Dispose();
        _stream?.Dispose();
        _gate.Dispose();
    }

    private static async Task<byte[]> ReadAllAsync(InMemoryRandomAccessStream stream)
    {
        var size = (uint)stream.Size;
        stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
