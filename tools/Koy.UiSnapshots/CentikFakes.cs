using Koy.Core.Audio;
using Koy.Core.Display;
using Koy.Core.Media;
using Koy.Core.Power;
using Koy.Core.Radio;
using Koy.Core.Shelf;
using Koy.Core.Shell;
using Koy.Core.Weather;

namespace Koy.UiSnapshots;

// Çentik sahnesi için sahte servisler: gerçek medya, pil, hava, ses ya da radyo servisine dokunulmaz.

internal sealed class FakeMedia(MediaInfo? current) : IMediaService
{
    public event EventHandler<MediaInfo?>? Changed { add { } remove { } }
    public event EventHandler<PlaybackTimeline?>? TimelineChanged { add { } remove { } }
    public event EventHandler<IReadOnlyList<MediaSessionInfo>>? SessionsChanged { add { } remove { } }

    public MediaInfo? Current { get; } = current;
    public PlaybackTimeline? Timeline { get; } = current is null
        ? null
        : new PlaybackTimeline(TimeSpan.FromSeconds(84), TimeSpan.Zero, TimeSpan.FromSeconds(221), DateTimeOffset.MinValue);
    public IReadOnlyList<MediaSessionInfo> Sessions { get; } = [];

    public Task StartAsync() => Task.CompletedTask;
    public void Stop() { }
    public Task PlayPauseAsync() => Task.CompletedTask;
    public Task NextAsync() => Task.CompletedTask;
    public Task PreviousAsync() => Task.CompletedTask;
    public Task SeekAsync(TimeSpan position) => Task.CompletedTask;
    public Task SelectSessionAsync(string sessionId) => Task.CompletedTask;
    public void Dispose() { }
}

// Örnek metin bu proje için yazıldı; ağa ve dosyaya gidilmez. Satırlar FakeMedia'nın 84. saniyesi çevresinde.
internal sealed class FakeLyrics : Koy.Core.Lyrics.ILyricsProvider
{
    private static readonly string[] Sample =
    [
        "Yol uzar, farlar söner",
        "Şehir arkamda kalır",
        "",
        "Gece boyu radyoda aynı şarkı çalar, kimse söndürmez",
        "Camda ince bir yağmur",
        "Sabah daha çok uzak",
        "Dönüş yok bu yoldan",
        "Yine de gülümserim"
    ];

    public Task<Koy.Core.Lyrics.LyricsLoadResult> LoadAsync(Koy.Core.Lyrics.LyricsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new Koy.Core.Lyrics.LyricsLoadResult(
            Koy.Core.Lyrics.LyricsLoadStatus.Found,
            Sample.Select((text, i) => new Koy.Core.Lyrics.LyricLine(TimeSpan.FromSeconds(70 + i * 5), text)).ToArray()));
}

internal sealed class FakePower(int percent = 86, bool charging = false, bool hasBattery = true) : IPowerService
{
    public bool HasBattery => hasBattery;
    public int RemainingPercent => percent;
    public bool IsCharging => charging;
    public BatteryStatus Status => charging ? BatteryStatus.Charging : BatteryStatus.Discharging;
    public event EventHandler<PowerChangedEventArgs>? PowerChanged { add { } remove { } }
    public void Dispose() { }
}

internal sealed class FakeWeather : IWeatherService
{
    public WeatherInfo? CurrentWeather { get; } = new(14, 2,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Partly cloudy" : "Parçalı bulutlu", "PartlyCloudy", DateTimeOffset.Now);
    public event EventHandler<WeatherInfo?>? WeatherChanged { add { } remove { } }
    public Task<WeatherInfo?> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult(CurrentWeather);
    public Task RefreshAsync(double latitude, double longitude, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
}

internal sealed class FakeShelf(IReadOnlyList<ShelfItem> items) : IShelfService
{
    public IReadOnlyList<ShelfItem> Items { get; } = items;
    public event EventHandler? ItemsChanged { add { } remove { } }
    public void AddFile(string path) { }
    public void AddText(string text) { }
    public void Remove(string id) { }
    public void Clear() { }
    public void Dispose() { }
}

internal sealed class FakeAudio : IAudioService
{
    public float Volume => 0.42f;
    public bool IsMuted => false;
    public event EventHandler<AudioStateChangedEventArgs>? StateChanged { add { } remove { } }
    public void SetVolume(float volume) { }
    public void SetMute(bool isMuted) { }
    public void ToggleMute() { }
    public void Dispose() { }
}

internal sealed class FakeBrightness : IBrightnessService
{
    private readonly bool _supported;
    public FakeBrightness(bool supported = true) => _supported = supported;
    public bool IsSupported => _supported;
    public int Brightness => 64;
    public Koy.Core.Display.BrightnessSourceKind Source => _supported ? Koy.Core.Display.BrightnessSourceKind.Wmi : Koy.Core.Display.BrightnessSourceKind.None;
    public event EventHandler<int>? BrightnessChanged { add { } remove { } }
    public event EventHandler? AvailabilityChanged { add { } remove { } }
    public void SetBrightness(int brightness) { }
    public void Refresh() { }
    public void Dispose() { }
}

internal sealed class FakeRadio : IRadioService
{
    public RadioAccess Access => RadioAccess.Allowed;
    public RadioState WiFiState => RadioState.On;
    public RadioState BluetoothState => RadioState.On;
    public event EventHandler<RadioStateChangedEventArgs>? StateChanged { add { } remove { } }
    public Task RefreshAsync() => Task.CompletedTask;
    public Task<bool> SetStateAsync(RadioKind kind, bool enable) => Task.FromResult(true);
    public Task<bool> ToggleAsync(RadioKind kind) => Task.FromResult(true);
    public void OpenSettings(RadioKind kind) { }
    public void OpenPrivacySettings() { }
    public void Dispose() { }
}

internal sealed class FakeFocusAssist : IFocusAssistService
{
    public bool IsActive => false;
    public bool IsDoNotDisturb => false;
    public event EventHandler<bool>? StateChanged { add { } remove { } }
    public void OpenSettings() { }
    public void Dispose() { }
}
