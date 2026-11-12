using System.Windows;
using Koy.App.Features.Settings;
using Koy.Core.Display;
using Koy.Core.Settings;
using Koy.Platform.Display;
using Koy.Platform.Shell;

namespace Koy.App.Features.Menu;

public partial class SagAltMenuWindow : Window
{
    private static SagAltMenuWindow? _instance;

    public static SagAltMenuWindow Instance => _instance ??= new SagAltMenuWindow();

    /// <summary>SagAltMenuWindow örneğinin bellekte oluşturulup oluşturulmadığını belirtir.</summary>
    public static bool IsCreated => _instance is not null;

    /// <summary>
    /// Harici kaynaklardan (kısayollar vb.) değişen durumları açık/gizli SagAltMenuWindow örneğine iletir.
    /// </summary>
    public static void SyncFeatureStates(bool? notchEnabled = null, bool? dockEnabled = null)
    {
        if (_instance is null)
            return;

        if (!_instance.Dispatcher.CheckAccess())
        {
            _instance.Dispatcher.BeginInvoke(() => SyncFeatureStates(notchEnabled, dockEnabled));
            return;
        }

        if (notchEnabled.HasValue)
            _instance.ViewModel.NotchEnabled = notchEnabled.Value;
        if (dockEnabled.HasValue)
            _instance.ViewModel.DockEnabled = dockEnabled.Value;
    }

    public SagAltMenuViewModel ViewModel => (SagAltMenuViewModel)DataContext;

    public SagAltMenuWindow(SagAltMenuViewModel? viewModel = null)
    {
        InitializeComponent();
        // Gerçek bulanıklık, koyu çerçeve, gölge ve köşeler DWM'den (Koy penceresiyle aynı).
        Koy.App.Resources.WindowFrame.Attach(this);
        DataContext = viewModel ?? new SagAltMenuViewModel();

        ViewModel.RequestClose += () => Hide();
        ViewModel.RequestOpenKoyWindow += OnOpenKoyWindow;
        ViewModel.NotchToggled += OnNotchToggled;
        ViewModel.DockToggled += OnDockToggled;
        ViewModel.PauseToggled += OnPauseToggled;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionNearCursorOrBottomRight();
    }

    public static void Toggle()
    {
        var win = Instance;
        if (win.IsVisible)
        {
            win.Hide();
        }
        else
        {
            win.ShowMenu();
        }
    }

    public void ShowMenu()
    {
        ViewModel.Refresh();
        PositionNearCursorOrBottomRight();
        Show();
        Activate();
    }

    public void PositionNearCursorOrBottomRight()
    {
        // 1. Farenin fiziksel piksel pozisyonunu al
        bool hasCursor = CursorHelper.GetPosition(out var cursorX, out var cursorY);

        // 2. Monitörleri sorgula ve farenin bulunduğu monitörü bul
        var monitorService = new MonitorService();
        var monitors = monitorService.GetMonitors();

        DisplayMonitor? targetMonitor = null;
        if (hasCursor && monitors.Count > 0)
        {
            foreach (var m in monitors)
            {
                if (cursorX >= m.Bounds.Left && cursorX <= m.Bounds.Right &&
                    cursorY >= m.Bounds.Top && cursorY <= m.Bounds.Bottom)
                {
                    targetMonitor = m;
                    break;
                }
            }
        }

        targetMonitor ??= monitors.FirstOrDefault(m => m.IsPrimary) ?? (monitors.Count > 0 ? monitors[0] : null);

        // 3. Monitör DPI ve Çalışma Alanı (WPF DIP cinsinden)
        double dpiX = targetMonitor?.DpiScaleX ?? 1.0;
        double dpiY = targetMonitor?.DpiScaleY ?? 1.0;
        if (dpiX <= 0) dpiX = 1.0;
        if (dpiY <= 0) dpiY = 1.0;

        double workLeft, workRight, workTop, workBottom;
        if (targetMonitor is not null)
        {
            workLeft = targetMonitor.WorkArea.Left / dpiX;
            workRight = targetMonitor.WorkArea.Right / dpiX;
            workTop = targetMonitor.WorkArea.Top / dpiY;
            workBottom = targetMonitor.WorkArea.Bottom / dpiY;
        }
        else
        {
            var pwa = SystemParameters.WorkArea;
            workLeft = pwa.Left;
            workRight = pwa.Right;
            workTop = pwa.Top;
            workBottom = pwa.Bottom;
        }

        // 4. Pencere boyutları
        // WPF Window.Measure, HWND yokken GetWindowMinMax içinde FailFast ile süreci düşürür (kaydedilen 5.11 17:11 çökmesi).
        // Bu yüzden ölçmeden önce HWND'yi (göstermeden) oluştururuz; böylece ilk gösterimde de titremesiz konumlanır.
        // Koy ölçeği (issue #71): kök öğe ölçeklenir, pencere genişliği ona uyar. Menü küçük olduğu için ekrana sığdırma gerekmez.
        var scale = Koy.Core.Appearance.FontScalePolicy.ClampUiScale(Koy.App.Resources.ThemeManager.LastSettings?.Appearance.UiScalePercent
            ?? Koy.Core.Appearance.FontScalePolicy.DefaultUiScale) / 100.0;
        if (Content is FrameworkElement root)
            root.LayoutTransform = scale == 1.0 ? System.Windows.Media.Transform.Identity : new System.Windows.Media.ScaleTransform(scale, scale);
        double menuWidth = 320 * scale;
        Width = menuWidth;
        double menuHeight = 350 * scale;
        if (EnsureNativeWindow())
        {
            try
            {
                Measure(new Size(menuWidth, double.PositiveInfinity));
                if (DesiredSize.Height > 100)
                    menuHeight = DesiredSize.Height;
                else if (ActualHeight > 100)
                    menuHeight = ActualHeight;
            }
            catch (InvalidOperationException)
            {
                // Ölçülemezse tahmini yükseklikle devam; Loaded'da gerçek boyutla yeniden konumlanır.
            }
        }

        // 5. Hedef koordinatları belirle
        double targetLeft;
        double targetTop;

        if (hasCursor && targetMonitor is not null)
        {
            double mouseDipX = cursorX / dpiX;
            double mouseDipY = cursorY / dpiY;

            // Farenin bulunduğu yatay hizaya yerleştir (menünün sağ tarafı fareye yakın)
            targetLeft = mouseDipX - menuWidth + 24;

            // Dikey olarak farenin üstüne yerleştir (dock veya görev çubuğunun üstü)
            targetTop = mouseDipY - menuHeight - 10;
            if (targetTop < workTop + 8)
            {
                // Ekranın üstünden taşıyorsa farenin altına yerleştir
                targetTop = mouseDipY + 10;
            }
        }
        else
        {
            // Fallback: Çalışma alanının sağ alt köşesi
            targetLeft = workRight - menuWidth - 16;
            targetTop = workBottom - menuHeight - 16;
        }

        // 6. Monitör sınırları içine sabitle (Clamp) - ASLA diğer ekrana taşamaz veya ekran dışına çıkamaz
        targetLeft = Math.Clamp(targetLeft, workLeft + 8, workRight - menuWidth - 8);
        targetTop = Math.Clamp(targetTop, workTop + 8, workBottom - menuHeight - 8);

        Left = targetLeft;
        Top = targetTop;
    }

    /// <summary>HWND'yi göstermeden oluşturur; Measure/konumlama için gereklidir.</summary>
    private bool EnsureNativeWindow()
    {
        try
        {
            return new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle() != 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        Hide();
    }

    private static void OnOpenKoyWindow()
    {
        KoyWindow.ShowInstance();
    }

    private static void OnNotchToggled(bool enabled)
    {
        if (Application.Current is App app)
        {
            app.UpdateNotchState(enabled);
        }
    }

    private static void OnDockToggled(bool enabled)
    {
        if (Application.Current is App app)
        {
            app.UpdateDockState(enabled);
        }
    }

    private static void OnPauseToggled(bool paused)
    {
        if (Application.Current is App app)
        {
            app.SetPaused(paused);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Kapatmak yerine gizle
        e.Cancel = true;
        Hide();
    }
}
