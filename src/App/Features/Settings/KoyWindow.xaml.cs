using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Koy.App.Resources;

namespace Koy.App.Features.Settings;

public partial class KoyWindow : Window
{
    private static KoyWindow? _instance;

    public static KoyWindow Instance => _instance ??= new KoyWindow();

    /// <summary>KoyWindow örneğinin bellekte oluşturulup oluşturulmadığını belirtir.</summary>
    public static bool IsCreated => _instance is not null;

    /// <summary>
    /// Sağ alt menü veya kısayol üzerinden değişen özellikleri açık/gizli KoyWindow örneğine iletir.
    /// Pencere henüz oluşturulmamışsa hiçbir şey yapmaz.
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

        _instance.ViewModel.SyncExternalState(notchEnabled, dockEnabled);
    }

    public KoyWindowViewModel ViewModel => (KoyWindowViewModel)DataContext;

    public KoyWindow(KoyWindowViewModel? viewModel = null)
    {
        InitializeComponent();
        DataContext = viewModel ?? new KoyWindowViewModel();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>Sayfa değişince yeni sayfa kısa bir solmayla girer; hareket kapalıysa anında geçer.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // ThemeManager önce uygulansın (Current teması ve yüzeyler), çerçeve ardından.
        if (e.PropertyName is nameof(KoyWindowViewModel.Material) or nameof(KoyWindowViewModel.MaterialReadabilityGuard)
            or nameof(KoyWindowViewModel.Theme) or nameof(KoyWindowViewModel.CornerRadius))
            Dispatcher.BeginInvoke(DispatcherPriority.Background, ApplyFrame);

        if (e.PropertyName == nameof(KoyWindowViewModel.IsUninstallConfirming) && ViewModel.IsUninstallConfirming && IsVisible)
        {
            // Kart solarak girer; odak güvenli seçeneğe (İptal) verilir.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                Motion.PageEnter(UninstallCard);
                UninstallCancelButton.Focus();
            });
            return;
        }

        if (e.PropertyName == nameof(KoyWindowViewModel.IsUpdateOverlayOpen) && ViewModel.IsUpdateOverlayOpen && IsVisible)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                Motion.PageEnter(UpdateCard);
            });
            return;
        }

        if (e.PropertyName == nameof(KoyWindowViewModel.IsWhatsNewOverlayOpen) && ViewModel.IsWhatsNewOverlayOpen && IsVisible)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                Motion.PageEnter(WhatsNewCard);
            });
            return;
        }

        if (e.PropertyName != nameof(KoyWindowViewModel.SelectedPageIndex) || !IsVisible)
            return;

        // Hemen başlatılır: Loaded önceliğine ertelenince yeni sayfa bir kare tam opak çizilip sonra sönüyordu (titreme).
        var index = ViewModel.SelectedPageIndex;
        if (index >= 0 && index < PagesHost.Children.Count && PagesHost.Children[index] is FrameworkElement page)
            Motion.PageEnter(page);
    }

    public static void ShowInstance(int pageIndex = 0)
    {
        var win = Instance;
        win.ViewModel.LoadFromSettings();
        win.ViewModel.SelectPage(pageIndex);
        win.ContentScrollViewer?.ScrollToTop();

        if (win.WindowState == WindowState.Minimized)
        {
            win.WindowState = WindowState.Normal;
        }

        win.Show();
        win.Topmost = true;
        win.Activate();
        win.Topmost = false;
        win.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Katmansız pencerede WPF yüzeyi siyahla temizler; DWM arka planı görünsün diye saydam temizlenir.
        if (PresentationSource.FromVisual(this) is System.Windows.Interop.HwndSource source && source.CompositionTarget is not null)
            source.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
        ApplyFrame();
        MaterialState.Changed += ApplyFrame;
        Closed += (_, _) => MaterialState.Changed -= ApplyFrame;
    }

    /// <summary>
    /// Pencere çerçevesi DWM'den: Buzlu ve Cam'da sistem akriliği (gerçek bulanıklık; ThemeManager'ın yarı saydam
    /// yüzeyleri onun üstünde ton verir), Katı'da arka plan yok. Koyu / açık çerçeve temaya, köşe "Köşe yuvarlaklığı"na
    /// uyar (Keskin küçük yuvarlak, diğerleri Windows'un standart yuvarlağı). Tema, malzeme, köşe ya da duvar kağıdı
    /// değişince yeniden uygulanır.
    /// </summary>
    private void ApplyFrame()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == 0)
            return;

        var settings = new Koy.Core.Settings.AppSettings
        {
            Material = ViewModel.Material,
            MaterialReadabilityGuard = ViewModel.MaterialReadabilityGuard
        };
        var level = MaterialState.ForWindow(settings, ThemeManager.Current);
        var sharp = Koy.Core.Appearance.CornerSetting.Resolve(ViewModel.CornerRadius) == Koy.Core.Appearance.CornerStyle.Sharp;
        Koy.Platform.Shell.BackdropService.ApplyWindowFrame(
            hwnd,
            level == Koy.Core.Appearance.MaterialSetting.Solid
                ? Koy.Platform.Shell.WindowBackdropType.None
                : Koy.Platform.Shell.WindowBackdropType.Acrylic,
            ThemeManager.Current == Koy.Core.Appearance.ResolvedTheme.Dark,
            sharp ? Koy.Platform.Shell.WindowCornerStyle.RoundSmall : Koy.Platform.Shell.WindowCornerStyle.Round);
    }

    private void KeepSettingsLabel_Click(object sender, MouseButtonEventArgs e)
    {
        // Kutunun kendisine tıklama zaten çevirir; yalnız metne tıklama buraya gelir.
        if (e.OriginalSource is System.Windows.Controls.TextBlock)
            ViewModel.KeepSettingsOnUninstall = !ViewModel.KeepSettingsOnUninstall;
    }

    private void Navigate(int index)
    {
        ViewModel.SelectPage(index);
        ContentScrollViewer?.ScrollToTop();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        SyncAndSaveOnClose();
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Pencereyi kapatmak Koy'u kapatmaz; gizler
        e.Cancel = true;
        SyncAndSaveOnClose();
        Hide();
    }

    private void SyncAndSaveOnClose()
    {
        if (AppUninstaller.InProgress)
            return;

        if (ViewModel.IsWhatsNewOverlayOpen)
        {
            ViewModel.CloseWhatsNewOverlay();
        }

        ViewModel.SyncFromDisk();
        ViewModel.SaveSettingsCommand.Execute(null);
    }

    private void NavGenel_Click(object sender, RoutedEventArgs e) => Navigate(0);
    private void NavCentik_Click(object sender, RoutedEventArgs e) => Navigate(1);
    private void NavDock_Click(object sender, RoutedEventArgs e) => Navigate(2);
    private void NavEkranlar_Click(object sender, RoutedEventArgs e) => Navigate(3);
    private void NavGorunum_Click(object sender, RoutedEventArgs e) => Navigate(4);
    private void NavKisayollar_Click(object sender, RoutedEventArgs e) => Navigate(5);
    private void NavPerformans_Click(object sender, RoutedEventArgs e) => Navigate(6);
    private void NavHakkinda_Click(object sender, RoutedEventArgs e) => Navigate(7);
}
