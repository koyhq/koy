using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock;

/// <summary>
/// WS_EX_NOACTIVATE pencerelerinde ContextMenu'lerin dışarı tıklandığında veya
/// başka bir pencereye geçildiğinde otomatik kapanmasını yönetir.
/// </summary>
public static class DockContextMenuManager
{
    private static ContextMenu? _activeMenu;
    public static bool HasActiveMenu => _activeMenu is not null && _activeMenu.IsOpen;
    public static event EventHandler? ActiveMenuChanged;

    public static void Open(ContextMenu menu, FrameworkElement target)
    {
        CloseActive();

        _activeMenu = menu;
        menu.PlacementTarget = target;
        menu.StaysOpen = false;

        RoutedEventHandler? loadedHandler = null;
        loadedHandler = (s, e) =>
        {
            menu.Loaded -= loadedHandler;
            menu.Focus();
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
                _activeMenu = null;
                ActiveMenuChanged?.Invoke(null, EventArgs.Empty);
            }
        };
        menu.Closed += closedHandler;

        menu.IsOpen = true;
        ActiveMenuChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void CloseActive()
    {
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
