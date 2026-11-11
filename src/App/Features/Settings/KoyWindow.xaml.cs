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
        // Gerçek bulanıklık, koyu çerçeve ve köşeler DWM'den; tema / malzeme / köşe değişince kendiliğinden yenilenir.
        WindowFrame.Attach(this);
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
