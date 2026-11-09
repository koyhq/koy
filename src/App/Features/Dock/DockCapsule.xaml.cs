using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Koy.App.Features.Dock;

/// <summary>Yüzen kapsül dock yüzeyi. Öğe şeridi ve etkileşimi <see cref="DockItemsStrip"/>'tedir.</summary>
public partial class DockCapsule : UserControl
{
    public DockCapsule()
    {
        InitializeComponent();
    }

    // Olay işlenmiş sayılır: yoksa pencerenin kendi sağ tık menüsü (Ayarlar) da açılırdı.
    private void OnStartRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        DockStartMenu.Open(DataContext as DockViewModel, (FrameworkElement)sender);
    }
}
