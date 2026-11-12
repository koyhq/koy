using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Koy.Core.Dock;

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

        if (sender is FrameworkElement { DataContext: DockItemViewModel item } element)
        {
            // "Önizlemeleri göster" eylemi: çalışan simgede kutucuk açılır; açılamazsa denetleyici varsayılan eyleme düşer.
            if (ViewModel is { } vm &&
                vm.Controller.LeftClickAction == DockLeftClickAction.ShowThumbnails &&
                item.Model.IsRunning &&
                vm.TryShowPreviewNow(item, element))
            {
                return;
            }

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

    private void OnExtraLeftClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DockExtraViewModel { IsInteractive: true } extra })
        {
            ViewModel?.ActivateExtra(extra);
            e.Handled = true;
        }
    }

    private void OnExtraRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: DockExtraViewModel extra } element && ViewModel is { } dock)
        {
            dock.ClosePreviewNow();
            DockContextMenuManager.Open(DockContextMenuManager.CreateExtraContextMenu(dock, extra), element);
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
            if (ViewModel?.Settings.Dock.MiddleClickAction == MiddleClickAction.Close)
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

    private async void OnItemRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu } element)
            return;

        e.Handled = true; // await'ten önce: sonraki işleyiciler olayı ikinci kez işlemesin
        ViewModel?.ClosePreviewNow();

        if (element.DataContext is not DockItemViewModel item)
        {
            DockContextMenuManager.Open(menu, element);
            return;
        }

        try
        {
            // Uygulamanın atlama listesi menüye eklenir; okuma ayrı iş parçacığında, UI beklemez.
            await DockJumpListMenu.OpenAsync(element, menu, item, ViewModel?.JumpLists);
        }
        catch
        {
            // Liste okunamasa da Koy'un kendi menüsü açılır.
            DockJumpListMenu.Clear(menu);
            DockContextMenuManager.Open(menu, element);
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
