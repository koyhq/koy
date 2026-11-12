using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using Koy.Core.Dock;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock;

/// <summary>
/// WS_EX_NOACTIVATE pencerelerinde ContextMenu'lerin dışarı tıklandığında veya
/// başka bir pencereye geçildiğinde otomatik kapanmasını yönetir.
/// </summary>
public static class DockContextMenuManager
{
    private static ContextMenu? _activeMenu;
    private static nint _activeMenuHwnd;
    private static OutsideClickHook? _outsideHook;

    public static bool HasActiveMenu => _activeMenu is not null && _activeMenu.IsOpen;
    public static bool IsMenuWindow(nint hwnd) => hwnd != 0 && hwnd == _activeMenuHwnd;
    public static event EventHandler? ActiveMenuChanged;

    public static void Open(ContextMenu menu, FrameworkElement target)
    {
        CloseActive();

        _activeMenu = menu;
        menu.PlacementTarget = target;
        ApplyPlacement(menu, target);
        menu.StaysOpen = false;

        RoutedEventHandler? loadedHandler = null;
        loadedHandler = (s, e) =>
        {
            menu.Loaded -= loadedHandler;
            menu.Focus();
            // Dock penceresi Topmost olduğundan açılan Popup'ın da z-sırasında en önde kalmasını sağla.
            if (PresentationSource.FromVisual(menu) is HwndSource source && source.Handle != 0)
            {
                _activeMenuHwnd = source.Handle;
                WindowStyles.ReassertTopmost(source.Handle);
            }
        };
        menu.Loaded += loadedHandler;

        MouseButtonEventHandler outsideClickHandler = (s, e) => CloseActive();
        Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(menu, outsideClickHandler);

        KeyEventHandler? keyHandler = null;
        keyHandler = (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CloseActive();
                e.Handled = true;
            }
        };
        menu.PreviewKeyDown += keyHandler;

        RoutedEventHandler? closedHandler = null;
        closedHandler = (s, e) =>
        {
            menu.Closed -= closedHandler;
            menu.PreviewKeyDown -= keyHandler;
            Mouse.RemovePreviewMouseDownOutsideCapturedElementHandler(menu, outsideClickHandler);
            if (_activeMenu == menu)
            {
                StopOutsideHook();
                _activeMenu = null;
                _activeMenuHwnd = 0;
                ActiveMenuChanged?.Invoke(null, EventArgs.Empty);
            }
        };
        menu.Closed += closedHandler;

        StartOutsideHook(menu);
        menu.IsOpen = true;
        ActiveMenuChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void StartOutsideHook(ContextMenu menu)
    {
        StopOutsideHook();
        var dispatcher = menu.Dispatcher;
        _outsideHook = new OutsideClickHook((x, y) =>
        {
            dispatcher.BeginInvoke(() =>
            {
                if (_activeMenu != menu || !menu.IsOpen)
                    return;

                if (!IsPointInsideMenuTree(menu, x, y))
                    CloseActive();
            });
        });
        _outsideHook.Start();
    }

    private static void StopOutsideHook()
    {
        _outsideHook?.Dispose();
        _outsideHook = null;
    }

    private static bool IsPointInsideMenuTree(ItemsControl container, int screenX, int screenY)
    {
        if (IsPointInsideVisual(container, screenX, screenY))
            return true;

        foreach (var item in container.Items)
        {
            if (item is MenuItem { IsSubmenuOpen: true } subMenu && IsPointInsideSubmenu(subMenu, screenX, screenY))
                return true;
        }

        return false;
    }

    private static bool IsPointInsideSubmenu(MenuItem parent, int screenX, int screenY)
    {
        foreach (var child in parent.Items)
        {
            if (child is FrameworkElement fe && fe.IsVisible)
            {
                if (fe.Parent is FrameworkElement popupRoot && IsPointInsideVisual(popupRoot, screenX, screenY))
                    return true;
                if (IsPointInsideVisual(fe, screenX, screenY))
                    return true;
            }

            if (child is MenuItem { IsSubmenuOpen: true } nested && IsPointInsideSubmenu(nested, screenX, screenY))
                return true;
        }

        return false;
    }

    private static bool IsPointInsideVisual(FrameworkElement element, int screenX, int screenY)
    {
        if (!element.IsVisible || PresentationSource.FromVisual(element) is null)
            return false;

        try
        {
            var local = element.PointFromScreen(new Point(screenX, screenY));
            return local.X >= 0 && local.Y >= 0
                && local.X <= element.ActualWidth
                && local.Y <= element.ActualHeight;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyPlacement(ContextMenu menu, FrameworkElement target)
    {
        if (target is Window || Window.GetWindow(target)?.DataContext is not DockViewModel vm)
            return;

        menu.Placement = vm.Edge switch
        {
            DockEdge.Top => PlacementMode.Bottom,
            DockEdge.Left => PlacementMode.Right,
            DockEdge.Right => PlacementMode.Left,
            _ => PlacementMode.Top
        };
        menu.VerticalOffset = vm.Edge switch
        {
            DockEdge.Top => 8,
            DockEdge.Bottom => -8,
            _ => 0
        };
        menu.HorizontalOffset = vm.Edge switch
        {
            DockEdge.Left => 8,
            DockEdge.Right => -8,
            _ => 0
        };
    }

    public static void CloseActive()
    {
        StopOutsideHook();
        _activeMenuHwnd = 0;

        if (_activeMenu is not null)
        {
            var menu = _activeMenu;
            _activeMenu = null;
            menu.IsOpen = false;
            ActiveMenuChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Klasör için sağ tık menüsü oluşturur: "Adını değiştir", "Klasörü dağıt".
    /// </summary>
    public static ContextMenu CreateFolderContextMenu(DockFolderViewModel folderVm)
    {
        var menu = new ContextMenu();

        var renameItem = new MenuItem
        {
            Header = Resources.Strings.Get("DockFolderRename")
        };
        renameItem.Click += (_, _) =>
        {
            CloseActive();
            folderVm.StartRename();
        };
        menu.Items.Add(renameItem);

        menu.Items.Add(new Separator());

        var dissolveItem = new MenuItem
        {
            Header = Resources.Strings.Get("DockFolderDissolve")
        };
        dissolveItem.Click += (_, _) =>
        {
            CloseActive();
            folderVm.DissolveCommand.Execute(null);
        };
        menu.Items.Add(dissolveItem);

        return menu;
    }

    /// <summary>Ek öğenin (ayırıcı, boşluk, saat, çöp kutusu, özel düğme) sağ tık menüsü: sola/sağa taşı, kaldır.</summary>
    public static ContextMenu CreateExtraContextMenu(DockViewModel dock, DockExtraViewModel extra)
    {
        var menu = new ContextMenu();

        void Add(string key, Action action)
        {
            var item = new MenuItem { Header = Resources.Strings.Get(key) };
            item.Click += (_, _) =>
            {
                CloseActive();
                action();
            };
            menu.Items.Add(item);
        }

        Add("DockItemMoveLeft", () => dock.MoveExtra(extra, -1));
        Add("DockItemMoveRight", () => dock.MoveExtra(extra, 1));
        menu.Items.Add(new Separator());
        Add("DockItemRemove", () => dock.RemoveExtra(extra));
        return menu;
    }

    /// <summary>
    /// Klasörün sağ tık menüsünü açar.
    /// </summary>
    public static void OpenFolderContextMenu(DockFolderViewModel folderVm, FrameworkElement target)
    {
        var menu = CreateFolderContextMenu(folderVm);
        Open(menu, target);
    }
}
