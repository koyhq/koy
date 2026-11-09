using Koy.Core.Search;
using Koy.Core.StartMenu;

namespace Koy.Core.Tests.StartMenu;

public class StartMenuModelTests
{
    private static List<StartMenuItemId> Flat(IReadOnlyList<StartMenuGroup> groups) =>
        groups.SelectMany(g => g.Items).Select(i => i.Id).ToList();

    [Fact]
    public void Layout_FollowsWindows11OrderAndGrouping()
    {
        var groups = StartMenuModel.Build(hasBattery: true);

        Assert.Equal(5, groups.Count);
        Assert.Equal(
            [
                StartMenuItemId.InstalledApps, StartMenuItemId.MobilityCenter, StartMenuItemId.PowerOptions,
                StartMenuItemId.EventViewer, StartMenuItemId.System, StartMenuItemId.DeviceManager,
                StartMenuItemId.NetworkConnections, StartMenuItemId.DiskManagement, StartMenuItemId.ComputerManagement
            ],
            groups[0].Items.Select(i => i.Id));
        Assert.Equal([StartMenuItemId.Terminal, StartMenuItemId.TerminalAdmin], groups[1].Items.Select(i => i.Id));
        Assert.Equal(
            [
                StartMenuItemId.TaskManager, StartMenuItemId.Settings, StartMenuItemId.FileExplorer,
                StartMenuItemId.Search, StartMenuItemId.Run
            ],
            groups[2].Items.Select(i => i.Id));
        Assert.Equal([StartMenuItemId.ShutDownOrSignOut], groups[3].Items.Select(i => i.Id));
        Assert.Equal([StartMenuItemId.Desktop], groups[4].Items.Select(i => i.Id));
    }

    [Fact]
    public void MobilityCenter_HiddenWithoutBattery_RestUnchanged()
    {
        var withBattery = Flat(StartMenuModel.Build(hasBattery: true));
        var without = Flat(StartMenuModel.Build(hasBattery: false));

        Assert.DoesNotContain(StartMenuItemId.MobilityCenter, without);
        Assert.Equal(withBattery.Where(id => id != StartMenuItemId.MobilityCenter), without);
    }

    [Fact]
    public void ShutDownOrSignOut_IsSubmenuWithSessionCommandsInOrder()
    {
        var parent = StartMenuModel.Build(true).SelectMany(g => g.Items).Single(i => i.Id == StartMenuItemId.ShutDownOrSignOut);

        Assert.Equal(StartMenuActionKind.Submenu, parent.Kind);
        Assert.NotNull(parent.Children);
        Assert.Equal(
            [StartMenuItemId.SignOut, StartMenuItemId.Sleep, StartMenuItemId.ShutDown, StartMenuItemId.Restart],
            parent.Children.Select(c => c.Id));
        Assert.Equal(
            [SystemCommand.SignOut, SystemCommand.Sleep, SystemCommand.Shutdown, SystemCommand.Restart],
            parent.Children.Select(c => c.Session!.Value));
        Assert.All(parent.Children, c => Assert.Equal(StartMenuActionKind.Session, c.Kind));
    }

    [Fact]
    public void ActionKinds_AndTargets()
    {
        var items = StartMenuModel.Build(true).SelectMany(g => g.Items).ToDictionary(i => i.Id);

        Assert.Equal(StartMenuActionKind.Terminal, items[StartMenuItemId.Terminal].Kind);
        Assert.Equal(StartMenuActionKind.TerminalAdmin, items[StartMenuItemId.TerminalAdmin].Kind);
        Assert.Equal(StartMenuActionKind.Search, items[StartMenuItemId.Search].Kind);
        Assert.Equal(StartMenuActionKind.Run, items[StartMenuItemId.Run].Kind);
        Assert.Equal(StartMenuActionKind.ShowDesktop, items[StartMenuItemId.Desktop].Kind);

        Assert.Equal("ms-settings:appsfeatures", items[StartMenuItemId.InstalledApps].Target);
        Assert.Equal("mblctr.exe", items[StartMenuItemId.MobilityCenter].Target);
        Assert.Equal("ms-settings:powersleep", items[StartMenuItemId.PowerOptions].Target);
        Assert.Equal("eventvwr.msc", items[StartMenuItemId.EventViewer].Target);
        Assert.Equal("ms-settings:about", items[StartMenuItemId.System].Target);
        Assert.Equal("devmgmt.msc", items[StartMenuItemId.DeviceManager].Target);
        Assert.Equal("ms-settings:network-status", items[StartMenuItemId.NetworkConnections].Target);
        Assert.Equal("diskmgmt.msc", items[StartMenuItemId.DiskManagement].Target);
        Assert.Equal("compmgmt.msc", items[StartMenuItemId.ComputerManagement].Target);
        Assert.Equal("taskmgr.exe", items[StartMenuItemId.TaskManager].Target);
        Assert.Equal("ms-settings:", items[StartMenuItemId.Settings].Target);
        Assert.Equal("explorer.exe", items[StartMenuItemId.FileExplorer].Target);
    }

    [Fact]
    public void EveryOpenTargetItem_HasTarget_AndIdsAreUnique()
    {
        var all = StartMenuModel.Build(true).SelectMany(g => g.Items)
            .SelectMany(i => i.Children is null ? [i] : new[] { i }.Concat(i.Children))
            .ToList();

        Assert.All(all.Where(i => i.Kind == StartMenuActionKind.OpenTarget), i => Assert.False(string.IsNullOrEmpty(i.Target)));
        Assert.Equal(all.Count, all.Select(i => i.Id).Distinct().Count());
        // Pilli makinede her kimlik menüde tam bir kez geçer.
        Assert.Equal(Enum.GetValues<StartMenuItemId>().Length, all.Count);
    }
}

public class StartMenuActionsTests
{
    private sealed class FakeHost : IStartMenuHost
    {
        public bool HasBattery { get; set; } = true;
        public bool ThrowOnBattery { get; set; }
        public bool ThrowOnLaunch { get; set; }
        public Dictionary<string, ToolLaunchResult> Results { get; } = new();
        public List<(string Target, bool Elevated)> Launches { get; } = [];
        public List<string> Calls { get; } = [];

        bool IStartMenuHost.HasBattery => ThrowOnBattery ? throw new InvalidOperationException("pil") : HasBattery;

        public ToolLaunchResult Launch(string target, string? arguments, bool elevated)
        {
            if (ThrowOnLaunch)
                throw new InvalidOperationException("boom");

            Launches.Add((target, elevated));
            return Results.GetValueOrDefault(target, ToolLaunchResult.Started);
        }

        public void ShowDesktop() => Calls.Add("desktop");
        public void OpenSearch() => Calls.Add("search");
        public void OpenRunDialog() => Calls.Add("run");
    }

    private sealed class FakeSession : ISystemCommandService
    {
        public List<SystemCommand> Executed { get; } = [];
        public void Execute(SystemCommand command) => Executed.Add(command);
    }

    private static StartMenuItem Item(StartMenuItemId id, bool battery = true) =>
        StartMenuModel.Build(battery).SelectMany(g => g.Items)
            .SelectMany(i => i.Children is null ? [i] : new[] { i }.Concat(i.Children))
            .Single(i => i.Id == id);

    [Fact]
    public void BuildMenu_UsesHostBatteryState()
    {
        var host = new FakeHost { HasBattery = false };
        var actions = new StartMenuActions(host, new FakeSession());

        Assert.DoesNotContain(StartMenuItemId.MobilityCenter, actions.BuildMenu().SelectMany(g => g.Items).Select(i => i.Id));

        host.HasBattery = true;
        Assert.Contains(StartMenuItemId.MobilityCenter, actions.BuildMenu().SelectMany(g => g.Items).Select(i => i.Id));
    }

    [Fact]
    public void BuildMenu_BatteryQueryFailure_StillBuildsMenuWithoutMobilityCenter()
    {
        var errors = new List<string>();
        var actions = new StartMenuActions(new FakeHost { ThrowOnBattery = true }, new FakeSession(), (s, _) => errors.Add(s));

        var ids = actions.BuildMenu().SelectMany(g => g.Items).Select(i => i.Id).ToList();

        Assert.DoesNotContain(StartMenuItemId.MobilityCenter, ids);
        Assert.Contains(StartMenuItemId.Desktop, ids);
        Assert.Single(errors);
    }

    [Fact]
    public void OpenTarget_LaunchesTargetNotElevated()
    {
        var host = new FakeHost();
        new StartMenuActions(host, new FakeSession()).Execute(Item(StartMenuItemId.DeviceManager));

        Assert.Equal([("devmgmt.msc", false)], host.Launches);
    }

    [Fact]
    public void Terminal_PrefersWindowsTerminal()
    {
        var host = new FakeHost();
        new StartMenuActions(host, new FakeSession()).Execute(Item(StartMenuItemId.Terminal));

        Assert.Equal([("wt.exe", false)], host.Launches);
    }

    [Theory]
    [InlineData(ToolLaunchResult.NotFound)]
    [InlineData(ToolLaunchResult.Failed)]
    public void Terminal_FallsBackToPowerShell_WhenWindowsTerminalUnavailable(ToolLaunchResult result)
    {
        var host = new FakeHost();
        host.Results["wt.exe"] = result;
        new StartMenuActions(host, new FakeSession()).Execute(Item(StartMenuItemId.Terminal));

        Assert.Equal([("wt.exe", false), ("powershell.exe", false)], host.Launches);
    }

    [Fact]
    public void TerminalAdmin_RunsElevated_AndFallsBackElevated()
    {
        var host = new FakeHost();
        host.Results["wt.exe"] = ToolLaunchResult.NotFound;
        new StartMenuActions(host, new FakeSession()).Execute(Item(StartMenuItemId.TerminalAdmin));

        Assert.Equal([("wt.exe", true), ("powershell.exe", true)], host.Launches);
    }

    [Fact]
    public void TerminalAdmin_DeclinedUac_IsSilent_NoFallbackNoError()
    {
        var host = new FakeHost();
        host.Results["wt.exe"] = ToolLaunchResult.Declined;
        var errors = new List<string>();

        new StartMenuActions(host, new FakeSession(), (s, _) => errors.Add(s)).Execute(Item(StartMenuItemId.TerminalAdmin));

        Assert.Equal([("wt.exe", true)], host.Launches);
        Assert.Empty(errors);
    }

    [Fact]
    public void SessionCommands_GoToSessionService_NotToHost()
    {
        var host = new FakeHost();
        var session = new FakeSession();
        var actions = new StartMenuActions(host, session);

        actions.Execute(Item(StartMenuItemId.SignOut));
        actions.Execute(Item(StartMenuItemId.Sleep));
        actions.Execute(Item(StartMenuItemId.ShutDown));
        actions.Execute(Item(StartMenuItemId.Restart));

        Assert.Equal([SystemCommand.SignOut, SystemCommand.Sleep, SystemCommand.Shutdown, SystemCommand.Restart], session.Executed);
        Assert.Empty(host.Launches);
        Assert.Empty(host.Calls);
    }

    [Fact]
    public void ShellShortcuts_DesktopSearchRun()
    {
        var host = new FakeHost();
        var actions = new StartMenuActions(host, new FakeSession());

        actions.Execute(Item(StartMenuItemId.Desktop));
        actions.Execute(Item(StartMenuItemId.Search));
        actions.Execute(Item(StartMenuItemId.Run));

        Assert.Equal(["desktop", "search", "run"], host.Calls);
    }

    [Fact]
    public void Submenu_ParentDoesNothing()
    {
        var host = new FakeHost();
        var session = new FakeSession();
        new StartMenuActions(host, session).Execute(Item(StartMenuItemId.ShutDownOrSignOut));

        Assert.Empty(host.Launches);
        Assert.Empty(host.Calls);
        Assert.Empty(session.Executed);
    }

    [Fact]
    public void LaunchException_NeverEscapes_AndIsReported()
    {
        var errors = new List<string>();
        var actions = new StartMenuActions(new FakeHost { ThrowOnLaunch = true }, new FakeSession(), (s, _) => errors.Add(s));

        actions.Execute(Item(StartMenuItemId.TaskManager));
        actions.Execute(Item(StartMenuItemId.Terminal));

        Assert.Equal(2, errors.Count);
    }
}
