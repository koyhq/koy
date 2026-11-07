using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

public class DockSettingsDiffTests
{
    [Fact]
    public void Identical_settings_need_nothing()
        => Assert.True(DockSettingsDiff.Compare(new AppSettings(), new AppSettings()).IsEmpty);

    [Fact]
    public void Notch_only_change_needs_nothing()
        => Assert.True(DockSettingsDiff.Compare(new AppSettings(), new AppSettings { NotchVisibility = Koy.Core.Visibility.VisibilityMode.Smart }).IsEmpty);

    [Fact]
    public void Toggling_a_button_only_rebuilds_view_models()
    {
        var d = DockSettingsDiff.Compare(new AppSettings(), new AppSettings { DockShowRecents = false });

        Assert.True(d.RebuildViewModels);
        Assert.False(d.ReconcileAppBar);
        Assert.False(d.ReconcileTaskbar);
        Assert.False(d.ReconcileMonitors);
        Assert.False(d.ReconcileTray);
    }

    [Fact]
    public void Style_change_reconciles_appbar_but_never_the_taskbar()
    {
        var d = DockSettingsDiff.Compare(new AppSettings(), new AppSettings { DockStyle = DockStyleMode.FullWidth });

        Assert.True(d.RebuildViewModels);
        Assert.True(d.ReconcileAppBar);
        Assert.False(d.ReconcileTaskbar);
    }

    [Fact]
    public void Proximity_change_without_effective_change_keeps_the_appbar()
    {
        // Tam genişlikte Aşağı yapış ve Yer ayır aynı etkin modu verir: AppBar'a dokunulmaz.
        var a = new AppSettings { DockStyle = DockStyleMode.FullWidth, DockProximity = DockProximityMode.DockDown };
        var b = new AppSettings { DockStyle = DockStyleMode.FullWidth, DockProximity = DockProximityMode.ReserveSpace };

        Assert.False(DockSettingsDiff.Compare(a, b).ReconcileAppBar);
    }

    [Fact]
    public void Capsule_reserve_to_dockdown_reconciles_appbar()
        => Assert.True(DockSettingsDiff.Compare(
            new AppSettings { DockProximity = DockProximityMode.ReserveSpace },
            new AppSettings { DockProximity = DockProximityMode.DockDown }).ReconcileAppBar);

    [Fact]
    public void Hide_taskbar_toggle_reconciles_taskbar_and_tray()
    {
        var d = DockSettingsDiff.Compare(new AppSettings { HideWindowsTaskbar = true }, new AppSettings { HideWindowsTaskbar = false });

        Assert.True(d.ReconcileTaskbar);
        Assert.True(d.ReconcileTray);
    }

    [Fact]
    public void Monitor_changes_reconcile_monitors()
    {
        Assert.True(DockSettingsDiff.Compare(new AppSettings(), new AppSettings { NewScreenDefault = NewScreenMode.NotchOnly }).ReconcileMonitors);
        Assert.True(DockSettingsDiff.Compare(
            new AppSettings(),
            new AppSettings { Monitors = new() { ["a"] = new MonitorSettings { DockEnabled = false } } }).ReconcileMonitors);
    }

    [Fact]
    public void Battery_saver_toggle_reconciles_power_and_rebuilds()
    {
        var d = DockSettingsDiff.Compare(new AppSettings { BatterySaverReduceMotion = true }, new AppSettings { BatterySaverReduceMotion = false });

        Assert.True(d.ReconcilePower);
        Assert.True(d.RebuildViewModels);
    }
}
