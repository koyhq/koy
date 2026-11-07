using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock'taki uygulama ve klasör şeridi (yüzen kapsül ve tam genişlik paylaşır). Tıklama, sürükleyerek sıralama,
/// sağ tık menüsü, orta tık ve fare üstü işleyicileri tek yerdedir; şablonlar (göstergeler, rozet, dikkat) tek kaynaktır.
/// </summary>
public partial class DockItemsStrip : UserControl
{
    private DockViewModel? ViewModel => DataContext as DockViewModel;

    private readonly DockReorderHelper _reorder;

    public DockItemsStrip()
    {
        _reorder = new DockReorderHelper(() => ViewModel);
        InitializeComponent();
    }

    private void OnItemLeftDown(object sender, MouseButtonEventArgs e) => _reorder.OnMouseDown(sender, e);

    private void OnItemMouseMove(object sender, MouseEventArgs e) => _reorder.OnMouseMove(sender, e);

    private void OnItemLostCapture(object sender, MouseEventArgs e) => _reorder.OnLostCapture();

    private void OnItemLeftClick(object sender, MouseButtonEventArgs e)
    {
        // Sürükleyerek sıralama bittiyse bu bırakma tıklama sayılmaz.
        if (_reorder.TryFinish())
        {
            e.Handled = true;
            return;
        }

        if (sender is FrameworkElement { DataContext: DockItemViewModel item })
        {
            ViewModel?.ClosePreviewNow();
            item.ClickCommand.Execute(null);
        }
    }

    private void OnFolderLeftClick(object sender, MouseButtonEventArgs e)
    {
        if (_reorder.TryFinish())
        {
            e.Handled = true;
            return;
        }

        if (sender is FrameworkElement { DataContext: DockFolderViewModel folder })
        {
            ViewModel?.ClosePreviewNow();
            folder.ClickCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnFolderRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DockFolderViewModel folder } element)
        {
            ViewModel?.ClosePreviewNow();
            DockContextMenuManager.OpenFolderContextMenu(folder, element);
            e.Handled = true;
        }
    }

    private void OnItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle &&
            sender is FrameworkElement { DataContext: DockItemViewModel item })
        {
            ViewModel?.ClosePreviewNow();
            // Ayar: 0 = yeni pencere, 1 = kapat
            if (ViewModel?.Settings.MiddleClickAction == 1)
                item.CloseCommand.Execute(null);
            else
                item.MiddleClickCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnItemContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true; // Menü açılışını OnItemRightClick ve DockContextMenuManager yönetir
    }

    private void OnItemRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.ContextMenu is not null)
        {
            ViewModel?.ClosePreviewNow();
            DockContextMenuManager.Open(element.ContextMenu, element);
            e.Handled = true;
        }
    }

    private void OnItemMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element &&
            element.DataContext is DockEntryViewModel entry &&
            ViewModel is not null)
        {
            var index = ViewModel.Items.IndexOf(entry);
            if (index >= 0)
            {
                ViewModel.OnItemHover(index);
            }

            if (entry is DockItemViewModel item)
            {
                ViewModel.OnItemMouseEnter(item, element);
            }
        }
    }

    private void OnItemMouseLeave(object sender, MouseEventArgs e)
    {
        _reorder.OnMouseLeave();
        ViewModel?.OnItemMouseLeave();
        ViewModel?.OnHoverEnded();
    }
}
