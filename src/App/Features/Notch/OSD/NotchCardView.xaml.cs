using System.Windows.Controls;
using System.Windows.Input;

namespace Koy.App.Features.Notch.OSD;

public partial class NotchCardView : UserControl
{
    public NotchCardView()
    {
        InitializeComponent();
    }

    // Kartın üstüne basmak çentiği açmasın (çentiğin tıklama işleyicisine ulaşmaz).
    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void Action_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        (DataContext as OsdIndicatorViewModel)?.RequestPrimaryAction();
        e.Handled = true;
    }

    private void Dismiss_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        (DataContext as OsdIndicatorViewModel)?.RequestDismiss();
        e.Handled = true;
    }
}
