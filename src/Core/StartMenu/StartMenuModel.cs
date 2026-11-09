using Koy.Core.Search;

namespace Koy.Core.StartMenu;

/// <summary>Başlat düğmesi sağ tık menüsündeki (Win+X) öğeler. Ad, çeviri anahtarının sonudur.</summary>
public enum StartMenuItemId
{
    InstalledApps,
    MobilityCenter,
    PowerOptions,
    EventViewer,
    System,
    DeviceManager,
    NetworkConnections,
    DiskManagement,
    ComputerManagement,
    Terminal,
    TerminalAdmin,
    TaskManager,
    Settings,
    FileExplorer,
    Search,
    Run,
    ShutDownOrSignOut,
    SignOut,
    Sleep,
    ShutDown,
    Restart,
    Desktop
}

/// <summary>Öğe tıklanınca ne tür bir iş yapılır.</summary>
public enum StartMenuActionKind
{
    /// <summary><see cref="StartMenuItem.Target"/> kabuk üzerinden açılır (ms-settings:, .msc, .exe).</summary>
    OpenTarget,
    /// <summary>Windows Terminal; yoksa PowerShell.</summary>
    Terminal,
    /// <summary>Terminal, yönetici olarak (UAC istemi çıkar).</summary>
    TerminalAdmin,
    /// <summary>Oturum komutu (<see cref="StartMenuItem.Session"/>).</summary>
    Session,
    /// <summary>Alt menü açar; kendi başına iş yapmaz.</summary>
    Submenu,
    ShowDesktop,
    Search,
    Run
}

/// <summary>Menü öğesi. Hedefler saf metindir; Windows'ta çalıştırmak <see cref="IStartMenuHost"/>'un işi.</summary>
public sealed record StartMenuItem(
    StartMenuItemId Id,
    StartMenuActionKind Kind,
    string? Target = null,
    SystemCommand? Session = null,
    IReadOnlyList<StartMenuItem>? Children = null);

/// <summary>Ayırıcıyla ayrılan öğe grubu.</summary>
public sealed record StartMenuGroup(IReadOnlyList<StartMenuItem> Items);

/// <summary>Windows 11'in Başlat sağ tık menüsünün sırası ve grupları.</summary>
public static class StartMenuModel
{
    /// <summary>Menüyü kurar. Mobilite Merkezi yalnız pili olan makinede çıkar (Windows da böyle yapar).</summary>
    public static IReadOnlyList<StartMenuGroup> Build(bool hasBattery)
    {
        var system = new List<StartMenuItem>
        {
            Open(StartMenuItemId.InstalledApps, "ms-settings:appsfeatures")
        };

        if (hasBattery)
            system.Add(Open(StartMenuItemId.MobilityCenter, "mblctr.exe"));

        system.AddRange(
        [
            // Windows 11'de Güç Seçenekleri, Sistem ve Ağ Bağlantıları Ayarlar'ın ilgili sayfasını açar.
            Open(StartMenuItemId.PowerOptions, "ms-settings:powersleep"),
            Open(StartMenuItemId.EventViewer, "eventvwr.msc"),
            Open(StartMenuItemId.System, "ms-settings:about"),
            Open(StartMenuItemId.DeviceManager, "devmgmt.msc"),
            Open(StartMenuItemId.NetworkConnections, "ms-settings:network-status"),
            Open(StartMenuItemId.DiskManagement, "diskmgmt.msc"),
            Open(StartMenuItemId.ComputerManagement, "compmgmt.msc")
        ]);

        var terminal = new List<StartMenuItem>
        {
            new(StartMenuItemId.Terminal, StartMenuActionKind.Terminal),
            new(StartMenuItemId.TerminalAdmin, StartMenuActionKind.TerminalAdmin)
        };

        var tools = new List<StartMenuItem>
        {
            Open(StartMenuItemId.TaskManager, "taskmgr.exe"),
            Open(StartMenuItemId.Settings, "ms-settings:"),
            Open(StartMenuItemId.FileExplorer, "explorer.exe"),
            new(StartMenuItemId.Search, StartMenuActionKind.Search),
            new(StartMenuItemId.Run, StartMenuActionKind.Run)
        };

        var session = new List<StartMenuItem>
        {
            new(StartMenuItemId.ShutDownOrSignOut, StartMenuActionKind.Submenu, Children:
            [
                SessionItem(StartMenuItemId.SignOut, SystemCommand.SignOut),
                SessionItem(StartMenuItemId.Sleep, SystemCommand.Sleep),
                SessionItem(StartMenuItemId.ShutDown, SystemCommand.Shutdown),
                SessionItem(StartMenuItemId.Restart, SystemCommand.Restart)
            ])
        };

        var desktop = new List<StartMenuItem> { new(StartMenuItemId.Desktop, StartMenuActionKind.ShowDesktop) };

        return
        [
            new(system), new(terminal), new(tools), new(session), new(desktop)
        ];
    }

    private static StartMenuItem Open(StartMenuItemId id, string target) =>
        new(id, StartMenuActionKind.OpenTarget, target);

    private static StartMenuItem SessionItem(StartMenuItemId id, SystemCommand command) =>
        new(id, StartMenuActionKind.Session, Session: command);
}
