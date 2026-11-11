using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.Core.Lyrics;
using Koy.Core.Media;
using Koy.Platform.Audio;

namespace Koy.App.Features.Music;

/// <summary>
/// Müzik paneli ViewModel'i. IMediaService ile arayüz arasındaki köprüyü kurar.
/// Zamanlayıcı yalnız panel Expanded durumundayken çalışır (%0 boşta CPU).
/// </summary>
public sealed partial class MusicViewModel : ObservableObject, IDisposable
{
    private readonly IMediaService _mediaService;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _progressTimer;
    private bool _isActive;
    private bool _disposed;

    [ObservableProperty]
    private bool _hasTrack;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private string _sourceAppId = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWideCover))]
    private ImageSource? _thumbnail;

    /// <summary>
    /// Kapak yatay mı (video küçük resmi: Chrome/YouTube Windows'a ~150×83 verir). Yataysa kare alana kırpılıp
    /// iki katından fazla büyütülmez; tamamı gösterilir, arkasını aynı resmin bulanık hâli doldurur.
    /// </summary>
    public bool IsWideCover => Thumbnail is BitmapSource b && b.PixelHeight > 0 && b.PixelWidth > b.PixelHeight * 1.15;

    [ObservableProperty]
    private TimeSpan _position = TimeSpan.Zero;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.Zero;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _positionText = "0:00";

    [ObservableProperty]
    private string _durationText = "0:00";

    [ObservableProperty]
    private IReadOnlyList<MediaSessionInfo> _sessions = [];

    public bool HasMultipleSessions => Sessions.Count > 1;
    public bool ShowCollapsedMedia => HasTrack && IsPlaying;

    public event Action? TrackChanged;

    // Müzik kartındaki ses çubukları: yakalama servisi yalnız kart görünür ve müzik çalarken alınır.
    private readonly Func<SpectrumCaptureService>? _spectrumAcquire;
    private readonly Action? _spectrumRelease;
    private SpectrumCaptureService? _spectrum;
    private bool _spectrumAllowed;

    /// <summary>Çubukların düzeyleri (arayüz iş parçacığında güncellenir).</summary>
    public SpectrumFeed Spectrum { get; }

    /// <summary>Kartta çubuk satırı gösterilsin mi (ayar açık, pil tasarrufu hareketi kısmıyor, yakalama mümkün).</summary>
    [ObservableProperty]
    private bool _showSpectrum;

    // Albüm rengi: stil Düz değilse ve kapak renkliyse parça değişince bir kez, arka planda hesaplanır.
    private MusicCardStyle _cardStyle = MusicCardStyle.Tinted;
    private CoverFingerprint? _coverKey;
    private int _paletteGeneration;

    /// <summary>Kart kapaktan boyanıyor (stil Düz değil ve kapak renkli). Değilse XAML Tokens fırçalarında kalır.</summary>
    [ObservableProperty]
    private bool _hasTint;

    /// <summary>Kartın zemini küçültülmüş kapak (Albüm kapağı stili, renkli kapak).</summary>
    [ObservableProperty]
    private bool _hasBackdrop;

    [ObservableProperty]
    private Brush? _cardBrush;

    [ObservableProperty]
    private Brush? _subTextBrush;

    [ObservableProperty]
    private Brush? _spectrumBrush;

    /// <summary>Kapak arka planının altındaki hafif ton (Card, alfa 0,30).</summary>
    [ObservableProperty]
    private Brush? _tintOverlayBrush;

    /// <summary>32 px kapak; kart büyütür (UniformToFill, Linear). Dondurulmuş.</summary>
    [ObservableProperty]
    private Brush? _backdropBrush;

    /// <summary>Müzik kartı stili; çentik kurulurken ayardan verilir.</summary>
    public void SetCardStyle(MusicCardStyle style)
    {
        if (style == _cardStyle)
            return;

        _cardStyle = style;
        _coverKey = null;
        UpdateCoverPalette(_mediaService.Current?.Thumbnail);
    }

    private void UpdateCoverPalette(byte[]? bytes)
    {
        if (_cardStyle == MusicCardStyle.Neutral || bytes is null || bytes.Length == 0)
        {
            _paletteGeneration++;
            _coverKey = null;
            ApplyPalette(null, null);
            return;
        }

        var key = CoverFingerprint.Of(bytes);
        if (key == _coverKey)
            return; // aynı kapak: yeniden hesaplanmaz

        _coverKey = key;
        var generation = ++_paletteGeneration;
        var wantBackdrop = _cardStyle == MusicCardStyle.Artwork;
        // 32 px çözüm ve palet arka planda; sonuç arayüz iş parçacığında uygulanır (parça artık değiştiyse atılır).
        Task.Run(() => CoverSampler.Sample(bytes, wantBackdrop)).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                _dispatcher.InvokeAsync(() =>
                {
                    if (!_disposed && generation == _paletteGeneration)
                        ApplyPalette(t.Result.Palette, t.Result.Small);
                });
        }, TaskScheduler.Default);
    }

    // Albüm rengi stilinde kart zemini çentiğin etkin malzemesinin opaklığında (onaylı demo CamMuzikKarti.dc.html):
    // Katı 255, Buzlu 219, Cam 184. Kapak resmi stili opak kalır; okunurluğu koyu perde sağlar.
    private byte _cardAlpha = 255;
    private CoverPalette? _palette;

    /// <summary>Albüm rengi kart zemininin alfası; çentik malzemesi değişince çentik verir.</summary>
    public void SetCardAlpha(byte alpha)
    {
        if (alpha == _cardAlpha)
            return;

        _cardAlpha = alpha;
        if (_palette is not null)
            CardBrush = Solid(_palette.Card, _cardAlpha);
    }

    private void ApplyPalette(CoverPalette? palette, BitmapSource? small)
    {
        _palette = palette;
        if (palette is null)
        {
            HasTint = false;
            HasBackdrop = false;
            CardBrush = SubTextBrush = SpectrumBrush = TintOverlayBrush = BackdropBrush = null;
            return;
        }

        CardBrush = Solid(palette.Card, _cardAlpha);
        SubTextBrush = Solid(palette.SubText, 255);
        SpectrumBrush = Solid(palette.Spectrum, 0xBF); // alfa 0,75
        if (small is not null)
        {
            TintOverlayBrush = Solid(palette.Card, 0x4D); // alfa 0,30
            var image = new ImageBrush(small) { Stretch = Stretch.UniformToFill };
            image.Freeze();
            BackdropBrush = image;
        }
        else
        {
            TintOverlayBrush = BackdropBrush = null;
        }

        HasBackdrop = small is not null;
        HasTint = true;
    }

    private static SolidColorBrush Solid(Koy.Core.Appearance.Rgb c, byte alpha)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>Müzik kartındaki eşzamanlı sözler.</summary>
    public LyricsViewModel Lyrics { get; }

    public MusicViewModel(IMediaService mediaService, Dispatcher? dispatcher = null,
        Func<SpectrumCaptureService>? spectrumAcquire = null, Action? spectrumRelease = null,
        ILyricsProvider? lyricsProvider = null, MusicCardStyle cardStyle = MusicCardStyle.Tinted)
    {
        _mediaService = mediaService;
        _cardStyle = cardStyle;
        _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _spectrumAcquire = spectrumAcquire;
        _spectrumRelease = spectrumRelease;
        Spectrum = new SpectrumFeed(SpectrumCaptureService.BandCount, _dispatcher);
        Lyrics = new LyricsViewModel(mediaService, lyricsProvider, _dispatcher);
        Lyrics.PropertyChanged += OnLyricsPropertyChanged;

        _progressTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _progressTimer.Tick += OnProgressTick;

        _mediaService.Changed += OnMediaChanged;
        _mediaService.TimelineChanged += OnTimelineChanged;
        _mediaService.SessionsChanged += OnSessionsChanged;

        if (_mediaService.Current is not null)
            ApplyMediaInfo(_mediaService.Current);

        if (_mediaService.Timeline is not null)
            ApplyTimeline(_mediaService.Timeline);

        Sessions = _mediaService.Sessions;
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        Lyrics.SetPanelActive(active);
        UpdateProgressTimer();
    }

    // Sözler çubukların yerini alır: görünmeyen çubuk için ses yakalanmaz (AGENTS #5).
    private void OnLyricsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsViewModel.IsShown))
            SetSpectrumAllowed(_spectrumAllowed);
    }

    /// <summary>Ses çubukları izinli mi (Çentik ayarındaki "kapak ve dalga" açık ve pil tasarrufu hareketi kısmıyor).</summary>
    public void SetSpectrumAllowed(bool allowed)
    {
        _spectrumAllowed = allowed;
        ShowSpectrum = allowed && _spectrumAcquire is not null && !Lyrics.IsShown;
        UpdateSpectrum();
    }

    // Yakalama yalnız kart görünür (çentik açık, Ana sekmesi) ve müzik çalarken sürer; aksi halde %0 CPU.
    private void UpdateSpectrum()
    {
        var want = !_disposed && _isActive && HasTrack && IsPlaying && ShowSpectrum;
        if (want && _spectrum is null)
        {
            try
            {
                _spectrum = _spectrumAcquire!();
                _spectrum.LevelsUpdated += Spectrum.Push;
            }
            catch (Exception ex)
            {
                Koy.Core.Diagnostics.CrashLog.Write("Müzik çubukları başlatılamadı", ex);
                _spectrum = null;
            }
        }
        else if (!want && _spectrum is not null)
        {
            _spectrum.LevelsUpdated -= Spectrum.Push;
            _spectrum = null;
            _spectrumRelease?.Invoke();
            Spectrum.Clear();
        }
    }

    private void UpdateProgressTimer()
    {
        UpdateSpectrum();
        if (_isActive && HasTrack && IsPlaying)
        {
            if (!_progressTimer.IsEnabled)
                _progressTimer.Start();
        }
        else
        {
            if (_progressTimer.IsEnabled)
                _progressTimer.Stop();
        }
    }

    [RelayCommand]
    private async Task PlayPause() => await _mediaService.PlayPauseAsync();

    [RelayCommand]
    private async Task Next() => await _mediaService.NextAsync();

    [RelayCommand]
    private async Task Previous() => await _mediaService.PreviousAsync();

    [RelayCommand]
    private async Task Seek(double progressFraction)
    {
        if (Duration > TimeSpan.Zero)
        {
            var clamped = Math.Clamp(progressFraction, 0.0, 1.0);
            var target = TimeSpan.FromTicks((long)(Duration.Ticks * clamped));
            Position = target;
            Progress = clamped;
            PositionText = FormatTime(target);
            await _mediaService.SeekAsync(target);
        }
    }

    [RelayCommand]
    private async Task SelectSession(string sessionId) => await _mediaService.SelectSessionAsync(sessionId);

    private void OnMediaChanged(object? sender, MediaInfo? info)
    {
        _dispatcher.InvokeAsync(() =>
        {
            var oldTitle = Title;
            ApplyMediaInfo(info);
            if (HasTrack && oldTitle != Title)
                TrackChanged?.Invoke();
        });
    }

    private void OnTimelineChanged(object? sender, PlaybackTimeline? timeline)
    {
        _dispatcher.InvokeAsync(() => ApplyTimeline(timeline));
    }

    private void OnSessionsChanged(object? sender, IReadOnlyList<MediaSessionInfo> sessions)
    {
        _dispatcher.InvokeAsync(() =>
        {
            Sessions = sessions;
            OnPropertyChanged(nameof(HasMultipleSessions));
        });
    }

    private void ApplyMediaInfo(MediaInfo? info)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.Title))
        {
            HasTrack = false;
            Title = string.Empty;
            Artist = string.Empty;
            SourceAppId = string.Empty;
            IsPlaying = false;
            Thumbnail = null;
            UpdateCoverPalette(null);
            UpdateProgressTimer();
            OnPropertyChanged(nameof(ShowCollapsedMedia));
            return;
        }

        HasTrack = true;
        Title = info.Title;
        Artist = info.Artist;
        SourceAppId = MediaAppName.FromSourceAppId(info.SourceAppId);
        IsPlaying = info.IsPlaying;
        Thumbnail = DecodeThumbnail(info.Thumbnail);
        UpdateCoverPalette(info.Thumbnail);

        UpdateProgressTimer();
        OnPropertyChanged(nameof(ShowCollapsedMedia));
    }

    private void ApplyTimeline(PlaybackTimeline? timeline)
    {
        if (timeline is null)
        {
            Position = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            Progress = 0;
            PositionText = "0:00";
            DurationText = "0:00";
            return;
        }

        Duration = PlaybackClock.Duration(timeline);
        Position = PlaybackClock.PositionAt(timeline, IsPlaying, DateTimeOffset.Now);
        Progress = Duration.TotalSeconds > 0 ? Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0.0, 1.0) : 0.0;
        PositionText = FormatTime(Position);
        DurationText = FormatTime(Duration);
    }

    private void OnProgressTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || Duration <= TimeSpan.Zero)
            return;

        var nextPos = Position + TimeSpan.FromSeconds(1);
        if (nextPos > Duration)
            nextPos = Duration;

        Position = nextPos;
        Progress = Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0.0, 1.0);
        PositionText = FormatTime(Position);
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";
    }

    private static BitmapImage? DecodeThumbnail(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return null;

        try
        {
            // Büyük müzik kartında kapak 172 DIP; %150 ölçekte ~260 piksel gerekir. Kaynak daha büyükse
            // MaxCoverPixels'e indirilir (bellek ~400 KB), küçükse olduğu gibi çözülür: büyütmek netlik katmaz,
            // ölçeklemeyi ekranda yüksek kaliteli süzgeç yapar.
            var width = ReadPixelWidth(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            if (width > MaxCoverPixels)
                bitmap.DecodePixelWidth = MaxCoverPixels;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private const int MaxCoverPixels = 320;

    // Yalnız başlık okunur (OnDemand + DelayCreation): piksel çözülmez.
    private static int ReadPixelWidth(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            return decoder.Frames.Count > 0 ? decoder.Frames[0].PixelWidth : 0;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _paletteGeneration++;
        Lyrics.PropertyChanged -= OnLyricsPropertyChanged;
        Lyrics.Dispose();
        UpdateSpectrum();
        _progressTimer.Stop();
        _progressTimer.Tick -= OnProgressTick;
        _mediaService.Changed -= OnMediaChanged;
        _mediaService.TimelineChanged -= OnTimelineChanged;
        _mediaService.SessionsChanged -= OnSessionsChanged;
    }
}
