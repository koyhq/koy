using System.Windows;
using Koy.Core.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Koy.Core.Animation;
using Koy.App.Resources;
using Koy.App.Features.Notch.OSD;
using Koy.Core.Display;
using Koy.Core.Notch;
using Koy.Platform.Shell;

namespace Koy.App.Features.Notch;

public partial class NotchWindow : Window
{
    private readonly Spring _width = new(236);
    private readonly Spring _height = new(34);
    private readonly Spring _cornerRadius = new(18);
    private readonly Spring _translateY = new(0);
    private readonly Spring _panelTranslateX = new(0);
    private readonly Spring _panelOpacity = new(1);
    private TimeSpan _lastFrame;
    // Panel geçişinin süre çarpanından türeyen zaman ölçeği (Sakin 1/1.5, Hızlı 1/0.65); MotionPolicy ile aynı kaynak.
    private double _panelTimeScale = 1.0;
    private bool _animating;
    private bool _isNotchVisible = true;
    private nint _hwnd;

    private const int WheelVolumeStepPercent = 2;

    private NotchViewModel? ViewModel => DataContext as NotchViewModel;

    // Yüzen adada pencerenin Top'u ekran üstünden kayar; tuval boyutu ve tuval içi konumlar değişmez (AGENTS #13).
    private double TopOffsetDip => ViewModel?.TopOffsetDip ?? 0;

    private DisplayMonitor? _targetMonitor;
    public Action? DisplayChangeHandler { get; set; }
    public Action? VoiceConfirmRequested { get; set; }
    public Action? VoiceCancelRequested { get; set; }
    public Action? VoiceReplyRequested { get; set; }

    public DisplayMonitor? TargetMonitor => _targetMonitor;
    public nint Handle => _hwnd != 0 ? _hwnd : (_hwnd = new WindowInteropHelper(this).Handle);

    public NotchWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PlaceAtTopCenter(_targetMonitor);
        DpiChanged += (_, _) => PlaceAtTopCenter(_targetMonitor);
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (ViewModel?.Osd.Type == OsdType.Voice)
        {
            if (e.Key == Key.Enter)
            {
                VoiceConfirmRequested?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                VoiceCancelRequested?.Invoke();
                e.Handled = true;
            }
        }
    }

    private const int WmDisplayChange = 0x007E;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.ApplyToolWindowAndNoActivate(_hwnd);
        WindowStyles.ReassertTopmost(_hwnd);

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        PlaceAtTopCenter(_targetMonitor);

        if (ViewModel is not null)
        {
            AnimateVisibility(ViewModel.IsNotchVisible);
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // WndProc'tan sızan istisna süreci düşürür; logla ve yut.
        if (msg == WmDisplayChange)
        {
            try
            {
                DisplayChangeHandler?.Invoke();
                PlaceAtTopCenter(_targetMonitor);
            }
            catch (Exception ex)
            {
                CrashLog.Write("NotchWindow.WndProc", ex);
            }
        }
        return 0;
    }

    public void SetTargetMonitor(DisplayMonitor? monitor)
    {
        _targetMonitor = monitor;
        PlaceAtTopCenter(monitor);
    }

    public void PlaceAtTopCenter(DisplayMonitor? targetMonitor = null)
    {
        _targetMonitor = targetMonitor ?? _targetMonitor;

        if (_targetMonitor is not null && _targetMonitor.PixelWidth > 100 && _hwnd != 0)
        {
            // Pencere oluşmuşsa konum hedef ekranın kendi ölçeğiyle pikselde verilir (karışık DPI'da DIP Left/Top yanıltır).
            // Pencere şu an başka DPI'lı ekrandaysa önce yalnız taşınır: Windows taşırken boyutu kendi ölçekler
            // (WM_DPICHANGED), boyut aynı çağrıda verilirse iki kez ölçeklenir. DpiChanged gelince tam yerleşir.
            var windowScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var sameDpi = Math.Abs(windowScale - _targetMonitor.DpiScaleX) < 0.01;
            WindowStyles.SetBounds(_hwnd, WindowPlacementMath.TopCenter(_targetMonitor, Width, Height, TopOffsetDip), resize: sameDpi);
        }
        else if (_targetMonitor is not null && _targetMonitor.WidthDip > 100)
        {
            // Pencere henüz oluşmadı: yaklaşık DIP konumu; SourceInitialized'da piksel konumu uygulanır.
            Left = _targetMonitor.LeftDip + (_targetMonitor.WidthDip - Width) / 2;
            Top = _targetMonitor.TopDip + TopOffsetDip;
        }
        else
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = (screenWidth - Width) / 2;
            Top = TopOffsetDip;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is NotchViewModel oldVm)
        {
            oldVm.TargetSizeChanged -= AnimateTo;
            oldVm.TargetVisibilityChanged -= AnimateVisibility;
            oldVm.PanelTransitionTriggered -= AnimatePanelTransition;
            oldVm.ShakeRequested -= OnShakeRequested;
            oldVm.SearchClosed -= OnSearchClosed;
            oldVm.Home.QuickLaunch.ResolveDialogOwner = null;
        }

        if (e.NewValue is NotchViewModel newVm)
        {
            newVm.TargetSizeChanged += AnimateTo;
            newVm.TargetVisibilityChanged += AnimateVisibility;
            newVm.PanelTransitionTriggered += AnimatePanelTransition;
            newVm.ShakeRequested += OnShakeRequested;
            newVm.SearchClosed += OnSearchClosed;
            newVm.Home.QuickLaunch.ResolveDialogOwner = PrepareDialogOwner;
            AnimateTo(newVm.TargetWidth, newVm.TargetHeight);
            if (!newVm.IsNotchVisible)
            {
                // Peek modunda başlangıçta çentik aniden kaybolmak yerine doğrudan gizli başlar
                _isNotchVisible = false;
                _translateY.SnapTo(-150);
                NotchTranslate.Y = -150;
                if (_hwnd != 0)
                    WindowStyles.SetClickThrough(_hwnd, true);
            }
            else
            {
                AnimateVisibility(newVm.IsNotchVisible);
            }
        }
    }

    // Çentik odak çalmaz (WS_EX_NOACTIVATE); bu yüzden yazı alanına tıklanınca pencere geçici olarak etkinleştirilir
    // ki klavye çalışsın (takvimde etkinlik eklemek için). Odak gidince yine odak çalmaz hale gelir.
    private void Notch_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Komut kutusu açıkken: "Enter ↵" etiketi komutu çalıştırır; başka yere tıklama çentiği yeniden etkinleştirip
        // odağı kutuya verir (sesle yazma paneli açılınca çentik odağı kaybedebiliyor, Enter/Esc o zaman gelmez).
        if (ViewModel?.Osd.Type == OsdType.Voice && ViewModel.Osd.IsVoiceEditing)
        {
            if (OsdHost.IsEnterLabel(e.OriginalSource as DependencyObject))
            {
                VoiceConfirmRequested?.Invoke();
                e.Handled = true;
            }
            else
            {
                FocusVoiceConfirmation();
            }

            return;
        }

        // Komut bitti ya da hata verdi: tıklama yanıtın tamamını açar.
        if (ViewModel?.Osd.Type == OsdType.Voice
            && ViewModel.Osd.VoiceState is Koy.Core.Voice.VoiceState.Completed or Koy.Core.Voice.VoiceState.Failed)
        {
            VoiceReplyRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if (_hwnd == 0 || e.OriginalSource is not DependencyObject source || FindParent<TextBox>(source) is null)
            return;

        WindowStyles.SetNoActivate(_hwnd, false);
        Activate();
    }

    /// <summary>
    /// Komut kutusu açılınca klavye odağını çentiğe ve kutuya verir: sesle yazma (Win+H) odaktaki kutuya yazar, Enter/Esc burada yakalanır.
    /// </summary>
    public void FocusVoiceConfirmation()
    {
        if (_hwnd == 0)
            return;

        WindowStyles.SetNoActivate(_hwnd, false);
        Activate();
        WindowStyles.SetForeground(_hwnd);
        OsdHost.FocusVoiceInput();
    }

    /// <summary>
    /// Sesli komut onayından çıkıldığında odak çalmama stilini geri getirir ve önceki pencereyi ön plana alır.
    /// </summary>
    public void ReleaseVoiceConfirmation(nint restoreTo)
    {
        if (_hwnd != 0)
            WindowStyles.SetNoActivate(_hwnd, true);

        if (restoreTo != 0)
            WindowStyles.SetForeground(restoreTo);
    }

    // Dosya seçici gibi diyaloglara sahip olabilmek için çentik geçici etkinleştirilir (kullanıcı az önce tıkladı,
    // Windows ön plana almaya izin verir); sahipli diyalog çentiğin üstünde açılır. Diyalog kapanıp pencere
    // etkinliğini kaybedince OnDeactivated odak çalmamayı geri getirir.
    private Window? PrepareDialogOwner()
    {
        if (_hwnd == 0)
            return null;

        WindowStyles.SetNoActivate(_hwnd, false);
        Activate();
        return this;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (_hwnd != 0)
            WindowStyles.SetNoActivate(_hwnd, true);

        // Odak başka yere gidince arama kapanır; iğnelendiyse açık kalır. Odak zaten gitti, geri çekilmez.
        if (ViewModel is { IsSearchOpen: true, Search.IsPinned: false } viewModel)
            viewModel.CloseSearch(restoreFocus: false);
    }

    /// <summary>
    /// Arama kutusuna klavye odağını verir. Çentik normalde odak almaz; kısayola basıldığı için Windows öne geçmeye
    /// izin verir, kutu düzen bittikten sonra odaklanır (görünür olmayan denetim odak alamaz).
    /// </summary>
    public void FocusSearch()
    {
        if (_hwnd == 0)
            return;

        WindowStyles.SetNoActivate(_hwnd, false);
        Activate();
        WindowStyles.SetForeground(_hwnd);
        Dispatcher.BeginInvoke(SearchHost.FocusBox, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnSearchClosed(nint restoreTo)
    {
        if (_hwnd != 0)
            WindowStyles.SetNoActivate(_hwnd, true);
        if (restoreTo != 0)
            WindowStyles.SetForeground(restoreTo);
    }

    private void Notch_MouseEnter(object sender, MouseEventArgs e)
    {
        ViewModel?.Controller.OnMouseEnter();
        ViewModel?.Visibility.SetMouseOver(true);
        ViewModel?.Visibility.SetMouseAtEdge(false);
    }

    private void Notch_MouseLeave(object sender, MouseEventArgs e)
    {
        ViewModel?.Controller.OnMouseLeave();
        ViewModel?.Visibility.SetMouseOver(false);
        ViewModel?.Visibility.SetMouseAtEdge(false);
    }

    private void Notch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            var button = FindParent<Button>(dep);
            var progress = FindParent<ProgressBar>(dep);
            var slider = FindParent<Slider>(dep);
            var textBox = FindParent<TextBox>(dep);
            if (button is not null || progress is not null || slider is not null || textBox is not null)
                return;
        }

        ViewModel?.Controller.OnClick();
    }

    private void Notch_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Arama açıkken tekerlek sonuç listesini kaydırır, sekme ya da ses değiştirmez.
        if (ViewModel is null || ViewModel.IsSearchOpen)
            return;

        // Tekerlek eylemi ayardan: panel değiştir ya da sistem sesi (yukarı = yükselt).
        if (ViewModel.Settings.Notch.WheelAction == WheelAction.Volume)
        {
            if (e.Delta != 0)
                ViewModel.ControlCenter.AdjustVolume(e.Delta > 0 ? WheelVolumeStepPercent : -WheelVolumeStepPercent);
        }
        else if (e.Delta < 0)
            ViewModel.NextPanelCommand.Execute(null);
        else if (e.Delta > 0)
            ViewModel.PreviousPanelCommand.Execute(null);
        e.Handled = true;
    }

    private void Notch_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text))
        {
            e.Effects = DragDropEffects.Copy;
            ViewModel?.ExpandToShelf();
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Notch_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text))
        {
            e.Effects = DragDropEffects.Copy;
            if (ViewModel is not null && (ViewModel.State != Core.Notch.NotchState.Expanded || ViewModel.CurrentPanel != Core.Notch.NotchPanel.Shelf))
            {
                ViewModel.ExpandToShelf();
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Notch_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is null)
            return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                ViewModel.Shelf.AddFiles(files);
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.Text))
        {
            if (e.Data.GetData(DataFormats.Text) is string text)
            {
                ViewModel.Shelf.HandleDrop(null, text);
            }
        }

        e.Handled = true;
    }

    private void AnimatePanelTransition(int direction)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => AnimatePanelTransition(direction));
            return;
        }

        // Pil tasarrufu, Windows "animasyonları azalt" ya da Animasyon: Kapalı iken panel anında değişir.
        if (ViewModel?.ReduceMotion == true || !Motion.Enabled)
        {
            _panelTranslateX.SnapTo(0);
            _panelOpacity.SnapTo(1);
            PanelTranslate.X = 0;
            BodyHost.Opacity = 1;
            return;
        }

        _panelTimeScale = 1.0 / Motion.Scale;
        _panelTranslateX.SnapTo(direction > 0 ? 35 : -35);
        _panelTranslateX.Target = 0;
        _panelOpacity.SnapTo(0.25);
        _panelOpacity.Target = 1.0;
        WakeAnimation();
    }

    private void OnShakeRequested()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(OnShakeRequested);
            return;
        }

        _translateY.SnapTo(8);
        _translateY.Target = 0;
        WakeAnimation();
    }

    private void AnimateVisibility(bool isVisible)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => AnimateVisibility(isVisible));
            return;
        }

        _isNotchVisible = isVisible;
        if (isVisible)
        {
            if (_hwnd != 0)
            {
                WindowStyles.SetClickThrough(_hwnd, false);
                WindowStyles.ReassertTopmost(_hwnd);
            }

            if (ViewModel?.ReduceMotion == true)
                _translateY.SnapTo(0);
            else
                _translateY.Target = 0;
        }
        else
        {
            var hideOffset = Notch.ActualHeight > 0 ? -(Notch.ActualHeight + 20) : -150;
            if (ViewModel?.ReduceMotion == true)
                _translateY.SnapTo(hideOffset);
            else
                _translateY.Target = hideOffset;
        }

        WakeAnimation();
    }

    private void AnimateTo(double width, double height)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => AnimateTo(width, height));
            return;
        }

        if (ViewModel?.ReduceMotion == true)
        {
            // Pil tasarrufu: yay animasyonu yok, şekil tek karede yeni boyuta geçer.
            _width.SnapTo(width);
            _height.SnapTo(height);
            _cornerRadius.SnapTo(ViewModel.TargetCornerRadius);
        }
        else
        {
            _width.Target = width;
            _height.Target = height;
            if (ViewModel is not null)
            {
                _cornerRadius.Target = ViewModel.TargetCornerRadius;
            }
        }
        WakeAnimation();
    }

    private void WakeAnimation()
    {
        if (_animating)
            return;

        // Kareler yalnız animasyon sürerken dinlenir; boştayken CPU harcanmaz (AGENTS.md #6).
        _animating = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        var dt = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        if (dt <= 0)
            return;

        var moving = _width.Step(dt) | _height.Step(dt) | _cornerRadius.Step(dt) | _translateY.Step(dt) | _panelTranslateX.Step(dt * _panelTimeScale) | _panelOpacity.Step(dt * _panelTimeScale);
        Notch.Width = _width.Value;
        Notch.Height = _height.Value;
        Notch.CornerRadius = _cornerRadius.Value;
        NotchTranslate.Y = _translateY.Value;
        PanelTranslate.X = _panelTranslateX.Value;
        BodyHost.Opacity = _panelOpacity.Value;

        if (moving)
            return;

        CompositionTarget.Rendering -= OnRendering;
        _animating = false;

        if (!_isNotchVisible && _hwnd != 0)
        {
            WindowStyles.SetClickThrough(_hwnd, true);
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
