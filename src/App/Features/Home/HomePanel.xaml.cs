using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Koy.App.Features.Home;

public partial class HomePanel : UserControl
{
    public HomePanel()
    {
        InitializeComponent();
    }

    private void ProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ProgressBar bar && DataContext is HomeViewModel vm)
        {
            var fraction = bar.ActualWidth > 0 ? e.GetPosition(bar).X / bar.ActualWidth : 0;
            vm.Music.SeekCommand.Execute(fraction);
            // Atlama tıklaması karta ulaşıp kartı büyütmesin, çentiğe ulaşıp onu kapatmasın.
            e.Handled = true;
        }
    }

    // Müzik kartına tıklanınca büyür / eski boyutuna döner. Düğme ve ilerleme çubuğu kendi işini yapar.
    // Olay işlenmiş sayılır: yoksa çentik penceresi tıklamayı "boşluğa tıklandı" sayıp çentiği kapatırdı.
    private void MusicCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HomeViewModel vm || !vm.ShowMusic)
            return;

        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, sender); node = ParentOf(node))
        {
            if (node is ButtonBase or RangeBase)
                return;
        }

        vm.ToggleMusicExpandedCommand.Execute(null);
        e.Handled = true;
    }

    private bool _isAddMenuHeld;

    private void AddButton_ContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (_isAddMenuHeld)
            return;

        if (DataContext is HomeViewModel vm)
        {
            _isAddMenuHeld = true;
            vm.QuickLaunch.HoldOpen?.Invoke(true);
        }
    }

    private void AddButton_ContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (!_isAddMenuHeld)
            return;

        _isAddMenuHeld = false;
        if (DataContext is HomeViewModel vm)
        {
            vm.QuickLaunch.HoldOpen?.Invoke(false);
        }
    }

    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}
