using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Koy.App.Resources;
using Koy.Core.Animation;
using Koy.Core.Shell;
using Koy.Core.Tray;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock.Tray;

/// <summary>
/// Dock'taki sistem tepsisi düğmesi ve açılan simge penceresi (yüzen ve tam genişlik dock'ta ortak).
/// Pencere Windows'un taşma penceresi gibi davranır: uygulamanın bağlam menüsü açıkken açık kalır; menüden seçim
/// yapılınca ya da pencere ve menünün dışına tıklanınca kısa bir yay animasyonuyla kapanır. Odak kaybı kapatma
/// sebebi değildir (menü öne gelince odak zaten başka sürece geçer).
/// </summary>
public partial class TrayButton : UserControl
{
    private const double ShiftDip = 8.0;
    private const double ScaleFrom = 0.96;

    private bool _isDoubleClick;
    private DockViewModel? _subscribed;

    // 0 = gizli, 1 = tam görünür. Yalnız hareket varken kare çizilir (CompositionTarget.Rendering bağlı kalmaz).
    private readonly Spring _spring = new(0) { Stiffness = 520, Damping = 38 };
    private bool _renderHooked;
    private TimeSpan _lastRender = TimeSpan.MinValue;
    private bool _closing;

    // Pencere açıkken 50 ms yoklama: dış tıklama ve menü durumu. Kapalıyken zamanlayıcı durur (boşta iş yok).
    private readonly DispatcherTimer _watchTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly TrayPopupDismissPolicy _dismiss = new();
    private bool _prevPointerDown;

    /// <summary>
    /// Küçük tepsi biçimi: dar ok olarak Koy menüsü düğmesinin sağında durur. Değişince görünürlük ve genişlik
    /// ViewModel'deki ilgili özelliklere bağlanır (aynı düğmenin tam boy ve küçük örneği dock'ta birlikte bulunur).
    /// </summary>
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(TrayButton),
        new PropertyMetadata(false, (d, e) => ((TrayButton)d).ApplyCompact((bool)e.NewValue)));

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private void ApplyCompact(bool compact)
    {
        if (!compact)
            return;

        SetBinding(VisibilityProperty, new System.Windows.Data.Binding(nameof(DockViewModel.ShowSmallTray))
        {
            Converter = DockConverters.BoolToVisibilityConverter
        });
        TrayToggle.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(DockViewModel.SmallTrayWidth)));
        TrayToggle.Margin = new Thickness(1, 0, 0, 0);
        TrayToggle.Tag = "compact";
    }

    public TrayButton()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Subscribe();
        IsVisibleChanged += (_, _) => SyncPopup();
        TrayPopup.Closed += OnPopupClosed;
        _watchTimer.Tick += OnWatchTick;
        Unloaded += (_, _) => StopAll();
    }

    private DockViewModel? ViewModel => DataContext as DockViewModel;

    // Yüzen ve tam genişlik dock'ta birer düğme var, biri hep gizli. Popup'ı XAML'de IsTrayPopupOpen'a bağlamak
    // gizli olanın penceresini de açıyordu (Windows tepsisinin yerinde ikinci bir pencere). Yalnız görünen açılır.
    private void Subscribe()
    {
        if (_subscribed is not null)
            _subscribed.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribed = ViewModel;
        if (_subscribed is not null)
            _subscribed.PropertyChanged += OnViewModelPropertyChanged;

        SyncPopup();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockViewModel.IsTrayPopupOpen))
            SyncPopup();
    }

    private void SyncPopup()
    {
        var wantOpen = IsVisible && ViewModel?.IsTrayPopupOpen == true;
        if (wantOpen)
            Open();
        else
            Close();
    }

    private bool AnimationsEnabled => Motion.Enabled && ViewModel is { ReduceMotion: false };

    private void Open()
    {
        _closing = false;
        if (!TrayPopup.IsOpen)
        {
            ApplyVisual(AnimationsEnabled ? 0 : 1);
            _spring.SnapTo(AnimationsEnabled ? 0 : 1);
            _dismiss.Reset();
            _prevPointerDown = CursorHelper.IsAnyMouseButtonDown();
            TrayPopup.IsOpen = true;
        }

        _watchTimer.Start();

        if (AnimationsEnabled)
        {
            _spring.Target = 1;
            StartRendering();
        }
        else
        {
            _spring.SnapTo(1);
            ApplyVisual(1);
        }
    }

    private void Close()
    {
        if (!TrayPopup.IsOpen || _closing)
            return;

        _watchTimer.Stop();
        _dismiss.Reset();

        // Görünmeyen düğmenin penceresi (öteki dock biçimi) ya da hareketsiz ayar: anında kapanır.
        if (!AnimationsEnabled || !IsVisible)
        {
            FinishClose();
            return;
        }

        _closing = true;
        _spring.Target = 0;
        StartRendering();
    }

    private void FinishClose()
    {
        _closing = false;
        StopRendering();
        _watchTimer.Stop();
        TrayPopup.IsOpen = false;
    }

    private void StopAll()
    {
        StopRendering();
        _watchTimer.Stop();
        _closing = false;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        // Pencere başka yoldan kapandıysa (örn. dock yok edildi) durum ViewModel'e yansısın.
        _watchTimer.Stop();
        if (ViewModel is { IsTrayPopupOpen: true } vm)
            vm.IsTrayPopupOpen = false;
    }

    // ---- Animasyon: yalnız hareket sürerken kare çizilir ----

    private void StartRendering()
    {
        if (_renderHooked)
            return;

        _renderHooked = true;
        _lastRender = TimeSpan.MinValue;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (!_renderHooked)
            return;

        _renderHooked = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
            return;

        if (_lastRender == TimeSpan.MinValue)
        {
            _lastRender = args.RenderingTime;
            return;
        }

        var dt = (args.RenderingTime - _lastRender).TotalSeconds;
        _lastRender = args.RenderingTime;

        // Animasyon hızı ayarı (Sakin yavaşlatır, Hızlı hızlandırır).
        var scale = Motion.Scale > 0 ? Motion.Scale : 1.0;
        var moving = _spring.Step(dt / scale);
        ApplyVisual(Math.Clamp(_spring.Value, 0.0, 1.05));

        if (moving)
            return;

        StopRendering();
        if (_closing)
            FinishClose();
    }

    private void ApplyVisual(double v)
    {
        var clamped = Math.Clamp(v, 0.0, 1.0);
        PopupBody.Opacity = clamped;
        PopupShift.Y = (1.0 - clamped) * ShiftDip;
        var s = ScaleFrom + (1.0 - ScaleFrom) * Math.Min(v, 1.0);
        PopupScale.ScaleX = s;
        PopupScale.ScaleY = s;
    }

    // ---- Kapatma kararı: dış tıklama ve menü durumu ----

    private void OnWatchTick(object? sender, EventArgs e)
    {
        if (!TrayPopup.IsOpen || _closing)
        {
            _watchTimer.Stop();
            return;
        }

        var down = CursorHelper.IsAnyMouseButtonDown();
        var pressed = down && !_prevPointerDown;
        _prevPointerDown = down;

        var insidePopup = false;
        var insideMenu = false;
        IReadOnlyList<Koy.Core.Shell.WindowRect> menus = [];

        if (pressed || _menuMayBeOpen)
            menus = PopupMenuProbe.GetVisibleMenuRects(bringToFront: true);

        if (pressed && CursorHelper.GetPosition(out var x, out var y))
        {
            if (PresentationSource.FromVisual(PopupBody) is HwndSource source &&
                PopupMenuProbe.TryGetWindowRect(source.Handle, out var popupRect))
            {
                // Popup penceresi gölge payı (Margin=12) da içerir; payın içindeki tıklama dışarı sayılmaz, zararsızdır.
                insidePopup = popupRect.Contains(x, y);
            }

            insideMenu = menus.Any(m => m.Contains(x, y));
        }

        var action = _dismiss.Tick(
            TimeSpan.FromMilliseconds(Environment.TickCount64), pressed, insidePopup, insideMenu, menus.Count > 0);

        _menuMayBeOpen = _dismiss.IsExpectingMenu;

        if (action == TrayPopupDismissAction.Close && ViewModel is { } vm)
            vm.IsTrayPopupOpen = false;
    }

    // Menü sorgusu (EnumWindows benzeri) yalnız tıklama anında ya da sağ tıktan sonra menü beklenirken yapılır.
    private bool _menuMayBeOpen;

    // ---- Simge tıklamaları ----

    // Menü çıpası dock'un üzerine düşmesin diye dock penceresinin ekran dikdörtgeni (fiziksel piksel).
    private WindowRect GetDockRect()
    {
        var window = Window.GetWindow(this);
        return window is not null && PopupMenuProbe.TryGetWindowRect(new WindowInteropHelper(window).Handle, out var rect)
            ? rect
            : WindowRect.Empty;
    }

    private void OnIconLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Çift tıklama bırakmada gönderilir: Windows görev çubuğu da DBLCLK ardından LBUTTONUP yollar.
        _isDoubleClick = e.ClickCount >= 2;
        e.Handled = true;
    }

    private void OnIconLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TrayIconViewModel icon })
            ViewModel?.SendTrayMouse(icon, _isDoubleClick ? TrayMouseAction.LeftDoubleClick : TrayMouseAction.LeftClick, GetDockRect());

        _isDoubleClick = false;
        e.Handled = true;
    }

    private void OnIconRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TrayIconViewModel icon })
        {
            ViewModel?.SendTrayMouse(icon, TrayMouseAction.RightClick, GetDockRect());

            // Pencere açık kalır; uygulamanın menüsü belirince ve kapanınca karar verilir.
            _dismiss.OnIconRightClicked(TimeSpan.FromMilliseconds(Environment.TickCount64));
            _menuMayBeOpen = true;
        }

        e.Handled = true;
    }
}
