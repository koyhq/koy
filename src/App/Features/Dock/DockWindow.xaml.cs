using System.Windows;
using Koy.Core.Diagnostics;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock;

public partial class DockWindow : Window
{
    private DockViewModel? ViewModel => DataContext as DockViewModel;
    private bool _isRenderingHooked;
    private TimeSpan _lastRenderTime;
    private DisplayMonitor? _targetMonitor;

    public DisplayMonitor? TargetMonitor => _targetMonitor;
    public nint Handle => new WindowInteropHelper(this).Handle;

    /// <summary>
    /// Çalışma alanından ayrılacak yükseklik:
    /// Yüzen dock için 60 px + 10 px alt boşluk; Tam genişlik için FullWidthHeightDip (48); Yer ayır için 46 px.
    /// DIP olarak döner; piksele çevirme tek kuralla (WindowPlacementMath.ToPixels) AppBar'da yapılır.
    /// </summary>
    public double ReservedHeight
    {
        get
        {
            if (ViewModel?.IsFullWidthMode == true)
                return DockPositionResolver.FullWidthHeightDip;
            if (ViewModel?.EffectiveProximity == DockProximityMode.ReserveSpace)
                return DockPositionResolver.SnappedHeightDip;
            return ActualHeight > 0 ? ActualHeight : 70;
        }
    }

    public uint WmShellHook { get; set; }
    public Action<nint, nint>? ShellHookHandler { get; set; }
    public Action? DisplayChangeHandler { get; set; }

    public DockWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        DpiChanged += (_, _) => Reposition(_targetMonitor);
        PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Right)
            {
                DockContextMenuManager.CloseActive();
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DockContextMenuManager.CloseActive();
                ViewModel?.ClosePreviewNow();
            }
        };
    }

    private const int WmDisplayChange = 0x007E;

    protected override void OnSourceInitialized(EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.ApplyToolWindowAndNoActivate(hwnd);

        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        // Önce hedef ekrana yerleş, sonra SourceInitialized abonelerini çalıştır: AppBar kaydı pencerenin
        // bulunduğu monitöre göre yapılır, yanlış ekranda yer ayrılmasın.
        Reposition(_targetMonitor);
        base.OnSourceInitialized(e);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // WndProc'tan sızan istisna süreci düşürür; logla ve yut.
        try
        {
            if (msg == WmDisplayChange)
            {
                DisplayChangeHandler?.Invoke();
                Reposition(_targetMonitor);
            }
            else if (WmShellHook != 0 && msg == WmShellHook)
            {
                ShellHookHandler?.Invoke(wParam, lParam);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("DockWindow.WndProc", ex);
        }
        return 0;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.AnimationNeeded += OnAnimationNeeded;
            ViewModel.AnimationStopped += OnAnimationStopped;
            ViewModel.RequestReposition += OnRequestReposition;
            ViewModel.SetMonitor(_targetMonitor);
        }

        Reposition(_targetMonitor);
    }

    private void OnRequestReposition()
    {
        Reposition(_targetMonitor);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel?.EvaluatePosition(ActualWidth);
        Reposition(_targetMonitor);
    }

    public void SetTargetMonitor(DisplayMonitor? monitor)
    {
        _targetMonitor = monitor;
        ViewModel?.SetMonitor(monitor);
        Reposition(monitor);
    }

    public void Reposition(DisplayMonitor? targetMonitor = null)
    {
        _targetMonitor = targetMonitor ?? _targetMonitor;

        var hwnd = Handle;
        if (hwnd != 0 && _targetMonitor is { PixelWidth: > 100 } monitor)
        {
            // Pencere oluşmuşsa konum hedef ekranın kendi ölçeğiyle pikselde verilir (karışık DPI'da DIP Left/Top yanıltır).
            // Pencere başka DPI'lı ekrandaysa önce yalnız taşınır; DpiChanged gelince tam yerleşir (boyut iki kez ölçeklenmesin).
            var sameDpi = Math.Abs(VisualTreeHelper.GetDpi(this).DpiScaleX - monitor.DpiScaleX) < 0.01;
            if (ViewModel?.IsFullWidthMode == true)
            {
                SizeToContent = SizeToContent.Manual;
                WindowStyles.SetBounds(hwnd, WindowPlacementMath.BottomFullWidth(monitor, DockPositionResolver.FullWidthHeightDip), resize: sameDpi);
            }
            else
            {
                SizeToContent = SizeToContent.WidthAndHeight;
                var margin = (ViewModel?.IsSnapped == true) ? 0.0 : 10.0;
                WindowStyles.SetBounds(hwnd, WindowPlacementMath.BottomCenter(monitor, ActualWidth, ActualHeight, margin), resize: false);
            }
            return;
        }

        if (ViewModel?.IsFullWidthMode == true)
        {
            SizeToContent = SizeToContent.Manual;
            var screenWidth = _targetMonitor?.WidthDip ?? SystemParameters.PrimaryScreenWidth;
            var screenHeight = _targetMonitor?.HeightDip ?? SystemParameters.PrimaryScreenHeight;
            var screenLeft = _targetMonitor?.LeftDip ?? 0.0;
            var screenTop = _targetMonitor?.TopDip ?? 0.0;

            Width = screenWidth;
            Height = DockPositionResolver.FullWidthHeightDip;
            Left = screenLeft;
            Top = screenTop + screenHeight - DockPositionResolver.FullWidthHeightDip;
            return;
        }

        SizeToContent = SizeToContent.WidthAndHeight;

        // Yüzen dock: 60 px yükseklik, alttan 10 px boşluk (aşağı yapışınca alttan 0 px)
        var bottomMargin = (ViewModel?.IsSnapped == true) ? 0.0 : 10.0;

        if (_targetMonitor is not null && _targetMonitor.WidthDip > 100)
        {
            Left = Math.Max(_targetMonitor.LeftDip, _targetMonitor.LeftDip + (_targetMonitor.WidthDip - ActualWidth) / 2);
            Top = Math.Max(_targetMonitor.TopDip, _targetMonitor.TopDip + _targetMonitor.HeightDip - ActualHeight - bottomMargin);
        }
        else
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            Left = Math.Max(0, (screenWidth - ActualWidth) / 2);
            Top = Math.Max(0, screenHeight - ActualHeight - bottomMargin);
        }
    }

    private void OnAnimationNeeded(object? sender, EventArgs e)
    {
        StartRendering();
    }

    private void OnAnimationStopped(object? sender, EventArgs e)
    {
        StopRendering();
    }

    private void StartRendering()
    {
        if (_isRenderingHooked)
            return;

        _isRenderingHooked = true;
        _lastRenderTime = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (!_isRenderingHooked)
            return;

        _isRenderingHooked = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
            return;

        if (_lastRenderTime == TimeSpan.Zero)
        {
            _lastRenderTime = args.RenderingTime;
            return;
        }

        var dt = (args.RenderingTime - _lastRenderTime).TotalSeconds;
        _lastRenderTime = args.RenderingTime;

        if (dt <= 0 || dt > 0.1)
            dt = 0.016;

        ViewModel?.UpdateSprings(dt);
    }

    private void OnSingleThumbnailClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.PreviewItem?.SingleWindow is not null)
        {
            ViewModel.PreviewItem.ActivateSpecificWindowCommand.Execute(ViewModel.PreviewItem.SingleWindow.Handle);
            ViewModel.ClosePreviewNow();
        }
    }

    private void OnGroupedThumbnailClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DockWindowEntry entry } && ViewModel?.PreviewItem is not null)
        {
            ViewModel.PreviewItem.ActivateSpecificWindowCommand.Execute(entry.Handle);
            ViewModel.ClosePreviewNow();
        }
    }

    private void OnPopupMouseEnter(object sender, MouseEventArgs e)
    {
        ViewModel?.OnPopupMouseEnter();
    }

    private void OnPopupMouseLeave(object sender, MouseEventArgs e)
    {
        ViewModel?.OnPopupMouseLeave();
    }

    private void OnDodgeHandleMouseEnter(object sender, MouseEventArgs e)
    {
        ViewModel?.SetMouseAtBottom(true);
    }

    private void OnDodgeHandleMouseLeave(object sender, MouseEventArgs e)
    {
        ViewModel?.SetMouseAtBottom(false);
    }

    private void OnWindowMouseEnter(object sender, MouseEventArgs e)
    {
        ViewModel?.SetMouseOverDock(true);
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        ViewModel?.SetMouseOverDock(false);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopRendering();
        if (ViewModel is not null)
        {
            ViewModel.AnimationNeeded -= OnAnimationNeeded;
            ViewModel.AnimationStopped -= OnAnimationStopped;
            ViewModel.RequestReposition -= OnRequestReposition;
        }
        base.OnClosed(e);
    }
}
