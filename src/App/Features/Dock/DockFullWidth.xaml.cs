using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Koy.App.Features.Dock;

/// <summary>Tam genişlik (Windows görev çubuğu tarzı) dock yüzeyi. Öğe şeridi ve etkileşimi <see cref="DockItemsStrip"/>'tedir.</summary>
public partial class DockFullWidth : UserControl
{
    private DockViewModel? ViewModel => DataContext as DockViewModel;

    public DockFullWidth()
    {
        InitializeComponent();
    }

    // Orta sütun/satırın gerçek boyutu ViewModel'e bildirilir: simgeler yan bölümlere taşmadan sığacak biçimde küçülür.
    private void OnCenterSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel is null) return;
        var dimension = ViewModel.IsVertical ? e.NewSize.Height : e.NewSize.Width;
        ViewModel.ReportCenterWidth(dimension);
    }

    // Olay işlenmiş sayılır: yoksa pencerenin kendi sağ tık menüsü (Ayarlar) da açılırdı.
    private void OnStartRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        DockStartMenu.Open(ViewModel, (FrameworkElement)sender);
    }

    private void OnNowPlayingClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.OpenNotchMusicCommand.Execute(null);
    }
}
