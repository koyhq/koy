using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Calendar;
using Koy.App.Features.ClipboardPanel;
using Koy.App.Features.ControlCenter;
using Koy.App.Features.Focus;
using Koy.App.Features.Home;
using Koy.App.Features.Music;
using Koy.App.Features.Notch.OSD;
using Koy.App.Features.Search;
using Koy.App.Resources;
using Koy.App.Features.Settings;
using Koy.App.Features.Shelf;
using Koy.App.Features.Status;
using Koy.App.Features.SystemInfo;
using Koy.Core.Notch;
using Koy.Core.Power;
using Koy.Core.Settings;
using Koy.Core.Visibility;

namespace Koy.App.Features.Notch;

public sealed partial class NotchViewModel : ObservableObject, IDisposable
{
    private readonly NotchController _controller;
    private readonly MusicViewModel _music;
    private readonly ControlCenterViewModel _controlCenter;
    private readonly StatusViewModel _status;
    private readonly FocusViewModel _focus;
    private readonly ShelfViewModel _shelf;
    private readonly CalendarViewModel _calendar;
    private readonly ClipboardViewModel? _clipboard;
    private readonly VisibilityController _visibility;
    private readonly Dispatcher _dispatcher;
    private DispatcherTimer? _clockTimer;
    private SearchViewModel? _search;
    private nint _windowBeforeSearch;
    private bool _disposed;

    public AppSettings Settings { get; }
    public NotchController Controller => _controller;
    public MusicViewModel Music => _music;
    public ControlCenterViewModel ControlCenter => _controlCenter;
    public StatusViewModel Status => _status;
    public FocusViewModel Focus => _focus;
    public ShelfViewModel Shelf => _shelf;
    public CalendarViewModel Calendar => _calendar;

    /// <summary>Arama paneli; ilk açılışta kurulur, açılmadıysa null.</summary>
    public SearchViewModel? Search => _search;

    /// <summary>Arama açık: durum satırı, sayfa ve sekmeler gizlidir.</summary>
    public bool IsSearchOpen => _search?.IsOpen == true;

    /// <summary>Arama kapandı; değer odağın döneceği pencere (yoksa 0).</summary>
    public event Action<nint>? SearchClosed;

    /// <summary>Pano sayfası; pano geçmişi ayarı kapalıysa null (sekme de yoktur).</summary>
    public ClipboardViewModel? Clipboard => _clipboard;

    /// <summary>Sistem sayfası ve Ana'daki kartlar aynı örneği paylaşır.</summary>
    public SystemStatsViewModel SystemStats => Home.Stats;

    /// <summary>Ana sayfanın verisi (müzik ya da selamlama, hava, çalışan zamanlayıcı, bildirim komutu).</summary>
    public HomeViewModel Home { get; }
    public VisibilityController Visibility => _visibility;
    public OsdIndicatorViewModel Osd { get; }

    /// <summary>Boşta saat gösterilsin mi (ayar: Saat ve pil / Yalnız saat / Hiçbiri).</summary>
    public bool ShowIdleClock => IdleDisplay.ShowClock(Settings.IdleDisplayMode);

    /// <summary>
    /// Boşta pil gösterilsin mi (ayar izin veriyorsa ve bilgisayarda pil varsa). Bir zamanlayıcı çalışırken pilin yerine
    /// kalan süre yazılır.
    /// </summary>
    public bool ShowIdleBattery => HasBattery && IdleDisplay.ShowBattery(Settings.IdleDisplayMode)
        && !IdleDisplay.IsRing(Settings.IdleDisplayMode) && !_focus.HasRunning;

    /// <summary>Boşta görünüm "halka pil ve gün/saat" mi. Ayar değişince çentik yeniden kurulduğundan ömür boyu sabittir.</summary>
    public bool IsIdleRing => IdleDisplay.IsRing(Settings.IdleDisplayMode);

    /// <summary>Klasik yerleşimde soldaki saat (halka modunda saat sağda, gün adıyla birlikte).</summary>
    public bool ShowIdleClassicClock => ShowIdleClock && !IsIdleRing;

    /// <summary>Halka modunda soldaki pil halkası ve yüzde. Pilsiz kasada ve zamanlayıcı çalışırken gizli.</summary>
    public bool ShowIdleRing => HasBattery && IsIdleRing && !_focus.HasRunning;

    /// <summary>Halka modunda Peek'te saatin yanındaki kısa tarih ("· 18 Kas").</summary>
    public bool ShowIdlePeekDate => IsIdleRing && State == NotchState.Peek;

    /// <summary>Halkanın dolu kısmı; yalnız pil yüzdesi değişince yeniden kurulur (dondurulmuş, zamanlayıcı yok).</summary>
    [ObservableProperty]
    private Geometry? _batteryRingGeometry;

    /// <summary>Halka rengi: düşük pilde (≤%20, şarjda değil) kırmızı, aksi halde yeşil.</summary>
    [ObservableProperty]
    private Brush _batteryRingBrush = RingNormalBrush;

    /// <summary>Halkanın yanındaki yüzde, dilin yazımıyla ("%43" / "43%").</summary>
    [ObservableProperty]
    private string _batteryPercentText = "";

    /// <summary>Halka modunda sağdaki gün ve saat ("Sal 19:21").</summary>
    [ObservableProperty]
    private string _idleDayTime = "";

    /// <summary>Halka modunda Peek'teki kısa tarih ("· 18 Kas").</summary>
    [ObservableProperty]
    private string _idlePeekDate = "";

    private static readonly Brush RingNormalBrush = Frozen(0x4A, 0xDE, 0x80);
    private static readonly Brush RingLowBrush = Frozen(0xFB, 0x71, 0x85);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private CultureInfo IdleCulture => new(Settings.Language == AppLanguage.English ? "en-US" : "tr-TR");

    /// <summary>Kapalı çentikte pil / Wi-Fi / Bluetooth yerine zamanlayıcının süresi görünsün mü.</summary>
    public bool ShowIdleTimer => _focus.HasRunning;

    /// <summary>Kapalı çentikteki süre ("18:24"); saniyede bir yalnız zamanlayıcı çalışırken değişir.</summary>
    public string IdleTimerText => _focus.RunningText;

    /// <summary>Pil tasarrufu: pille çalışırken çentik animasyonsuz (anında) geçer.</summary>
    /// <summary>Çentik biçimi yüzen ada mı. Ayar değişince çentik yeniden kurulduğundan ömür boyu sabittir.</summary>
    public bool IsFloating => NotchStyle.IsFloating(Settings.NotchStyle);

    /// <summary>Üst köşe kulağı: yapışıkta 12, yüzen adada 0 (kulaksız, bütün hâllerde).</summary>
    public double EarRadius => NotchStyle.EarRadius(Settings.NotchStyle);

    /// <summary>Pencerenin ekran üstünden uzaklığı (DIP): yapışıkta 0, yüzen adada 9. Tuval boyutu değişmez (AGENTS #13).</summary>
    public double TopOffsetDip => NotchStyle.TopOffsetDip(Settings.NotchStyle);

    public bool ReduceMotion => Settings.BatterySaverReduceMotion && _status.IsOnBattery;

    /// <summary>Müzik çalarken dalga simgesi gösterilsin mi.</summary>
    public bool ShowMusicWaveform => Settings.MusicShowWaveform;

    /// <summary>Açık çentiğin üstündeki sekmeler; ayarlarda açık paneller, ayardaki sırayla.</summary>
    public ObservableCollection<NotchTabViewModel> Tabs { get; } = [];

    /// <summary>Kontrol paneli açıksa Wi-Fi / Bluetooth izlenir ve kapalı çentikte simgeleri gösterilebilir.</summary>
    public bool ShowRadioStatus { get; }

    /// <summary>Boşta Wi-Fi / Bluetooth simgeleri gösterilsin mi (Kontrol paneli açık ve boşta görünüm "saat, pil ve bağlantılar").</summary>
    public bool ShowIdleRadio => ShowRadioStatus && !_focus.HasRunning
        && (IdleDisplay.ShowRadioWhenCollapsed(Settings.IdleDisplayMode)
            || (IdleDisplay.ShowRadio(Settings.IdleDisplayMode) && State == NotchState.Peek));

    /// <summary>"Çentiği açık tut" açık: fare çıkınca kapanmaz.</summary>
    public bool IsPinned => _controller.IsPinned;

    // --- GİZLİLİK GÖSTERGESİ (KAMERA / MİKROFON) ---
    [ObservableProperty]
    private bool _isCameraActive;

    [ObservableProperty]
    private bool _isMicrophoneActive;

    [ObservableProperty]
    private string _privacyToolTip = "";

    public bool HasPrivacyActivity => IsCameraActive || IsMicrophoneActive;

    public void UpdatePrivacyState(Koy.Core.Privacy.PrivacyState state)
    {
        if (!Settings.ShowPrivacyIndicator)
        {
            IsCameraActive = false;
            IsMicrophoneActive = false;
            PrivacyToolTip = string.Empty;
            OnPropertyChanged(nameof(HasPrivacyActivity));
            return;
        }

        IsCameraActive = state.IsCameraInUse;
        IsMicrophoneActive = state.IsMicrophoneInUse;
        PrivacyToolTip = Koy.Core.Privacy.CaptureUsagePolicy.FormatToolTip(
            state,
            Strings.Get("PrivacyTooltipCamAndMic"),
            Strings.Get("PrivacyTooltipCam"),
            Strings.Get("PrivacyTooltipMic"),
            Strings.Get("PrivacyUnknownApp"));
        OnPropertyChanged(nameof(HasPrivacyActivity));
    }

    public ObservableObject CurrentPanelContent => CurrentPanel switch
    {
        NotchPanel.Home => Home,
        NotchPanel.Focus => Focus,
        NotchPanel.Calendar => Calendar,
        NotchPanel.Clipboard => (ObservableObject?)Clipboard ?? Home,
        NotchPanel.System => SystemStats,
        NotchPanel.Shelf => Shelf,
        NotchPanel.ControlCenter => ControlCenter,
        _ => Home
    };

    [ObservableProperty]
    private NotchState _state = NotchState.Collapsed;

    [ObservableProperty]
    private NotchPanel _currentPanel = NotchPanel.Home;

    [ObservableProperty]
    private double _targetWidth = 236;

    [ObservableProperty]
    private double _targetHeight = 34;

    [ObservableProperty]
    private double _targetCornerRadius = 18;

    [ObservableProperty]
    private bool _isNotchVisible = true;

    [ObservableProperty]
    private string _title = "Koy";

    [ObservableProperty]
    private string _clockTime = DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    private int _batteryPercent = 100;

    [ObservableProperty]
    private bool _isCharging;

    [ObservableProperty]
    private bool _hasBattery;

    /// <summary>Boşta yerleşimi (saat, pil, simgeler) çizilsin mi: kapalıyken ve parça yokken fareyle üstüne gelince de (bkz. <see cref="NotchLayout"/>).</summary>
    public bool ShowCollapsedLayout => NotchLayout.ShowCollapsedLayout(State, Music.HasTrack);

    /// <summary>Şarkı bilgili ipucu yerleşimi çizilsin mi.</summary>
    public bool ShowPeekLayout => NotchLayout.ShowPeekLayout(State, Music.HasTrack);

    partial void OnStateChanged(NotchState value) => NotifyLayoutChanged();

    private void NotifyLayoutChanged()
    {
        OnPropertyChanged(nameof(ShowCollapsedLayout));
        OnPropertyChanged(nameof(ShowPeekLayout));
        if (IsIdleRing)
        {
            OnPropertyChanged(nameof(ShowIdlePeekDate));
            OnPropertyChanged(nameof(ShowIdleRadio));
        }
    }

    partial void OnHasBatteryChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowIdleBattery));
        OnPropertyChanged(nameof(ShowIdleRing));
    }

    [ObservableProperty]
    private double _batteryFillWidth = 17;

    public bool IsMusicPlaying => Music.HasTrack && Music.IsPlaying;

    public event Action<double, double>? TargetSizeChanged;
    public event Action<bool>? TargetVisibilityChanged;
    public event Action<int>? PanelTransitionTriggered;
    public event Action? ShakeRequested;

    public NotchViewModel(
        AppSettings settings,
        NotchController controller,
        MusicViewModel music,
        ControlCenterViewModel controlCenter,
        StatusViewModel status,
        FocusViewModel focus,
        ShelfViewModel shelf,
        CalendarViewModel calendar,
        SystemStatsViewModel systemStats,
        QuickLaunchViewModel quickLaunch,
        ClipboardViewModel? clipboard = null,
        VisibilityController? visibility = null,
        OsdIndicatorViewModel? osd = null,
        Dispatcher? dispatcher = null,
        Koy.Core.Shell.IShellActionService? shellActions = null)
    {
        Settings = settings;
        _controller = controller;
        _music = music;
        _controlCenter = controlCenter;
        _status = status;
        _focus = focus;
        _shelf = shelf;
        _calendar = calendar;
        _clipboard = clipboard;
        Home = new HomeViewModel(music, status, focus, systemStats, quickLaunch, shellActions, () => _controller.GoToPanel(NotchPanel.Focus),
            controlCenter.BluetoothDevices);
        quickLaunch.HoldOpen = HoldOpenForPicker;
        _visibility = visibility ?? new VisibilityController(settings.NotchVisibility);
        _dispatcher = dispatcher ?? (System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        Osd = osd ?? new OsdIndicatorViewModel(_dispatcher);

        _isNotchVisible = _visibility.IsVisible;

        _controller.StateChanged += OnStateChanged;
        _controller.PanelChanged += OnPanelChanged;
        _controller.PanelTransitionTriggered += OnPanelTransitionTriggered;
        _visibility.Changed += OnVisibilityChanged;
        _music.TrackChanged += OnTrackChanged;
        _music.PropertyChanged += OnMusicPropertyChanged;
        _focus.TimerFinished += OnFocusTimerFinished;
        _focus.PropertyChanged += OnFocusPropertyChanged;
        _controller.PinnedChanged += OnPinnedChanged;
        _status.PropertyChanged += OnStatusPropertyChanged;
        Osd.CanShow = () => !_disposed && State != NotchState.Expanded;
        Osd.Activated += OnOsdActivated;
        Osd.Dismissed += OnOsdStateChanged;

        _controller.EnabledPanelsChanged += OnEnabledPanelsChanged;
        RebuildTabs();

        // Windows tepsisi gizli olduğu için Wi-Fi ve Bluetooth'un durumunu gösteren tek yer çentik.
        ShowRadioStatus = _controller.EnabledPanels.Contains(NotchPanel.ControlCenter);
        if (ShowRadioStatus && IdleDisplay.ShowRadio(Settings.IdleDisplayMode))
            _controlCenter.StartRadioStatus();

        UpdateClock();
        UpdateBatteryState();
        UpdateGlassSurface();
        MaterialState.Changed += OnMaterialChanged;
        UpdateTargetSize();
        UpdateActivePanels();

        if (_isNotchVisible)
        {
            StartClockTimer();
        }
    }

    private void OnOsdActivated(object? sender, EventArgs e)
    {
        // Akıllı/Fareyle modunda çentik gizli olabilir; gösterge süresince (3 sn) görünür kalsın.
        _visibility.OnMediaActivity();
        // Kart (güncelleme, bildirim) gösterildiği sürece çentik gizlenmez; başka OSD kartın yerini alırsa bırakılır.
        _visibility.SetHold(Osd.Type == OsdType.Card);
        UpdateTargetSize();
    }

    private void OnOsdStateChanged(object? sender, EventArgs e)
    {
        _visibility.SetHold(false);
        UpdateTargetSize();
    }

    private void OnStatusPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StatusViewModel.BatteryPercent) or nameof(StatusViewModel.IsCharging) or nameof(StatusViewModel.HasBattery))
        {
            UpdateBatteryState();
        }
    }

    private void UpdateBatteryState()
    {
        BatteryPercent = _status.BatteryPercent;
        IsCharging = _status.IsCharging;
        HasBattery = _status.HasBattery;
        var pct = Math.Clamp(BatteryPercent, 0, 100);
        // Dış gövde 22 DIP, iç dolgu alanı 17 DIP (22 - 3 - 2)
        BatteryFillWidth = Math.Max(2.0, 17.0 * pct / 100.0);

        if (IsIdleRing)
            UpdateBatteryRing(pct);
    }

    private int _ringPercent = -1;

    private void UpdateBatteryRing(int pct)
    {
        BatteryRingBrush = IdleDisplayFormat.IsLow(pct, IsCharging) ? RingLowBrush : RingNormalBrush;
        if (pct == _ringPercent)
            return;

        _ringPercent = pct;
        BatteryPercentText = IdleDisplayFormat.Percent(pct, IdleCulture);
        var data = IdleDisplayFormat.ArcPath(pct);
        Geometry? geometry = null;
        if (data.Length > 0)
        {
            geometry = Geometry.Parse(data);
            geometry.Freeze();
        }
        BatteryRingGeometry = geometry;
    }

    private void StartClockTimer()
    {
        if (_clockTimer is not null)
            return;

        UpdateClock();
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
    }

    private void StopClockTimer()
    {
        if (_clockTimer is not null)
        {
            _clockTimer.Stop();
            _clockTimer = null;
        }
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockTime = now.ToString("HH:mm");
        if (IsIdleRing)
        {
            var culture = IdleCulture;
            IdleDayTime = IdleDisplayFormat.DayTime(now, culture);
            IdlePeekDate = "· " + IdleDisplayFormat.ShortDate(now, culture);
        }
    }

    private void OnPinnedChanged(object? sender, EventArgs e) => RunOnUi(() => OnPropertyChanged(nameof(IsPinned)));

    // Zamanlayıcı çalışmaya başlayınca / durunca kapalı çentikte pil ve simgeler ile süre yer değiştirir.
    private void OnFocusPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FocusViewModel.HasRunning))
        {
            OnPropertyChanged(nameof(ShowIdleTimer));
            OnPropertyChanged(nameof(ShowIdleBattery));
            OnPropertyChanged(nameof(ShowIdleRing));
            OnPropertyChanged(nameof(ShowIdleRadio));
        }
        else if (e.PropertyName == nameof(FocusViewModel.RunningText))
        {
            OnPropertyChanged(nameof(IdleTimerText));
        }
    }

    private void OnFocusTimerFinished()
    {
        _controller.OnTrackChanged(); // Kısa süre Peek moduna geçirir
        ShakeRequested?.Invoke();
    }

    private void OnPanelTransitionTriggered(int direction)
    {
        PanelTransitionTriggered?.Invoke(direction);
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MusicViewModel.IsPlaying) or nameof(MusicViewModel.HasTrack))
        {
            OnPropertyChanged(nameof(IsMusicPlaying));
            NotifyLayoutChanged();
            if (State == NotchState.Collapsed)
                UpdateTargetSize();
        }
    }

    private void OnTrackChanged()
    {
        _controller.OnTrackChanged();
        _visibility.OnMediaActivity();
    }

    // Core zamanlayıcıları (otomatik peek, fare çıkışı, medya peek) ThreadPool'dan tikler;
    // durum ve pencere nesnelerine yalnız UI thread'inden dokunulur.
    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.InvokeAsync(() =>
        {
            if (!_disposed)
                action();
        });
    }

    private void OnStateChanged(object? sender, NotchState newState)
    {
        RunOnUi(() => ApplyState(newState));
    }

    private void ApplyState(NotchState newState)
    {
        State = newState;
        // Kart çentik açılınca kalkar (açık çentik kartın yerine geçer).
        if (newState == NotchState.Expanded && Osd.Type == OsdType.Card)
            Osd.Dismiss();
        UpdateTargetSize();
        UpdateActivePanels();
        _visibility.SetExpanded(State == NotchState.Expanded);
    }

    private void OnPanelChanged(object? sender, NotchPanel newPanel)
    {
        foreach (var tab in Tabs)
            tab.IsSelected = tab.Panel == newPanel;

        CurrentPanel = newPanel;
        OnPropertyChanged(nameof(CurrentPanelContent));
        UpdateTargetSize();
        UpdateActivePanels();
    }

    private void OnEnabledPanelsChanged(object? sender, EventArgs e) => RunOnUi(RebuildTabs);

    // Sekmeler açık panellerden ve onların sırasından türer; liste değişince baştan kurulur.
    private void RebuildTabs()
    {
        Tabs.Clear();
        foreach (var panel in _controller.EnabledPanels)
            Tabs.Add(NotchTabs.Create(panel, panel == _controller.CurrentPanel));
    }

    private void UpdateActivePanels()
    {
        // Arama açıkken sayfalar görünmez; servisleri de çalışmaz.
        var isExpanded = State == NotchState.Expanded && !IsSearchOpen;
        // Ses çubukları "kapak ve dalga" ayarına bağlı; pil tasarrufu hareketi kısarken hiç yakalanmaz.
        _music.SetSpectrumAllowed(Settings.MusicShowWaveform && !ReduceMotion);
        _music.Lyrics.SetAllowed(Settings.MusicLyricsEnabled);
        _music.SetActive(isExpanded && CurrentPanel == NotchPanel.Home);
        // Saat, hava ve pil her sekmede üstteki durum satırında görünür.
        _status.SetActive(isExpanded);
        _controlCenter.SetActive(isExpanded && CurrentPanel == NotchPanel.ControlCenter);
        // Zamanlayıcı süresi görünür çentikte (kapalıysa pil yerine, açıksa Ana satırında) ve Odak sayfasında tazelenir.
        _focus.SetViewing(isExpanded && CurrentPanel == NotchPanel.Focus, IsNotchVisible);

        // Her servis yalnız sayfası görünürken çalışır (boşta %0 CPU): sistem verisi Ana ve Sistem'de (kartlar orada),
        // pano, görevler, takvim ve hızlı başlat kendi sayfalarında.
        var shown = isExpanded && IsNotchVisible;
        SystemStats.SetActive(shown && CurrentPanel is NotchPanel.Home or NotchPanel.System);
        Home.QuickLaunch.SetActive(shown && CurrentPanel == NotchPanel.Home);
        // Kulaklık kartı yalnız Ana görünürken Bluetooth izleyicisini ister (Kontrol kendi "control" sahibiyle ister).
        _controlCenter.BluetoothDevices.SetViewing("home", shown && CurrentPanel == NotchPanel.Home);
        _focus.Tasks?.SetActive(shown && CurrentPanel == NotchPanel.Focus);
        _calendar.SetActive(shown && CurrentPanel == NotchPanel.Calendar);
        _clipboard?.SetActive(shown && CurrentPanel == NotchPanel.Clipboard);
    }

    private void OnVisibilityChanged(object? sender, VisibilityResult result)
    {
        RunOnUi(() => ApplyVisibility(result));
    }

    private void ApplyVisibility(VisibilityResult result)
    {
        IsNotchVisible = result.IsVisible;
        UpdateActivePanels();
        if (IsNotchVisible)
            StartClockTimer();
        else
            StopClockTimer();

        TargetVisibilityChanged?.Invoke(IsNotchVisible);
    }

    public void UpdateTargetSize()
    {
        if (Osd.IsActive)
        {
            var isCard = Osd.Type == OsdType.Card;
            TargetWidth = isCard ? NotchLayout.CardWidth : 260;
            TargetHeight = isCard ? NotchLayout.CardHeight : 40;
            TargetCornerRadius = NotchStyle.CornerRadius(Settings.NotchStyle, isCard ? NotchSurface.Card : NotchSurface.Closed,
                TargetHeight, isCard ? 28 : 20);
        }
        else
        {
            (TargetWidth, TargetHeight, TargetCornerRadius) = State switch
            {
                NotchState.Collapsed => (IsMusicPlaying ? 280 : 236, 34, 18),
                NotchState.Peek => (300, 38, 20),
                // Her sekmede aynı sabit gövde: sekme değişince çentik boyut değiştirmez (bkz. NotchLayout).
                NotchState.Expanded => (NotchLayout.ExpandedWidth, NotchLayout.ExpandedHeight, 34),
                _ => (236, 34, 18)
            };
            TargetCornerRadius = NotchStyle.CornerRadius(Settings.NotchStyle,
                State == NotchState.Expanded ? NotchSurface.Expanded : NotchSurface.Closed, TargetHeight, TargetCornerRadius);
        }

        UpdateSurface();
        TargetSizeChanged?.Invoke(TargetWidth, TargetHeight);
    }

    // --- MALZEME (Katı / Buzlu / Cam) ---
    // Kapalı çentik, Peek ve HUD fiziksel çentik yanılsaması için hep opak #0A0A0B; açık çentik ve kart etkin malzemenin
    // opaklığında (onaylı kaynak CamDurumlar.dc.html). Geçiş durum değişimiyle anlık; boyut yayı aynı kalır, Storyboard yok.

    private static readonly Brush OpaqueSurface = FrozenArgb(0xFF, 0x0A, 0x0A, 0x0B);
    private Brush _glassSurface = OpaqueSurface;

    /// <summary>Çentik şeklinin zemini.</summary>
    [ObservableProperty]
    private Brush _surfaceBrush = OpaqueSurface;

    private static Brush FrozenArgb(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private void UpdateGlassSurface()
    {
        var level = MaterialState.ForNotch(Settings);
        _glassSurface = level == Koy.Core.Appearance.MaterialSetting.Solid
            ? OpaqueSurface
            : FrozenArgb(Koy.Core.Appearance.MaterialSetting.SurfaceAlpha(level), 0x0A, 0x0A, 0x0B);
        // Albüm rengi müzik kartı da aynı opaklıkta (kart yalnız açık çentikte görünür).
        _music.SetCardAlpha(Koy.Core.Appearance.MaterialSetting.SurfaceAlpha(level));
    }

    private void UpdateSurface()
    {
        var glassy = Osd.IsActive ? Osd.Type == OsdType.Card : State == NotchState.Expanded;
        SurfaceBrush = glassy ? _glassSurface : OpaqueSurface;
    }

    private void OnMaterialChanged()
    {
        UpdateGlassSurface();
        UpdateSurface();
    }

    // Dosya seçici açıkken fare çentikten ayrılır; çentik kapanıp seçiciyi yetim bırakmasın diye geçici olarak açık tutulur.
    // Kullanıcının iğnesinden (IsPinned) ayrı sayaçlı tutma: iğne simgesi yanmaz, seçici kapanınca normal davranış döner.
    private void HoldOpenForPicker(bool hold)
    {
        if (hold)
            _controller.HoldOpen();
        else
            _controller.ReleaseHold();
    }

    public void ShowVolume(float volume, bool isMuted) => Osd.ShowVolume(volume, isMuted);
    public void ShowBrightness(int brightness) => Osd.ShowBrightness(brightness);
    public void ShowBattery(BatteryAlertKind kind, int percent) => Osd.ShowBattery(kind, percent);
    public void ShowBluetoothDevice(string name, bool connected, int? battery) => Osd.ShowBluetoothDevice(name, connected, battery);

    /// <summary>Çentiği açıp arama panelini gösterir. <paramref name="factory"/> yalnız ilk açılışta çalışır.</summary>
    public void OpenSearch(Func<SearchViewModel> factory, nint previousWindow, string? initialQuery)
    {
        if (_search is null)
        {
            _search = factory();
            _search.CloseRequested += restoreFocus => CloseSearch(restoreFocus);
            OnPropertyChanged(nameof(Search));
        }

        _windowBeforeSearch = previousWindow;
        _controller.Expand();
        _controller.HoldOpen();
        _search.Open(initialQuery);
        OnPropertyChanged(nameof(IsSearchOpen));
        UpdateActivePanels();
    }

    /// <summary>Panel açıkken sorguyu değiştirmeden odağı alır ya da verilen metni yazar.</summary>
    public void CloseSearch(bool restoreFocus)
    {
        if (!IsSearchOpen)
            return;

        _search!.Close();
        OnPropertyChanged(nameof(IsSearchOpen));
        _controller.ReleaseHold();
        if (!_controller.IsPinned)
            _controller.OnClick();
        UpdateActivePanels();

        SearchClosed?.Invoke(restoreFocus ? _windowBeforeSearch : 0);
    }

    public void ExpandToShelf()
    {
        _controller.Expand(NotchPanel.Shelf);
    }

    [RelayCommand]
    private void NextPanel() => _controller.NextPanel();

    [RelayCommand]
    private void SelectTab(NotchPanel panel) => _controller.GoToPanel(panel);

    /// <summary>Kapalı çentikteki Wi-Fi / Bluetooth simgesine tıklanınca çentik Kontrol sekmesinde açılır.</summary>
    [RelayCommand]
    private void OpenControlCenter() => _controller.Expand(NotchPanel.ControlCenter);

    /// <summary>"Çentiği açık tut" düğmesi.</summary>
    [RelayCommand]
    private void TogglePinned() => _controller.TogglePinned();

    [RelayCommand]
    private void PreviousPanel() => _controller.PreviousPanel();

    [RelayCommand]
    private void OpenSettings()
    {
        KoyWindow.ShowInstance(1);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        MaterialState.Changed -= OnMaterialChanged;
        StopClockTimer();
        _controller.StateChanged -= OnStateChanged;
        _controller.PanelChanged -= OnPanelChanged;
        _controller.EnabledPanelsChanged -= OnEnabledPanelsChanged;
        _controller.PanelTransitionTriggered -= OnPanelTransitionTriggered;
        _visibility.Changed -= OnVisibilityChanged;
        _music.TrackChanged -= OnTrackChanged;
        _music.PropertyChanged -= OnMusicPropertyChanged;
        _focus.TimerFinished -= OnFocusTimerFinished;
        _focus.PropertyChanged -= OnFocusPropertyChanged;
        _controller.PinnedChanged -= OnPinnedChanged;
        _status.PropertyChanged -= OnStatusPropertyChanged;
        Osd.Activated -= OnOsdActivated;
        Osd.Dismissed -= OnOsdStateChanged;
        Osd.Dispose();
        _controller.Dispose();
        _visibility.Dispose();
        _music.Dispose();
        _controlCenter.Dispose();
        _status.Dispose();
        _focus.Dispose();
        _shelf.Dispose();
        _calendar.Dispose();
        _clipboard?.Dispose();
        _search?.Dispose();
        Home.Dispose();
    }
}
