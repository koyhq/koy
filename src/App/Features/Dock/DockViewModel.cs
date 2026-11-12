using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Dock.Tray;
using Koy.App.Features.Settings;
using Koy.App.Resources;
using Koy.Core.Animation;
using Koy.Core.Appearance;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Dock.Items;
using Koy.Core.JumpList;
using Koy.Core.Media;
using Koy.Core.Settings;
using Koy.Core.Shell;
using Koy.Core.StartMenu;
using Koy.Core.Tray;
using Koy.Core.Visibility;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock penceresi için ana ViewModel.
/// Yüzen kapsül ve tam genişlik modlarını, yakınlık davranışlarını (DockDown, ReserveSpace, Dodge, AlwaysHidden)
/// ve 400 ms DWM pencere küçük resim önizlemesini yönetir.
/// </summary>
public sealed partial class DockViewModel : ObservableObject, IDisposable
{
    // Tek canlı ayar nesnesi: DockFeature.ApplySettings her uygulamada bütün ViewModel'lere aynı örneği verir (AdoptSettings).
    private AppSettings _settings;

    // Ek öğeler (ayırıcı, boşluk, saat, çöp kutusu, özel düğme). Hiç ek öğe yoksa bu alanlar boş kalır, dock değişmez.
    private IReadOnlyList<DockItemSpec> _layout = [];
    private IReadOnlyList<string> _effectiveOrder = [];
    private bool _hasExtras;
    private DispatcherTimer? _clockTimer;
    private readonly DockController _controller;
    private readonly IShellActionService _shellActionService;
    private readonly VisibilityController? _visibilityController;
    private readonly IMediaService? _mediaService;
    private ITrayService? _trayService;
    // Tepsi iş parçacığı okur, arayüz iş parçacığı yazar: kapalıyken simge olayları arayüzü uyandırmasın.
    private volatile bool _trayPopupOpen;
    private int _traySyncQueued;
    private DispatcherTimer? _trayCloseTimer;

    private readonly Spring _translateXSpring;
    private readonly Spring _translateYSpring;
    private readonly Spring _opacitySpring;
    private bool _isAnimating;
    private bool _disposed;

    // Pil tasarrufu politikası hareketi azaltmak istiyor mu (DockFeature güç servisinden besler).
    private bool _batteryReduceMotion;

    /// <summary>
    /// true iken dock kayma, solma ve büyüme yayları anında değer alır. Tek kaynak Motion: Animasyon hızı "Kapalı",
    /// Windows "animasyonları azalt" (Hareketi azalt ayarına uy açıksa) ve pil tasarrufu; bir de bu dock'un güç servisi.
    /// </summary>
    public bool ReduceMotion
    {
        get => _batteryReduceMotion || !Motion.Enabled;
        set => _batteryReduceMotion = value;
    }

    // Animasyon hızı ayarı (Sakin 1,5 / Hızlı 0,65): yay zamanı bununla bölünür. 0 ise ReduceMotion zaten anında atlar.
    private static double TimeScale => Motion.Scale > 0 ? 1.0 / Motion.Scale : 1.0;

    // Büyütme yayı: genel hareket ve "Dock büyütme" anahtarı birlikte (MotionPolicy.ScaleFor).
    private bool MagnifyAnimates => !ReduceMotion && Motion.EnabledFor(MotionPolicy.MotionKind.DockMagnify);

    private DisplayMonitor? _currentMonitor;
    private WindowRect _foregroundWindowBounds = WindowRect.Empty;
    private bool _isForegroundWindowMaximized;
    private bool _isForegroundWindowFullscreen;
    private bool _isMouseAtBottomEdge;
    private bool _isMouseOverDock;
    private bool _isMouseOverPopup;
    // Başlat / Arama / bildirim merkezi bu ekranda açık: dock, Windows görev çubuğu gibi görünür kalır.
    private bool _isShellFlyoutOpen;
    private double _lastContentWidth = 520.0;

    private readonly DispatcherTimer _thumbnailHoverTimer;
    private readonly DispatcherTimer _previewCloseTimer;
    private readonly DispatcherTimer _dodgeHoverTimer;
    private DockItemViewModel? _pendingPreviewItem;

    [ObservableProperty]
    private double _translationX;

    [ObservableProperty]
    private double _translationY;

    public DockEdge Edge => _settings.Dock.Edge;
    public bool IsVertical => Edge is DockEdge.Left or DockEdge.Right;
    public Orientation StripOrientation => IsVertical ? Orientation.Vertical : Orientation.Horizontal;
    public int LengthPercent => _settings.Dock.LengthPercent;
    public double EdgeOffsetDip => _settings.Dock.EdgeOffsetDip;
    public Thickness InnerPadding => new(DockLayoutLimits.ClampInnerPaddingDip(_settings.Dock.InnerPaddingDip));
    public Thickness SlotMargin
    {
        get
        {
            var spacing = DockLayoutLimits.ClampIconSpacingDip(_settings.Dock.IconSpacingDip);
            return IsVertical
                ? new Thickness(0, spacing / 2.0, 0, spacing / 2.0)
                : new Thickness(spacing / 2.0, 0, spacing / 2.0, 0);
        }
    }
    public PlacementMode PreviewPlacement => Edge switch
    {
        DockEdge.Top => PlacementMode.Bottom,
        DockEdge.Left => PlacementMode.Right,
        DockEdge.Right => PlacementMode.Left,
        _ => PlacementMode.Top
    };
    public double PreviewVerticalOffset => Edge switch
    {
        DockEdge.Top => 10,
        DockEdge.Bottom => -10,
        _ => 0
    };
    public double PreviewHorizontalOffset => Edge switch
    {
        DockEdge.Left => 10,
        DockEdge.Right => -10,
        _ => 0
    };

    public double SeparatorWidth => IsVertical ? 24.0 : 1.0;
    public double SeparatorHeight => IsVertical ? 1.0 : 24.0;
    public Thickness SeparatorMargin => IsVertical ? new Thickness(0, 4, 0, 4) : new Thickness(4, 0, 4, 0);

    public double DodgeHandleWidth => IsVertical ? DockPositionResolver.DodgeHandleHeightDip : DockPositionResolver.DodgeHandleWidthDip;
    public double DodgeHandleHeight => IsVertical ? DockPositionResolver.DodgeHandleWidthDip : DockPositionResolver.DodgeHandleHeightDip;
    public HorizontalAlignment DodgeHandleHorizontalAlignment => Edge switch
    {
        DockEdge.Left => HorizontalAlignment.Left,
        DockEdge.Right => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Center
    };
    public VerticalAlignment DodgeHandleVerticalAlignment => Edge switch
    {
        DockEdge.Top => VerticalAlignment.Top,
        DockEdge.Bottom => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Center
    };
    public Thickness DodgeHandleMargin => Edge switch
    {
        DockEdge.Top => new Thickness(0, 4, 0, 0),
        DockEdge.Left => new Thickness(4, 0, 0, 0),
        DockEdge.Right => new Thickness(0, 0, 4, 0),
        _ => new Thickness(0, 0, 0, 4)
    };

    public Thickness FullWidthBorderThickness => Edge switch
    {
        DockEdge.Top => new Thickness(0, 0, 0, 1),
        DockEdge.Left => new Thickness(0, 0, 1, 0),
        DockEdge.Right => new Thickness(1, 0, 0, 0),
        _ => new Thickness(0, 1, 0, 0)
    };
    public Thickness FullWidthPadding => IsVertical ? new Thickness(0, 12, 0, 12) : new Thickness(12, 0, 12, 0);

    [ObservableProperty]
    private double _opacity = 1.0;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _magnificationEnabled;

    // Düzen ve yakınlık modu durumu
    [ObservableProperty]
    private bool _isFullWidthMode;

    [ObservableProperty]
    private bool _isSnapped;

    [ObservableProperty]
    private bool _isHandleVisible;

    /// <summary>Gizli dock'un altındaki ince çizgi (ayar açık, dock gizli, tam ekran yok). Anında değişir, animasyonsuzdur.</summary>
    [ObservableProperty]
    private bool _isEdgeLineVisible;

    [ObservableProperty]
    private double _dockHeight = 60.0;

    [ObservableProperty]
    private double _capsuleWidth = double.NaN;

    [ObservableProperty]
    private double _capsuleHeight = 60.0;

    [ObservableProperty]
    private Thickness _dockMargin = new(12, 12, 12, 10);

    [ObservableProperty]
    private CornerRadius _capsuleCornerRadius = new(18);

    [ObservableProperty]
    private bool _showStartButton = true;

    [ObservableProperty]
    private bool _showFolders = true;

    [ObservableProperty]
    private bool _showBadges = true;

    private readonly DockPresentationStyle _presentation;

    // Sistem tepsisi: simgeler yalnız pencere açıkken eşitlenir ve çözülür (boştayken iş yok).
    [ObservableProperty]
    private bool _isTrayPopupOpen;

    [ObservableProperty]
    private bool _hasTrayIcons;

    /// <summary>Tepsi düğmesi yalnız tepsi penceresi gerçekten çalışıyorsa görünür (başlatma başarısızsa gizli).</summary>
    public bool HasTray => _trayService is { IsRunning: true };

    // Sağ bölümün neyi göstereceği Core'daki kuraldan gelir (kapsül ve tam genişlik aynısını kullanır).
    private DockRightSection RightSection =>
        DockRightSection.Resolve(ShowFolders, ShowRecents, HasTray, _settings.Dock.SmallTray);

    /// <summary>Uygulamalarla Koy menüsü arasındaki ilk ayırıcı: yalnız ortada gösterilecek bir düğme varsa görünür.</summary>
    public bool ShowRightSection => RightSection.ShowFirstSeparator;

    /// <summary>Tam boy tepsi düğmesi (Küçük tepsi kapalıyken, ayırıcıların arasında).</summary>
    public bool ShowFullTray => RightSection.ShowFullTray;

    /// <summary>Dar ok biçimindeki tepsi düğmesi (Küçük tepsi açıkken, Koy menüsü düğmesinin sağında).</summary>
    public bool ShowSmallTray => RightSection.ShowSmallTray;

    /// <summary>Küçük tepsi okunun genişliği (DIP).</summary>
    public double SmallTrayWidth => DockRightSection.SmallTrayWidthDip;

    // Son kullanılanlar: yığın düğmesi; liste yalnız düğmeye tıklanınca okunur (sürekli izleme yok), kapanınca bırakılır.
    private readonly IRecentFilesService? _recentFiles;

    /// <summary>Uygulama simgesinin sağ tık menüsündeki atlama listesi; yalnız menü açılınca okunur.</summary>
    public IJumpListService? JumpLists { get; }

    /// <summary>Başlat düğmesinin sağ tık menüsü (Win+X karşılığı); yoksa sağ tık bir şey açmaz.</summary>
    public StartMenuActions? StartContextMenu { get; }

    /// <summary>Son kullanılanlar düğmesi görünür mü (ayar açık ve servis var).</summary>
    public bool ShowRecents => _settings.Dock.ShowRecents && _recentFiles is not null;

    [ObservableProperty]
    private bool _isRecentsOpen;

    public ObservableCollection<RecentFile> RecentFiles { get; } = [];

    public bool HasRecentFiles => RecentFiles.Count > 0;

    [RelayCommand]
    private async Task ToggleRecentsAsync()
    {
        if (_recentFiles is null)
            return;

        if (IsRecentsOpen)
        {
            IsRecentsOpen = false;
            return;
        }

        ClosePreviewNow();

        IReadOnlyList<RecentFile> items;
        try
        {
            items = await Task.Run(() => _recentFiles.GetRecent());
        }
        catch
        {
            items = [];
        }

        RecentFiles.Clear();
        foreach (var item in items)
            RecentFiles.Add(item);

        OnPropertyChanged(nameof(HasRecentFiles));
        IsRecentsOpen = true;
    }

    /// <summary>Listeden bir dosya seçildi: Windows ilişkili uygulamayla açar, liste kapanır.</summary>
    public void OpenRecent(RecentFile file)
    {
        IsRecentsOpen = false;
        _shellActionService.Launch(file.OpenPath);
    }

    partial void OnIsRecentsOpenChanged(bool value)
    {
        if (!value)
        {
            RecentFiles.Clear();
            OnPropertyChanged(nameof(HasRecentFiles));
        }

        EvaluatePosition(_lastContentWidth);
    }

    public ObservableCollection<TrayIconViewModel> TrayIcons { get; } = [];

    // Tam Genişlik Modu: Çalan müzik hapı (Sol bölüm)
    [ObservableProperty]
    private string _nowPlayingTitle = Strings.NoMediaPlaying;

    [ObservableProperty]
    private string _nowPlayingArtist = "Koy";

    [ObservableProperty]
    private ImageSource? _nowPlayingCover;

    [ObservableProperty]
    private bool _hasNowPlaying;

    [ObservableProperty]
    private double _iconSize = 28.0;

    [ObservableProperty]
    private double _itemSize = 38.0;

    /// <summary>
    /// Tam genişlik çubuğun yüzeyi: etkin malzemenin opaklığında (Katı opak, Buzlu %86, Cam %72; okunabilirlik
    /// koruması dahil). Dock penceresi katmanlı (AllowsTransparency) olduğundan DWM akrilik uygulanamaz; bulanıklık yok.
    /// </summary>
    [ObservableProperty]
    private Brush _fullWidthSurface = Brushes.Black;

    /// <summary>Yüzen kapsülün yüzeyi: Katı'da bugünkü %90 (#E618181B), saydam seviyede o seviyenin opaklığı.</summary>
    [ObservableProperty]
    private Brush _capsuleSurface = Brushes.Black;

    private readonly AppSettings _materialSettings;

    private void UpdateMaterialSurfaces()
    {
        var level = MaterialState.ForDock(_materialSettings);
        var full = new SolidColorBrush(Color.FromArgb(MaterialSetting.LayeredAlpha(level), 0x12, 0x12, 0x16));
        full.Freeze();
        FullWidthSurface = full;

        var capsuleAlpha = Math.Min((byte)0xE6, MaterialSetting.LayeredAlpha(level)); // kapsül Katı'da da %90; daha opak olmaz
        var capsule = new SolidColorBrush(Color.FromArgb(capsuleAlpha, 0x18, 0x18, 0x1B));
        capsule.Freeze();
        CapsuleSurface = capsule;
    }

    private void OnMaterialChanged() => UpdateMaterialSurfaces();

    /// <summary>Simgeler en küçük boyutta bile sığmıyor: şerit sınırında kırpılır (aksi halde yan bölümlere taşardı).</summary>
    [ObservableProperty]
    private bool _isStripClipped;

    /// <summary>Uygulama şeridinin en çok genişliği: simgeler en küçük boyutta bile sığmazsa şerit bu sınırda kırpılır.</summary>
    [ObservableProperty]
    private double _itemsMaxWidth = double.PositiveInfinity;

    // Ayardaki simge boyutu; ekrana sığmayınca IconSize bundan küçülür, yer açılınca geri büyür.
    private double _preferredIconSize = 28.0;

    // Tam genişlikte orta sütunun gerçek genişliği (görünüm bildirir); kapsülde kullanılmaz.
    private double _centerWidth;

    // Kapsülün ölçülmüş sabit payları (DIP): sol/sağ pencere kenar boşluğu, iç dolgu + çerçeve, ayırıcı (1 + 4 + 4).
    private const double CapsuleWindowSideMargins = 24.0;
    private const double CapsulePaddingAndBorder = 18.0;
    private const double CapsuleSeparatorWidth = 9.0;

    // DWM Canlı Önizleme Açılır Penceresi
    [ObservableProperty]
    private bool _isPreviewPopupOpen;

    [ObservableProperty]
    private DockItemViewModel? _previewItem;

    [ObservableProperty]
    private UIElement? _previewPlacementTarget;

    public ObservableCollection<DockEntryViewModel> Items { get; } = [];

    /// <summary>Dock gizli ve kenardan açılabilir: kenar zamanlayıcısı çalışmalı.</summary>
    public bool NeedsEdgeWatch { get; private set; }

    public event EventHandler? EdgeWatchChanged;
    public event EventHandler? AnimationNeeded;
    public event EventHandler? AnimationStopped;
    public event Action? RequestReposition;

    /// <summary>Stile göre geçerli yakınlık modu: tam genişlikte yalnız Yer ayır (hep göster) ve Hep gizli (otomatik gizle).</summary>
    public DockProximityMode EffectiveProximity =>
        DockProximityMapping.Effective(_settings.Dock.Style, _settings.Dock.Proximity);

    /// <summary>Tam genişlikte ikon şeridinin yeri. Ayar değişince ViewModel yeniden kurulur (DockSettingsDiff), bildirim gerekmez.</summary>
    public HorizontalAlignment StripAlignment => _settings.Dock.IconAlignment switch
    {
        DockIconAlignment.Left => HorizontalAlignment.Left,
        DockIconAlignment.Right => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Center
    };

    /// <summary>Dikey tam genişlikte ikon şeridinin dikey yeri.</summary>
    public VerticalAlignment VerticalStripAlignment => _settings.Dock.IconAlignment switch
    {
        DockIconAlignment.Left => VerticalAlignment.Top,
        DockIconAlignment.Right => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Center
    };

    /// <summary>Başlat düğmesinin simgesi. Ayar değişince ViewModel yeniden kurulur (DockSettingsDiff), bildirim gerekmez.</summary>
    public ImageSource? StartIcon { get; }

    public DockController Controller => _controller;
    public AppSettings Settings => _settings;

    // Simge paketi ve uygulama başına değişim. Ayar değişince ViewModel yeniden kurulur (DockSettingsDiff), örnek de yenilenir.
    private readonly DockAppIcons _appIcons;

    private void AttachAppIcon(DockItemViewModel vm)
    {
        vm.IconOwner = this;
        if (!_appIcons.IsActive)
            return;

        var custom = _appIcons.Resolve(vm.Model);
        var hasOverride = _appIcons.HasOverride(vm.Model);
        if (custom is not null || hasOverride)
            vm.ApplyAppIcon(custom, hasOverride);
    }

    /// <summary>"Simgeyi değiştir...": PNG/ICO seçtirir, doğrulanmış yolu bütün anahtarlarıyla kaydeder, dock yeniden kurulur.</summary>
    public void ChangeAppIcon(DockItemViewModel item)
    {
        var keys = AppIconKey.All(item.Model.AppUserModelId, item.Model.Id);
        if (keys.Count == 0)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Get("DockItemIconPickTitle"),
            Filter = Strings.Get("DockItemIconPickFilter"),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        // Dosya, ayara yazılmadan önce gerçekten çözülebiliyor olmalı; bozuk dosya sessizce yok sayılır.
        if (DockStartIconLoader.LoadFile(dialog.FileName) is null)
            return;

        var overrides = new ValueDictionary<string>(_settings.Appearance.AppIconOverrides);
        var accepted = false;
        foreach (var key in keys)
            accepted |= AppIconOverrides.TrySet(overrides, key, dialog.FileName);

        if (accepted)
            SaveAppIconOverrides(overrides);
    }

    /// <summary>"Simgeyi sıfırla": öğenin bütün anahtarlarındaki değişimi siler.</summary>
    public void ResetAppIcon(DockItemViewModel item)
    {
        var overrides = new ValueDictionary<string>(_settings.Appearance.AppIconOverrides);
        foreach (var key in AppIconKey.All(item.Model.AppUserModelId, item.Model.Id))
            AppIconOverrides.Reset(overrides, key);

        SaveAppIconOverrides(overrides);
    }

    // Bellekteki ayar bilerek değiştirilmez: ReloadDock eski ve yeni ayarı karşılaştırıp (DockSettingsDiff) dock'u kurar.
    private static void SaveAppIconOverrides(ValueDictionary<string> overrides)
    {
        DockFeature.SaveAppIconOverrides(overrides);
        (Application.Current as App)?.ReloadDock();
    }

    /// <summary>Ayar nesnesini değiştirir (yerinde ayar uygulaması): sabit sırası/klasör yazımları ile okuyucular aynı nesneyi görür.</summary>
    public void AdoptSettings(AppSettings settings)
    {
        _settings = settings;
        OnPropertyChanged(nameof(Edge));
        OnPropertyChanged(nameof(IsVertical));
        OnPropertyChanged(nameof(StripOrientation));
        OnPropertyChanged(nameof(LengthPercent));
        OnPropertyChanged(nameof(EdgeOffsetDip));
        OnPropertyChanged(nameof(InnerPadding));
        OnPropertyChanged(nameof(SlotMargin));
        OnPropertyChanged(nameof(PreviewPlacement));
        OnPropertyChanged(nameof(PreviewVerticalOffset));
        OnPropertyChanged(nameof(PreviewHorizontalOffset));
        OnPropertyChanged(nameof(SeparatorWidth));
        OnPropertyChanged(nameof(SeparatorHeight));
        OnPropertyChanged(nameof(SeparatorMargin));
        OnPropertyChanged(nameof(DodgeHandleWidth));
        OnPropertyChanged(nameof(DodgeHandleHeight));
        OnPropertyChanged(nameof(DodgeHandleHorizontalAlignment));
        OnPropertyChanged(nameof(DodgeHandleVerticalAlignment));
        OnPropertyChanged(nameof(DodgeHandleMargin));
        OnPropertyChanged(nameof(FullWidthBorderThickness));
        OnPropertyChanged(nameof(FullWidthPadding));
        OnPropertyChanged(nameof(StripAlignment));
        OnPropertyChanged(nameof(VerticalStripAlignment));
        foreach (var item in Items)
            item.IsVertical = IsVertical;
        RecomputeFit();
        EvaluatePosition(_lastContentWidth);
    }

    // Bu dock'un bulunduğu monitörün sınırları (piksel). Yalnız "yalnız bu monitörün pencereleri" ayarında kullanılır.
    private WindowRect? _monitorBounds;

    /// <summary>Dock'un monitörü değişti (ekran düzeni): süzgeç yeni sınırlarla yeniden uygulanır.</summary>
    public void SetMonitorBounds(WindowRect bounds)
    {
        if (_monitorBounds == bounds)
            return;

        _monitorBounds = bounds;
        if (_settings.Dock.WindowMonitorScope == DockWindowMonitorScope.CurrentMonitor)
            UpdateItems();
    }

    private IReadOnlyList<DockItem> VisibleModels() =>
        _settings.Dock.WindowMonitorScope == DockWindowMonitorScope.CurrentMonitor && _monitorBounds is { } bounds
            ? DockMonitorFilter.Apply(_controller.Items, bounds, _controller.ForegroundHandle)
            : _controller.Items;
    public DockStatusViewModel? Status { get; }

    public DockViewModel(
        AppSettings settings,
        DockController controller,
        IShellActionService shellActionService,
        VisibilityController? visibilityController = null,
        DockStatusViewModel? status = null,
        IMediaService? mediaService = null,
        ITrayService? trayService = null,
        IRecentFilesService? recentFiles = null,
        IJumpListService? jumpLists = null,
        StartMenuActions? startMenu = null)
    {
        _settings = settings;
        _appIcons = new DockAppIcons(settings);
        _recentFiles = recentFiles;
        JumpLists = jumpLists;
        StartContextMenu = startMenu;
        Status = status;
        _controller = controller;
        _shellActionService = shellActionService;
        _visibilityController = visibilityController;
        _mediaService = mediaService;
        _trayService = trayService;

        _iconSize = settings.Dock.IconSize > 0 ? settings.Dock.IconSize : 28.0;
        _preferredIconSize = _iconSize;
        _itemSize = DockFitMath.ItemSizeFor(_iconSize);

        _magnificationEnabled = settings.Dock.HoverMagnify;

        _isFullWidthMode = settings.Dock.Style == DockStyleMode.FullWidth;
        _showStartButton = settings.Dock.ShowStartButton;
        // Düğme gizliyken dosya okunmaz.
        StartIcon = settings.Dock.ShowStartButton ? DockStartIconLoader.Load(settings) : null;
        _showFolders = settings.Dock.ShowFolders;
        _showBadges = settings.Dock.ShowBadges;
        _presentation = DockPresentationStyle.From(settings.Dock);

        _materialSettings = settings;
        UpdateMaterialSurfaces();
        MaterialState.Changed += OnMaterialChanged;

        _translateXSpring = new Spring(0) { Stiffness = 280, Damping = 25 };
        _translateYSpring = new Spring(0) { Stiffness = 280, Damping = 25 };
        _opacitySpring = new Spring(1.0) { Stiffness = 200, Damping = 24 };

        _thumbnailHoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(50, settings.Dock.ThumbnailDelayMs))
        };
        _thumbnailHoverTimer.Tick += OnThumbnailTimerTick;

        _previewCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _previewCloseTimer.Tick += OnPreviewCloseTimerTick;

        _dodgeHoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(50, settings.Dock.DodgeDelayMs))
        };
        _dodgeHoverTimer.Tick += OnDodgeHoverTimerTick;

        _controller.ItemsChanged += OnControllerItemsChanged;
        _controller.FullscreenStateChanged += OnFullscreenStateChanged;

        if (_visibilityController is not null)
        {
            _visibilityController.Changed += OnVisibilityChanged;
        }

        if (_mediaService is not null)
        {
            _mediaService.Changed += OnMediaChanged;
            UpdateMediaInfo(_mediaService.Current);
        }

        DockContextMenuManager.ActiveMenuChanged += OnActiveMenuChanged;

        if (_trayService is not null)
        {
            _trayService.IconsChanged += OnTrayIconsChanged;
        }

        UpdateItems();
    }

    /// <summary>
    /// Tepsi servisini takar ya da çıkarır (görev çubuğu gizlenince / geri gelince). Arayüz iş parçacığında çağrılır.
    /// Çıkarılınca açık pencere kapanır ve simge görüntüleri bırakılır.
    /// </summary>
    public void SetTrayService(ITrayService? service)
    {
        if (ReferenceEquals(_trayService, service))
            return;

        if (_trayService is not null)
            _trayService.IconsChanged -= OnTrayIconsChanged;

        _trayService = service;

        if (service is not null)
            service.IconsChanged += OnTrayIconsChanged;

        IsTrayPopupOpen = false;
        TrayIcons.Clear();
        HasTrayIcons = false;
        OnPropertyChanged(nameof(HasTray));
        OnPropertyChanged(nameof(ShowRightSection));
        OnPropertyChanged(nameof(ShowFullTray));
        OnPropertyChanged(nameof(ShowSmallTray));
        RecomputeFit();
    }

    [RelayCommand]
    private void ToggleTray()
    {
        if (_trayService is null)
            return;

        if (IsTrayPopupOpen)
        {
            IsTrayPopupOpen = false;
            return;
        }

        ClosePreviewNow();
        _trayService.RemoveDeadIcons();
        SyncTrayIcons();
        IsTrayPopupOpen = true;
    }

    /// <summary>Tepsi simgesine tıklandı: uygulamaya Windows görev çubuğunun göndereceği mesajları iletir.</summary>
    public void SendTrayMouse(TrayIconViewModel icon, TrayMouseAction action, WindowRect dockRect = default)
    {
        if (_trayService is null)
            return;

        CursorHelper.GetPosition(out var x, out var y);
        // Çıpa dock'un üzerine düşerse uygulamanın menüsü dock'un içine açılırdı: dock'un üst kenarının üstüne çekilir.
        (x, y) = TrayMenuAnchor.ClampAboveDock(x, y, dockRect);
        _trayService.SendMouse(icon.Key, action, x, y);

        if (action == TrayMouseAction.LeftClick)
        {
            // Bazı uygulamalar çift tıklama bekler; pencere çift tıklama süresi kadar açık kalır.
            _trayCloseTimer ??= CreateTrayCloseTimer();
            _trayCloseTimer.Stop();
            _trayCloseTimer.Start();
        }
        else if (action != TrayMouseAction.RightClick)
        {
            // Çift tık uygulamanın penceresini açar; tepsi penceresi önünü kapatmasın.
            // Sağ tıkta pencere AÇIK kalır: menü açıkken görünür, seçim ya da dış tıklamayla kapanır
            // (kapatma kararı TrayPopupDismissPolicy'de, yoklama TrayButton'da).
            IsTrayPopupOpen = false;
        }
    }

    private DispatcherTimer CreateTrayCloseTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CursorHelper.DoubleClickTimeMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            IsTrayPopupOpen = false;
        };
        return timer;
    }

    partial void OnIsTrayPopupOpenChanged(bool value)
    {
        _trayPopupOpen = value;
        if (value)
        {
            _trayClearTimer?.Stop();
        }
        else
        {
            _trayCloseTimer?.Stop();
            // Kapanış animasyonu süresince simgeler görünür kalır; sonra bırakılır (kapalıyken bellekte tutulmaz).
            _trayClearTimer ??= CreateTrayClearTimer();
            _trayClearTimer.Stop();
            _trayClearTimer.Start();
        }

        EvaluatePosition(_lastContentWidth);
    }

    private DispatcherTimer? _trayClearTimer;

    private DispatcherTimer CreateTrayClearTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!IsTrayPopupOpen)
            {
                TrayIcons.Clear();
                _trayExeByOwner.Clear();
            }
        };
        return timer;
    }

    private void OnTrayIconsChanged(object? sender, EventArgs e)
    {
        // Tepsi iş parçacığından gelir. Pencere kapalıyken arayüze hiç dönülmez: açılırken liste zaten baştan okunur.
        if (!_trayPopupOpen)
            return;

        // Sık değişen simgelerde (animasyonlu ipucu, rozet) arka arkaya iş biriktirme: bekleyen tek bir eşitleme yeter.
        if (Interlocked.Exchange(ref _traySyncQueued, 1) == 1)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            Volatile.Write(ref _traySyncQueued, 0);
            return;
        }

        dispatcher.InvokeAsync(() =>
        {
            Volatile.Write(ref _traySyncQueued, 0);
            if (IsTrayPopupOpen)
                SyncTrayIcons();
        }, DispatcherPriority.Background);
    }

    private void SyncTrayIcons()
    {
        if (_trayService is null)
            return;

        // Uygulama başına görünürlük (issue #77): Gizli olanlar tepsi penceresine hiç girmez. Exe adı sahip pencere
        // başına bir kez okunur; pencere kapanınca önbellek simgelerle birlikte bırakılır.
        var perApp = _settings.Dock.TrayAppVisibility ?? new ValueDictionary<TrayIconVisibility>();
        var newDefault = _settings.Dock.NewTrayIconVisibility;
        var models = _trayService.GetVisibleIcons()
            .Where(m => TrayIconVisibilityPolicy.Resolve(TrayExeName(m.OwnerHwnd), perApp, newDefault) != TrayIconVisibility.Hidden)
            .ToList();

        for (var i = TrayIcons.Count - 1; i >= 0; i--)
        {
            var key = TrayIcons[i].Key;
            if (!models.Any(m => m.Key == key))
                TrayIcons.RemoveAt(i);
        }

        for (var targetIndex = 0; targetIndex < models.Count; targetIndex++)
        {
            var model = models[targetIndex];
            var existingIndex = -1;
            for (var j = 0; j < TrayIcons.Count; j++)
            {
                if (TrayIcons[j].Key == model.Key)
                {
                    existingIndex = j;
                    break;
                }
            }

            TrayIconViewModel vm;
            if (existingIndex >= 0)
            {
                vm = TrayIcons[existingIndex];
                if (existingIndex != targetIndex)
                    TrayIcons.Move(existingIndex, targetIndex);
            }
            else
            {
                vm = new TrayIconViewModel(model, TrayExeName(model.OwnerHwnd));
                TrayIcons.Insert(targetIndex, vm);
            }

            vm.Update(model, _trayService.GetIconImage);
        }

        HasTrayIcons = TrayIcons.Count > 0;
    }

    private readonly Dictionary<nint, string> _trayExeByOwner = [];

    private string TrayExeName(nint ownerHwnd)
    {
        if (!_trayExeByOwner.TryGetValue(ownerHwnd, out var name))
        {
            name = TrayAppNames.ForOwner(ownerHwnd);
            _trayExeByOwner[ownerHwnd] = name;
        }

        return name;
    }

    [RelayCommand]
    private void StartMenu()
    {
        _shellActionService.SendWinKey();
    }

    [RelayCommand]
    private void SystemTray()
    {
        _shellActionService.ToggleSystemTray();
    }

    public static Action? OpenKoyMenuHandler { get; set; }

    [RelayCommand]
    private void OpenKoyMenu()
    {
        // Sağ alt Koy menüsü veya ayarlar
        if (OpenKoyMenuHandler is not null)
        {
            OpenKoyMenuHandler();
            return;
        }
        OpenSettings();
    }

    [RelayCommand]
    private static void OpenDownloads()
    {
        try
        {
            var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloadsPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadsPath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // İhmal et
        }
    }

    [RelayCommand]
    private void OpenNotchMusic()
    {
        // Çentik müzik paneli kısayolu
        _visibilityController?.OnMediaActivity();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        KoyWindow.ShowInstance(2);
    }

    public void SetMonitor(DisplayMonitor? monitor)
    {
        _currentMonitor = monitor;
        RecomputeFit();
        EvaluatePosition(_lastContentWidth);
    }

    /// <summary>Tam genişlikte orta sütunun genişliği değişti (görünüm bildirir): simgeler sığacak biçimde yeniden boyutlanır.</summary>
    public void ReportCenterWidth(double width)
    {
        if (Math.Abs(width - _centerWidth) < 0.5)
            return;

        _centerWidth = width;
        RecomputeFit();
    }

    /// <summary>
    /// Çok uygulama açıkken simgeleri ekrana sığdırır (Core: DockFitMath). Kapsülde bütün yuvalar (Başlat, sağ düğmeler dahil)
    /// ItemSize ile ölçeklendiğinden hepsi hesaba girer; tam genişlikte yalnız orta sütundaki uygulamalar.
    /// En küçük simge 24 px; ondan sonra şerit kırpılır.
    /// </summary>
    private void RecomputeFit()
    {
        double icon;
        double maxWidth;
        var spacing = _settings.Dock.IconSpacingDip;
        var isVertical = IsVertical;

        if (_settings.Dock.Style == DockStyleMode.FullWidth)
        {
            if (_centerWidth <= 0)
                return;

            icon = DockFitMath.FitIconSize(_preferredIconSize, Items.Count, 0, _centerWidth, spacing);
            maxWidth = _centerWidth;
        }
        else
        {
            var monitorDimension = isVertical
                ? (_currentMonitor?.HeightDip > 0 ? _currentMonitor.HeightDip : 1080.0)
                : (_currentMonitor?.WidthDip > 0 ? _currentMonitor.WidthDip : 1920.0);
            var available = monitorDimension - CapsuleWindowSideMargins;
            var right = RightSection;
            var otherSlots = (ShowStartButton ? 1 : 0) + (ShowFolders ? 1 : 0) + (ShowRecents ? 1 : 0)
                             + (right.ShowFullTray ? 1 : 0) + 1; // +1: Koy menüsü
            var fixedWidth = CapsulePaddingAndBorder + CapsuleSeparatorWidth
                             + (right.ShowFirstSeparator ? CapsuleSeparatorWidth : 0.0)
                             + (right.ShowSmallTray ? DockRightSection.SmallTrayWidthDip + 1.0 : 0.0);

            icon = DockFitMath.FitIconSize(_preferredIconSize, Items.Count + otherSlots, fixedWidth, available, spacing);
            maxWidth = DockFitMath.StripMaxWidth(available, fixedWidth + otherSlots * DockFitMath.SlotWidth(icon, spacing));
        }

        ItemsMaxWidth = maxWidth;
        IsStripClipped = Items.Count * DockFitMath.SlotWidth(icon, spacing) > maxWidth + 0.5;

        if (Math.Abs(icon - IconSize) < 0.01)
            return;

        IconSize = icon;
        ItemSize = DockFitMath.ItemSizeFor(icon);
        foreach (var entry in Items)
        {
            entry.IconSize = IconSize;
            entry.ItemSize = ItemSize;
        }
    }

    public void UpdateForegroundWindow(WindowRect bounds, bool isMaximized, bool isFullscreen = false)
    {
        _foregroundWindowBounds = bounds;
        _isForegroundWindowMaximized = isMaximized;
        _isForegroundWindowFullscreen = isFullscreen;
        EvaluatePosition(_lastContentWidth);
    }

    public void SetMouseAtBottom(bool atBottom)
    {
        if (_isMouseAtBottomEdge == atBottom)
            return;

        _isMouseAtBottomEdge = atBottom;

        if (atBottom && EffectiveProximity == DockProximityMode.Dodge)
        {
            _dodgeHoverTimer.Start();
        }
        else
        {
            _dodgeHoverTimer.Stop();
            EvaluatePosition(_lastContentWidth);
        }
    }

    public void SetShellFlyoutOpen(bool open)
    {
        if (_isShellFlyoutOpen == open)
            return;

        _isShellFlyoutOpen = open;
        EvaluatePosition(_lastContentWidth);
    }

    public void SetMouseOverDock(bool mouseOver)
    {
        _isMouseOverDock = mouseOver;
        EvaluatePosition(_lastContentWidth);
    }

    private void OnDodgeHoverTimerTick(object? sender, EventArgs e)
    {
        _dodgeHoverTimer.Stop();
        EvaluatePosition(_lastContentWidth);
    }

    // Aşağı yapış'ta yüzen ↔ yapışık geçişi 150 ms'den kısa sürerse uygulanmaz (Alt+Tab ara durakları dock'u zıplatıyordu).
    private readonly SnapDebounce _snapDebounce = new(TimeSpan.FromMilliseconds(150));
    private DockResolvedState? _stableResolved;
    private DispatcherTimer? _snapTimer;

    private static TimeSpan SnapClock => TimeSpan.FromSeconds(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);

    private DockResolvedState StabilizeSnap(DockResolvedState resolved)
    {
        if (resolved.IsFullWidth || resolved.IsHidden || EffectiveProximity != DockProximityMode.DockDown || _stableResolved is null)
        {
            _snapDebounce.Reset(resolved.IsSnapped);
            _snapTimer?.Stop();
            if (!resolved.IsHidden)
                _stableResolved = resolved;
            return resolved;
        }

        var effective = _snapDebounce.Update(resolved.IsSnapped, SnapClock);
        if (effective == resolved.IsSnapped)
        {
            _snapTimer?.Stop();
            _stableResolved = resolved;
            return resolved;
        }

        // Değişim bekliyor: önceki geometri korunur; kalan süre sonra tek atımlık zamanlayıcı yeniden değerlendirir.
        _snapTimer ??= new DispatcherTimer();
        _snapTimer.Stop();
        _snapTimer.Interval = _snapDebounce.Remaining(SnapClock) + TimeSpan.FromMilliseconds(10);
        _snapTimer.Tick -= OnSnapTimerTick;
        _snapTimer.Tick += OnSnapTimerTick;
        _snapTimer.Start();

        var s = _stableResolved;
        return resolved with
        {
            IsSnapped = s.IsSnapped,
            HeightDip = s.HeightDip,
            TopDip = s.TopDip,
            CornerRadiusTopLeft = s.CornerRadiusTopLeft,
            CornerRadiusTopRight = s.CornerRadiusTopRight,
            CornerRadiusBottomRight = s.CornerRadiusBottomRight,
            CornerRadiusBottomLeft = s.CornerRadiusBottomLeft
        };
    }

    private void OnSnapTimerTick(object? sender, EventArgs e)
    {
        _snapTimer?.Stop();
        if (!_disposed)
            EvaluatePosition(_lastContentWidth);
    }

    public void EvaluatePosition(double contentWidth)
    {
        if (contentWidth > 0)
            _lastContentWidth = contentWidth;

        var hasOpenFolder = Items.OfType<DockFolderViewModel>().Any(f => f.IsFolderPanelOpen);
        var effectiveMouseOver = _isMouseOverDock || _isMouseOverPopup || _isShellFlyoutOpen || IsPreviewPopupOpen || IsTrayPopupOpen || IsRecentsOpen || DockContextMenuManager.HasActiveMenu || hasOpenFolder;

        var resolved = DockPositionResolver.Resolve(
            _currentMonitor,
            _settings.Dock.Style,
            EffectiveProximity,
            _lastContentWidth,
            _foregroundWindowBounds,
            _isForegroundWindowMaximized,
            _isMouseAtBottomEdge,
            effectiveMouseOver,
            _isForegroundWindowFullscreen,
            _settings.Dock.HideInFullscreen,
            _settings.Dock.EdgeReveal,
            _settings.Dock.Edge,
            _settings.Dock.LengthPercent,
            _settings.Dock.EdgeOffsetDip,
            _settings.Dock.InnerPaddingDip);

        resolved = StabilizeSnap(resolved);

        IsFullWidthMode = resolved.IsFullWidth;
        IsSnapped = resolved.IsSnapped;
        IsHandleVisible = resolved.IsHandleVisible;
        DockHeight = IsVertical ? resolved.WidthDip : resolved.HeightDip;
        CapsuleWidth = IsVertical ? DockHeight : double.NaN;
        CapsuleHeight = IsVertical ? double.NaN : DockHeight;

        if (resolved.IsFullWidth)
        {
            DockMargin = new Thickness(0);
            CapsuleCornerRadius = new CornerRadius(0);
        }
        else if (resolved.IsSnapped)
        {
            DockMargin = Edge switch
            {
                DockEdge.Top => new Thickness(12, 0, 12, 12),
                DockEdge.Left => new Thickness(0, 12, 12, 12),
                DockEdge.Right => new Thickness(12, 12, 0, 12),
                _ => new Thickness(12, 12, 12, 0)
            };
            CapsuleCornerRadius = new CornerRadius(
                resolved.CornerRadiusTopLeft,
                resolved.CornerRadiusTopRight,
                resolved.CornerRadiusBottomRight,
                resolved.CornerRadiusBottomLeft);
        }
        else
        {
            DockMargin = Edge switch
            {
                DockEdge.Top => new Thickness(12, 10, 12, 12),
                DockEdge.Left => new Thickness(10, 12, 12, 12),
                DockEdge.Right => new Thickness(12, 12, 10, 12),
                _ => new Thickness(12, 12, 12, 10)
            };
            CapsuleCornerRadius = new CornerRadius(18);
        }

        // Tam ekran bu monitör için hesaplanır (yan ekrandaki oyun bu ekrandaki dock'u gizlemez; denetleyicinin
        // sistem geneli bayrağı bilerek kullanılmaz).
        var isBlockedByFullscreen = _settings.Dock.HideInFullscreen && _isForegroundWindowFullscreen;
        var shouldShow = !resolved.IsHidden && !isBlockedByFullscreen;
        if (_visibilityController is not null && EffectiveProximity != DockProximityMode.ReserveSpace)
        {
            shouldShow = shouldShow && _visibilityController.IsVisible;
        }

        IsVisible = shouldShow;
        // Çekil tutamağı zaten gizli dock'u işaret eder; ikisi üst üste çizilmez.
        IsEdgeLineVisible = !resolved.IsHandleVisible && DockPresentationMath.ShowsHiddenEdgeLine(
            _settings.Dock.HiddenEdgeLine, !shouldShow, isBlockedByFullscreen || _isForegroundWindowFullscreen);
        Status?.SetActive(shouldShow);
        SyncClockTimer();

        // Fare izleme yalnız dock gizliyken (ya da kenardan açıldıysa, kapanışını görmek için) ve tam ekranda değilken
        // gerekir; tam ekranda kenardan açılma yok.
        var needsEdgeWatch = (!shouldShow || _isMouseAtBottomEdge) && !isBlockedByFullscreen && !_isForegroundWindowFullscreen;
        if (needsEdgeWatch != NeedsEdgeWatch)
        {
            NeedsEdgeWatch = needsEdgeWatch;
            EdgeWatchChanged?.Invoke(this, EventArgs.Empty);
        }

        double targetX, targetY;
        if (shouldShow)
        {
            targetX = resolved.TranslationX;
            targetY = resolved.TranslationY;
        }
        else if (resolved.IsHidden)
        {
            targetX = resolved.TranslationX;
            targetY = resolved.TranslationY;
        }
        else
        {
            var thickness = resolved.IsFullWidth ? DockPositionResolver.FullWidthHeightDip : (resolved.IsSnapped ? DockPositionResolver.SnappedHeightDip : DockPositionResolver.FloatingHeightDip);
            var offset = resolved.IsFullWidth ? _settings.Dock.EdgeOffsetDip : (resolved.IsSnapped ? _settings.Dock.EdgeOffsetDip : (DockPositionResolver.FloatingBottomMarginDip + _settings.Dock.EdgeOffsetDip));
            var shift = thickness + offset + 10.0;
            (targetX, targetY) = _settings.Dock.Edge switch
            {
                DockEdge.Top => (0.0, -shift),
                DockEdge.Left => (-shift, 0.0),
                DockEdge.Right => (shift, 0.0),
                _ => (0.0, shift)
            };
        }

        var targetOpacity = shouldShow ? resolved.Opacity : 0.0;

        if (ReduceMotion)
        {
            _translateXSpring.SnapTo(targetX);
            _translateYSpring.SnapTo(targetY);
            _opacitySpring.SnapTo(targetOpacity);
        }
        else
        {
            _translateXSpring.Target = targetX;
            _translateYSpring.Target = targetY;
            _opacitySpring.Target = targetOpacity;
        }

        _isAnimating = true;
        AnimationNeeded?.Invoke(this, EventArgs.Empty);

        RequestReposition?.Invoke();
    }

    // DWM Canlı Önizleme Tetikleyicisi
    public void OnItemMouseEnter(DockItemViewModel item, UIElement element)
    {
        _previewCloseTimer.Stop();

        if (!_settings.Dock.ThumbnailPreview || !item.HasOpenWindows)
        {
            _thumbnailHoverTimer.Stop();
            IsPreviewPopupOpen = false;
            PreviewItem = null;
            _pendingPreviewItem = null;
            return;
        }

        _pendingPreviewItem = item;
        PreviewPlacementTarget = element;

        // Önizleme kutucuğu halihazırda açıksa başka simgeye geçişte hemen güncelle ve yeniden konumlandır
        if (IsPreviewPopupOpen)
        {
            _thumbnailHoverTimer.Stop();
            PreviewItem = item;
            // WPF Popup PlacementTarget değiştiğinde HWND konumunu güncellemesi için IsOpen tazelenir
            IsPreviewPopupOpen = false;
            IsPreviewPopupOpen = true;
            return;
        }

        _thumbnailHoverTimer.Stop();
        _thumbnailHoverTimer.Start();
    }

    /// <summary>
    /// Sol tık "önizlemeleri göster" iken: bekleme olmadan önizleme kutucuğunu açar. Önizleme kapalıysa ya da
    /// açık pencere yoksa false döner (çağıran varsayılan tık eylemine düşer).
    /// </summary>
    public bool TryShowPreviewNow(DockItemViewModel item, UIElement element)
    {
        if (!_settings.Dock.ThumbnailPreview || !item.HasOpenWindows)
            return false;

        _thumbnailHoverTimer.Stop();
        _previewCloseTimer.Stop();
        _pendingPreviewItem = item;
        PreviewPlacementTarget = element;
        PreviewItem = item;
        // PlacementTarget değişince HWND konumunu güncellemesi için IsOpen tazelenir (OnItemMouseEnter ile aynı).
        IsPreviewPopupOpen = false;
        IsPreviewPopupOpen = true;
        return true;
    }

    public void OnItemMouseLeave()
    {
        _thumbnailHoverTimer.Stop();
        _pendingPreviewItem = null;

        if (IsPreviewPopupOpen)
        {
            _previewCloseTimer.Stop();
            _previewCloseTimer.Start();
        }
        else
        {
            PreviewItem = null;
        }
    }

    public void OnPopupMouseEnter()
    {
        _isMouseOverPopup = true;
        _previewCloseTimer.Stop();
        EvaluatePosition(_lastContentWidth);
    }

    public void OnPopupMouseLeave()
    {
        _isMouseOverPopup = false;
        _previewCloseTimer.Stop();
        _previewCloseTimer.Start();
    }

    private void OnPreviewCloseTimerTick(object? sender, EventArgs e)
    {
        _previewCloseTimer.Stop();
        _isMouseOverPopup = false;
        IsPreviewPopupOpen = false;
        PreviewItem = null;
        _pendingPreviewItem = null;
        EvaluatePosition(_lastContentWidth);
    }

    public void ClosePreviewNow()
    {
        _thumbnailHoverTimer.Stop();
        _previewCloseTimer.Stop();
        _pendingPreviewItem = null;
        _isMouseOverPopup = false;
        IsPreviewPopupOpen = false;
        PreviewItem = null;
        EvaluatePosition(_lastContentWidth);
    }

    private void OnThumbnailTimerTick(object? sender, EventArgs e)
    {
        _thumbnailHoverTimer.Stop();
        if (_pendingPreviewItem is not null && _pendingPreviewItem.HasOpenWindows)
        {
            PreviewItem = _pendingPreviewItem;
            IsPreviewPopupOpen = true;
        }
    }

    public void OnItemHover(int hoveredIndex)
    {
        if (!MagnificationEnabled || ReduceMotion)
            return;

        // Tepe büyüme çubuğun yüksekliğine göre sınırlanır (48 px çubukta öğe taşmaz); komşu yarısı kadar büyür.
        var peak = DockFitMath.HoverPeakScale(DockHeight, ItemSize);
        var neighbor = 1.0 + (peak - 1.0) * 0.5;
        // Büyütme animasyonu anahtarı kapalıysa büyüme yine olur ama yaysız, tek karede (issue #79).
        var animate = MagnifyAnimates;

        for (var i = 0; i < Items.Count; i++)
        {
            var dist = Math.Abs(i - hoveredIndex);
            var targetScale = dist switch
            {
                0 => peak,
                1 => neighbor,
                _ => 1.0
            };
            if (animate)
                Items[i].SetTargetScale(targetScale);
            else
                Items[i].SnapScale(targetScale);
        }

        _isAnimating = true;
        AnimationNeeded?.Invoke(this, EventArgs.Empty);
    }

    // Sabitli uygulamaları sürükleyerek sıralama: görsel kayma yaylarla, bırakınca sıra Core'da kalıcı yazılır.
    private bool _isReordering;

    /// <summary>Sürükleme sürüyor: büyütme ve önizleme susar, imleç öğeyi izler.</summary>
    public bool IsReordering => _isReordering;

    /// <summary>Öğe sürüklenebilir mi: sabitli, sıralama destekleniyor ve en az iki sabitli var.</summary>
    public bool CanStartReorder(DockEntryViewModel item)
        => !_hasExtras && item.IsPinned && _controller.CanReorderPinned && Items.Count(i => i.IsPinned) > 1;

    public int PinnedCount => Items.Count(i => i.IsPinned);

    public void BeginReorder()
    {
        _isReordering = true;
        ClosePreviewNow();
        OnHoverEnded();
    }

    /// <summary>Sürükleme sırasında: öğe imleci izler, aradakiler hedef yuvaya göre bir yuva kayar.</summary>
    public void UpdateReorder(DockEntryViewModel dragged, int originalIndex, double dragOffset, double slotWidth)
    {
        var count = PinnedCount;
        var target = DockReorderMath.TargetIndex(count, originalIndex, dragOffset, slotWidth);
        dragged.SnapOffset(dragOffset);

        for (var i = 0; i < count && i < Items.Count; i++)
        {
            if (i != originalIndex)
                Items[i].SetTargetOffset(DockReorderMath.ShiftFor(i, originalIndex, target, slotWidth));
        }

        _isAnimating = true;
        AnimationNeeded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Bırakıldı. Sıra değiştiyse koleksiyon hemen yeni sıraya taşınır ve her öğenin ekrandaki konumu korunacak biçimde
    /// kayması ayarlanır; sonra yay sıfıra çeker (zıplama olmaz). Sıra kalıcı yazılır.
    /// </summary>
    public void CommitReorder(DockEntryViewModel dragged, int originalIndex, double dragOffset, double slotWidth)
    {
        _isReordering = false;
        var count = PinnedCount;
        var target = DockReorderMath.TargetIndex(count, originalIndex, dragOffset, slotWidth);

        if (target == originalIndex)
        {
            foreach (var item in Items)
                item.SetTargetOffset(0);
        }
        else
        {
            // Bırakma anındaki ekran konumları: yuva dizini × genişlik + o anki kayma.
            var visual = new Dictionary<DockEntryViewModel, double>();
            for (var i = 0; i < Items.Count; i++)
                visual[Items[i]] = i * slotWidth + Items[i].OffsetX;

            Items.Move(originalIndex, target);

            for (var i = 0; i < Items.Count; i++)
            {
                Items[i].SnapOffset(visual[Items[i]] - i * slotWidth);
                Items[i].SetTargetOffset(0);
            }

            if (dragged is DockItemViewModel itemVm)
            {
                _controller.MovePinned(itemVm.Model.Id, target);
            }
            else if (dragged is DockFolderViewModel folderVm)
            {
                var newOrder = PinnedOrder.Move(_settings.DockItems.PinnedOrder, folderVm.Id, target);
                DockFeature.SavePinnedOrder(newOrder, _settings.DockItems.Folders, _settings);
            }
        }

        _isAnimating = true;
        AnimationNeeded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sürükleme yarıda kesildi (yakalama kaybı, Esc): her şey yerine döner.</summary>
    public void CancelReorder()
    {
        if (!_isReordering)
            return;

        _isReordering = false;
        foreach (var item in Items)
            item.SetTargetOffset(0);

        _isAnimating = true;
        AnimationNeeded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Öğeye basıldı / bırakıldı: simge içe çöker ve koyulaşır, bırakınca yayla geri gelir.</summary>
    public void SetEntryPressed(DockEntryViewModel entry, bool pressed)
    {
        if (entry.IsPressed == pressed)
            return;

        entry.SetPressed(pressed, animate: !ReduceMotion);
        _isAnimating = true;
        AnimationNeeded?.Invoke(this, EventArgs.Empty);
    }

    public void OnHoverEnded()
    {
        var animate = MagnifyAnimates;
        for (var i = 0; i < Items.Count; i++)
        {
            if (!animate)
                Items[i].SnapScale(1.0);
            else
                Items[i].SetTargetScale(1.0);
        }

        _isAnimating = true;
        AnimationNeeded?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateSprings(double deltaSeconds)
    {
        if (!_isAnimating)
            return;

        deltaSeconds *= TimeScale;

        var txMoving = _translateXSpring.Step(deltaSeconds);
        var tyMoving = _translateYSpring.Step(deltaSeconds);
        var oMoving = _opacitySpring.Step(deltaSeconds);

        TranslationX = _translateXSpring.Value;
        TranslationY = _translateYSpring.Value;
        Opacity = Math.Clamp(_opacitySpring.Value, 0.0, 1.0);

        var scaleMoving = false;
        foreach (var item in Items)
        {
            if (item.StepScale(deltaSeconds))
                scaleMoving = true;
            if (item.StepOffset(deltaSeconds))
                scaleMoving = true;
            if (item.StepPress(deltaSeconds))
                scaleMoving = true;
        }

        if (!txMoving && !tyMoving && !oMoving && !scaleMoving)
        {
            _isAnimating = false;
            AnimationStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    #region Dock Klasörleri İşlemleri

    public void DissolveFolder(DockFolderViewModel folderVm)
    {
        var (order, folders) = DockFolders.Dissolve(_settings.DockItems.PinnedOrder, _settings.DockItems.Folders, folderVm.Folder.Id);
        DockFeature.SavePinnedOrder(order, folders, _settings);
        UpdateItems();
    }

    public void RenameFolder(DockFolderViewModel folderVm, string newName)
    {
        var folders = DockFolders.Rename(_settings.DockItems.Folders, folderVm.Folder.Id, newName);
        DockFeature.SavePinnedOrder(_settings.DockItems.PinnedOrder, folders, _settings);
        folderVm.Title = newName;
    }

    public void RemoveAppFromFolder(string folderId, string itemId, int insertIndex)
    {
        if (insertIndex < 0)
        {
            var folderToken = PinnedOrder.ToFolderToken(PinnedOrder.ExtractFolderId(folderId));
            var fIdx = _settings.DockItems.PinnedOrder.FindIndex(x => string.Equals(x, folderToken, StringComparison.OrdinalIgnoreCase));
            insertIndex = fIdx >= 0 ? fIdx + 1 : _settings.DockItems.PinnedOrder.Count;
        }

        var (order, folders) = DockFolders.RemoveFromFolder(_settings.DockItems.PinnedOrder, _settings.DockItems.Folders, folderId, itemId, insertIndex);
        DockFeature.SavePinnedOrder(order, folders, _settings);
        UpdateItems();
    }

    public void CreateFolderFromDrop(string draggedId, string targetId)
    {
        var defaultName = DockFolders.NextDefaultName(_settings.DockItems.Folders, Resources.Strings.Get("DockFolder"));
        var (order, folders) = DockFolders.CreateFromDrop(_settings.DockItems.PinnedOrder, _settings.DockItems.Folders, draggedId, targetId, defaultName);
        DockFeature.SavePinnedOrder(order, folders, _settings);
        UpdateItems();
    }

    public void AddAppToFolder(string draggedId, string folderId)
    {
        var (order, folders) = DockFolders.AddToFolder(_settings.DockItems.PinnedOrder, _settings.DockItems.Folders, draggedId, folderId);
        DockFeature.SavePinnedOrder(order, folders, _settings);
        UpdateItems();
    }

    #endregion

    private void UpdateItems()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var models = VisibleModels();
            var folders = _settings.DockItems.Folders ?? [];
            var pinnedOrder = _settings.DockItems.PinnedOrder ?? [];

            // 1. Klasörlerdeki uygulamaları tespit et (bunlar ana dock sırasında ayrıca görünmez)
            var folderAppMap = new Dictionary<string, DockFolder>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in folders)
            {
                foreach (var app in folder.Items)
                {
                    folderAppMap[app] = folder;
                }
            }

            // 2. DockItemViewModel'leri model kimliği veya kısayol adına göre eşle
            var itemVmCache = new Dictionary<string, DockItemViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in models)
            {
                var vm = new DockItemViewModel(m, _controller)
                {
                    IconSize = IconSize,
                    ItemSize = ItemSize,
                    Presentation = _presentation
                };
                AttachAppIcon(vm);
                itemVmCache[m.Id] = vm;
                itemVmCache[PinnedOrder.IdOf(m.Id)] = vm;
            }

            // 3. Hedef dock sırasını belirle
            var availableLnkIds = models.Where(m => m.IsPinned).Select(m => PinnedOrder.IdOf(m.Id)).ToList();
            var (effectiveOrder, reconciledFolders) = PinnedOrder.Reconcile(pinnedOrder, availableLnkIds, folders);
            _effectiveOrder = effectiveOrder;

            var folderDict = reconciledFolders.ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);
            var desiredEntries = new List<DockEntryViewModel>();

            // Sabitli öğeleri ve klasörleri sırayla ekle
            foreach (var token in effectiveOrder)
            {
                if (PinnedOrder.IsFolderId(token))
                {
                    var fId = PinnedOrder.ExtractFolderId(token);
                    if (folderDict.TryGetValue(fId, out var folder))
                    {
                        var folderItems = new List<DockItemViewModel>();
                        foreach (var itemId in folder.Items)
                        {
                            if (itemVmCache.TryGetValue(itemId, out var itemVm))
                            {
                                folderItems.Add(itemVm);
                            }
                            else
                            {
                                var fallbackModel = new DockItem(
                                    Id: itemId,
                                    Title: Path.GetFileNameWithoutExtension(itemId),
                                    FilePath: itemId,
                                    TargetPath: null,
                                    IconData: null,
                                    IsPinned: true,
                                    IsRunning: false,
                                    IsActive: false,
                                    WindowHandles: []);
                                var fallbackVm = new DockItemViewModel(fallbackModel, _controller)
                                {
                                    IconSize = IconSize,
                                    ItemSize = ItemSize,
                                    Presentation = _presentation
                                };
                                AttachAppIcon(fallbackVm);
                                folderItems.Add(fallbackVm);
                            }
                        }

                        var existingFolder = Items.OfType<DockFolderViewModel>().FirstOrDefault(f => string.Equals(f.Folder.Id, folder.Id, StringComparison.OrdinalIgnoreCase));
                        if (existingFolder is not null)
                        {
                            existingFolder.IconSize = IconSize;
                            existingFolder.ItemSize = ItemSize;
                            existingFolder.OwnerViewModel = this;
                            existingFolder.UpdateFolder(folder, folderItems);
                            desiredEntries.Add(existingFolder);
                        }
                        else
                        {
                            var newFolderVm = new DockFolderViewModel(folder, folderItems, this)
                            {
                                IconSize = IconSize,
                                ItemSize = ItemSize
                            };
                            desiredEntries.Add(newFolderVm);
                        }
                    }
                }
                else
                {
                    if (!folderAppMap.ContainsKey(token) && itemVmCache.TryGetValue(token, out var itemVm))
                    {
                        var existingItem = Items.OfType<DockItemViewModel>().FirstOrDefault(i => string.Equals(i.Model.Id, itemVm.Model.Id, StringComparison.OrdinalIgnoreCase));
                        if (existingItem is not null)
                        {
                            existingItem.IconSize = IconSize;
                            existingItem.ItemSize = ItemSize;
                            existingItem.Update(itemVm.Model);
                            desiredEntries.Add(existingItem);
                        }
                        else
                        {
                            desiredEntries.Add(itemVm);
                        }
                    }
                }
            }

            // Sabitli olmayan açık (çalışan) pencereleri ekle
            foreach (var m in models.Where(m => !m.IsPinned))
            {
                if (!folderAppMap.ContainsKey(PinnedOrder.IdOf(m.Id)) && !folderAppMap.ContainsKey(m.Id))
                {
                    var existingItem = Items.OfType<DockItemViewModel>().FirstOrDefault(i => string.Equals(i.Model.Id, m.Id, StringComparison.OrdinalIgnoreCase));
                    if (existingItem is not null)
                    {
                        existingItem.IconSize = IconSize;
                        existingItem.ItemSize = ItemSize;
                        existingItem.Update(m);
                        desiredEntries.Add(existingItem);
                    }
                    else if (itemVmCache.TryGetValue(m.Id, out var itemVm))
                    {
                        desiredEntries.Add(itemVm);
                    }
                }
            }

            // Ek öğeler varsa şerit düzenine katılır (yoksa desiredEntries aynen kalır).
            desiredEntries = ComposeExtras(desiredEntries);

            // 4. Koleksiyonu yerinde güncelle
            foreach (var entry in desiredEntries)
                entry.IsVertical = IsVertical;

            var desiredIds = new HashSet<string>(desiredEntries.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                if (!desiredIds.Contains(Items[i].Id))
                {
                    Items.RemoveAt(i);
                }
            }

            for (var targetIndex = 0; targetIndex < desiredEntries.Count; targetIndex++)
            {
                var entry = desiredEntries[targetIndex];
                var existingIndex = -1;
                for (var j = 0; j < Items.Count; j++)
                {
                    if (string.Equals(Items[j].Id, entry.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    if (existingIndex != targetIndex && existingIndex < Items.Count && targetIndex < Items.Count)
                    {
                        Items.Move(existingIndex, targetIndex);
                    }
                }
                else
                {
                    if (targetIndex < Items.Count)
                        Items.Insert(targetIndex, entry);
                    else
                        Items.Add(entry);
                }
            }

            RecomputeFit();
            EvaluatePosition(_lastContentWidth);
            SyncClockTimer();
        });
    }

    #region Ek öğeler

    /// <summary>
    /// Kayıtlı ek öğeleri uygulama/klasör girdileriyle birleştirir. Ek öğe yoksa girdiler değişmeden döner.
    /// Sabitlenmemiş çalışan uygulamalar son sabitli girdinin arkasına girer.
    /// </summary>
    private List<DockEntryViewModel> ComposeExtras(List<DockEntryViewModel> entries)
    {
        var specs = DockItemRules.Resolve(_settings.DockItems.Items, _settings.DockItems.PinnedOrder ?? []);
        _hasExtras = DockItemLayout.HasExtras(specs);
        if (!_hasExtras)
        {
            _layout = specs;
            return entries;
        }

        var layout = DockItemLayout.Merge(specs, _effectiveOrder);
        _layout = layout;

        var pinned = new Dictionary<string, DockEntryViewModel>(StringComparer.OrdinalIgnoreCase);
        var running = new List<DockEntryViewModel>();
        foreach (var entry in entries)
        {
            if (entry.IsPinned)
                pinned.TryAdd(entry is DockFolderViewModel ? entry.Id : PinnedOrder.IdOf(entry.Id), entry);
            else
                running.Add(entry);
        }

        var result = new List<DockEntryViewModel>();
        var separators = 0;
        var spacers = 0;
        for (var i = 0; i < layout.Count; i++)
        {
            var spec = layout[i];
            var token = DockItemLayout.TokenOf(spec);
            if (token is not null)
            {
                if (pinned.TryGetValue(token, out var entry))
                    result.Add(entry);
                continue;
            }

            var id = spec.Kind switch
            {
                DockItemKind.Separator => $"item:separator:{separators++}",
                DockItemKind.Spacer => $"item:spacer:{spacers++}",
                _ => $"item:{spec.Kind.ToString().ToLowerInvariant()}:{spec.Id}",
            };

            var extra = Items.OfType<DockExtraViewModel>().FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (extra is null)
                extra = new DockExtraViewModel(spec, id, i);
            else
                extra.Update(spec, i);

            extra.IconSize = IconSize;
            extra.ItemSize = ItemSize;
            extra.Title = ExtraTitle(spec);
            result.Add(extra);
        }

        var lastPinned = result.FindLastIndex(e => e.IsPinned);
        result.InsertRange(lastPinned + 1, running);
        return result;
    }

    private static string ExtraTitle(DockItemSpec spec) => spec.Kind switch
    {
        DockItemKind.Clock => Resources.Strings.Get("DockItemClock"),
        DockItemKind.Trash => Resources.Strings.Get("DockItemTrash"),
        DockItemKind.Custom => DockCustomCommand.TryParse(spec.Command, out var launch)
            ? launch.Target
            : Resources.Strings.Get("DockItemCustom"),
        _ => string.Empty,
    };

    /// <summary>Ek öğeye tıklandı. Saat Bildirim Merkezi'ni (takvim), çöp kutusu Geri Dönüşüm Kutusu'nu, özel düğme hedefini açar.</summary>
    public void ActivateExtra(DockExtraViewModel extra)
    {
        ClosePreviewNow();
        switch (extra.Kind)
        {
            case DockItemKind.Clock:
                _shellActionService.OpenNotificationCenter();
                break;
            case DockItemKind.Trash:
                // Boşaltma bu sürümde yok: yalnız kutuyu açar. Doluluk durumu için yoklama gerekeceğinden simge sabittir.
                _shellActionService.Launch("shell:RecycleBinFolder");
                break;
            case DockItemKind.Custom:
                // Komut güvenilmez: Core ayrıştırır, geçersizse hiçbir şey çalışmaz. Hedef ve bağımsız değişken ayrı geçer.
                if (DockCustomCommand.TryParse(extra.Spec.Command, out var launch))
                    _shellActionService.Launch(launch.Target, launch.Arguments);
                break;
        }
    }

    [RelayCommand]
    private void AddSeparator() => AddExtra(new DockItemSpec(DockItemKind.Separator, "", ""));

    [RelayCommand]
    private void AddSpacer() => AddExtra(new DockItemSpec(DockItemKind.Spacer, "", ""));

    [RelayCommand]
    private void AddClock() => AddExtra(new DockItemSpec(DockItemKind.Clock, nameof(DockItemKind.Clock), ""));

    [RelayCommand]
    private void AddTrash() => AddExtra(new DockItemSpec(DockItemKind.Trash, nameof(DockItemKind.Trash), ""));

    [RelayCommand]
    private void AddCustom()
    {
        var command = DockCustomButtonPicker.PickCommand();
        if (command is not null)
            AddExtra(new DockItemSpec(DockItemKind.Custom, DockItemLayout.NewCustomId(), command));
    }

    private void AddExtra(DockItemSpec spec)
    {
        var current = DockItemLayout.Merge(
            DockItemRules.Resolve(_settings.DockItems.Items, _settings.DockItems.PinnedOrder ?? []),
            _effectiveOrder);
        SaveLayout(DockItemLayout.Add(current, spec));
    }

    public void RemoveExtra(DockExtraViewModel extra) =>
        SaveLayout(DockItemLayout.RemoveAt(_layout, extra.LayoutIndex));

    /// <summary>Ek öğeyi bir yuva sola (-1) ya da sağa (+1) kaydırır.</summary>
    public void MoveExtra(DockExtraViewModel extra, int delta) =>
        SaveLayout(DockItemRules.Move(_layout, extra.LayoutIndex, extra.LayoutIndex + delta));

    private void SaveLayout(IReadOnlyList<DockItemSpec> layout)
    {
        DockFeature.SaveItems(DockItemLayout.ToStoredEntries(layout), _settings);
        UpdateItems();
    }

    /// <summary>
    /// Saat zamanlayıcısı yalnız dock görünürken ve şeritte saat varken çalışır; dakika başına hizalı tek uyanma
    /// (saniye gösterilmez). Gizlenince durur (AGENTS #5).
    /// </summary>
    private void SyncClockTimer()
    {
        var clocks = Items.OfType<DockExtraViewModel>().Where(x => x.IsClock).ToList();
        if (_disposed || !IsVisible || clocks.Count == 0)
        {
            _clockTimer?.Stop();
            return;
        }

        if (_clockTimer is null)
        {
            _clockTimer = new DispatcherTimer(DispatcherPriority.Background);
            _clockTimer.Tick += OnClockTimerTick;
        }

        if (_clockTimer.IsEnabled)
        {
            foreach (var clock in clocks.Where(c => c.TimeText.Length == 0))
                ApplyClockText(clock, DateTime.Now);
            return;
        }

        OnClockTimerTick(null, EventArgs.Empty);
    }

    private void OnClockTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        foreach (var clock in Items.OfType<DockExtraViewModel>().Where(x => x.IsClock))
            ApplyClockText(clock, now);

        if (_clockTimer is null)
            return;

        _clockTimer.Interval = DockClockTick.DelayToNextMinute(now);
        _clockTimer.Start();
    }

    private static void ApplyClockText(DockExtraViewModel clock, DateTime now)
    {
        clock.TimeText = DockClockTick.FormatTime(now, System.Globalization.CultureInfo.CurrentCulture);
        clock.DateText = DockClockTick.FormatDate(now, System.Globalization.CultureInfo.CurrentCulture);
    }

    #endregion

    private void OnMediaChanged(object? sender, MediaInfo? info)
    {
        Application.Current?.Dispatcher.InvokeAsync(() => UpdateMediaInfo(info));
    }

    private void UpdateMediaInfo(MediaInfo? info)
    {
        if (info is null || !info.IsPlaying)
        {
            HasNowPlaying = false;
            NowPlayingTitle = Strings.NoMediaPlaying;
            NowPlayingArtist = "Koy";
            NowPlayingCover = null;
            return;
        }

        HasNowPlaying = true;
        NowPlayingTitle = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : Strings.Get("UnknownTrack");
        NowPlayingArtist = !string.IsNullOrWhiteSpace(info.Artist) ? info.Artist : Strings.Get("UnknownArtist");

        if (info.Thumbnail is { Length: > 0 })
        {
            try
            {
                using var ms = new MemoryStream(info.Thumbnail);
                var img = new BitmapImage();
                img.BeginInit();
                img.DecodePixelWidth = 28;
                img.StreamSource = ms;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                NowPlayingCover = img;
            }
            catch
            {
                NowPlayingCover = null;
            }
        }
        else
        {
            NowPlayingCover = null;
        }
    }

    private void OnControllerItemsChanged(object? sender, EventArgs e) => UpdateItems();
    private void OnFullscreenStateChanged(object? sender, bool isFullscreen) => EvaluatePosition(_lastContentWidth);
    private void OnVisibilityChanged(object? sender, VisibilityResult result)
    {
        // Medya peek zamanlayıcısı ThreadPool'dan tikler; EvaluatePosition pencereye dokunduğu için UI'a taşı.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            EvaluatePosition(_lastContentWidth);
            return;
        }

        dispatcher.InvokeAsync(() =>
        {
            if (!_disposed)
                EvaluatePosition(_lastContentWidth);
        });
    }
    private void OnActiveMenuChanged(object? sender, EventArgs e) => EvaluatePosition(_lastContentWidth);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        MaterialState.Changed -= OnMaterialChanged;
        _thumbnailHoverTimer.Stop();
        _previewCloseTimer.Stop();
        _dodgeHoverTimer.Stop();
        _snapTimer?.Stop();
        if (_clockTimer is not null)
        {
            _clockTimer.Stop();
            _clockTimer.Tick -= OnClockTimerTick;
        }

        DockContextMenuManager.ActiveMenuChanged -= OnActiveMenuChanged;

        _controller.ItemsChanged -= OnControllerItemsChanged;
        _controller.FullscreenStateChanged -= OnFullscreenStateChanged;

        if (_visibilityController is not null)
        {
            _visibilityController.Changed -= OnVisibilityChanged;
        }

        if (_mediaService is not null)
        {
            _mediaService.Changed -= OnMediaChanged;
        }

        if (_trayService is not null)
        {
            _trayService.IconsChanged -= OnTrayIconsChanged;
            _trayService = null;
        }

        _trayCloseTimer?.Stop();
        _trayClearTimer?.Stop();
    }
}
