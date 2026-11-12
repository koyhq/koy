using Koy.Core.Appearance;
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
        => Assert.True(DockSettingsDiff.Compare(new AppSettings(), new AppSettings { Notch = new NotchSettings { Visibility = Koy.Core.Visibility.VisibilityMode.Smart } }).IsEmpty);

    [Fact]
    public void Toggling_a_button_only_rebuilds_view_models()
    {
        var d = DockSettingsDiff.Compare(new AppSettings(), new AppSettings { Dock = new DockSettings { ShowRecents = false } });

        Assert.True(d.RebuildViewModels);
        Assert.False(d.ReconcileAppBar);
        Assert.False(d.ReconcileTaskbar);
        Assert.False(d.ReconcileMonitors);
        Assert.False(d.ReconcileTray);
    }

    [Fact]
    public void Style_change_reconciles_appbar_but_never_the_taskbar()
    {
        var d = DockSettingsDiff.Compare(new AppSettings(), new AppSettings { Dock = new DockSettings { Style = DockStyleMode.FullWidth } });

        Assert.True(d.RebuildViewModels);
        Assert.True(d.ReconcileAppBar);
        Assert.False(d.ReconcileTaskbar);
    }

    [Fact]
    public void Proximity_change_without_effective_change_keeps_the_appbar()
    {
        // Tam genişlikte Aşağı yapış ve Yer ayır aynı etkin modu verir: AppBar'a dokunulmaz.
        var a = new AppSettings { Dock = new DockSettings { Style = DockStyleMode.FullWidth, Proximity = DockProximityMode.DockDown } };
        var b = new AppSettings { Dock = new DockSettings { Style = DockStyleMode.FullWidth, Proximity = DockProximityMode.ReserveSpace } };

        Assert.False(DockSettingsDiff.Compare(a, b).ReconcileAppBar);
    }

    [Fact]
    public void Capsule_reserve_to_dockdown_reconciles_appbar()
        => Assert.True(DockSettingsDiff.Compare(
            new AppSettings { Dock = new DockSettings { Proximity = DockProximityMode.ReserveSpace } },
            new AppSettings { Dock = new DockSettings { Proximity = DockProximityMode.DockDown } }).ReconcileAppBar);

    [Fact]
    public void Capsule_reserve_to_always_floating_releases_the_appbar()
    {
        var d = DockSettingsDiff.Compare(
            new AppSettings { Dock = new DockSettings { Proximity = DockProximityMode.ReserveSpace } },
            new AppSettings { Dock = new DockSettings { Proximity = DockProximityMode.AlwaysFloating } });

        Assert.True(d.ReconcileAppBar);
        Assert.True(d.RebuildViewModels);
        Assert.False(d.ReconcileTaskbar);
    }

    [Fact]
    public void Dockdown_to_always_floating_keeps_the_appbar_but_rebuilds_view_models()
    {
        // İkisi de yer ayırmaz: AppBar'a dokunulmaz, ama yakınlık ayarı ViewModel'e okunur, yeniden kurulur.
        var d = DockSettingsDiff.Compare(
            new AppSettings { Dock = new DockSettings { Proximity = DockProximityMode.DockDown } },
            new AppSettings { Dock = new DockSettings { Proximity = DockProximityMode.AlwaysFloating } });

        Assert.False(d.ReconcileAppBar);
        Assert.True(d.RebuildViewModels);
    }

    [Fact]
    public void Full_width_always_floating_maps_to_reserve_space_so_the_appbar_stays()
    {
        // Tam genişlikte Hep havada etkin olarak Yer ayır'dır: Yer ayır'dan geçişte AppBar'a dokunulmaz.
        var a = new AppSettings { Dock = new DockSettings { Style = DockStyleMode.FullWidth, Proximity = DockProximityMode.AlwaysFloating } };
        var b = new AppSettings { Dock = new DockSettings { Style = DockStyleMode.FullWidth, Proximity = DockProximityMode.ReserveSpace } };

        Assert.False(DockSettingsDiff.Compare(a, b).ReconcileAppBar);
    }

    [Fact]
    public void Hide_taskbar_toggle_reconciles_taskbar_and_tray()
    {
        var d = DockSettingsDiff.Compare(new AppSettings { Dock = new DockSettings { HideWindowsTaskbar = true } }, new AppSettings { Dock = new DockSettings { HideWindowsTaskbar = false } });

        Assert.True(d.ReconcileTaskbar);
        Assert.True(d.ReconcileTray);
        Assert.True(d.ReconcileAppBar);   // alt kenar sahipliği değişir: AppBar yeniden iddia edilmeli
    }

    [Fact]
    public void Monitor_changes_reconcile_monitors()
    {
        Assert.True(DockSettingsDiff.Compare(new AppSettings(), new AppSettings { Displays = new DisplaysSettings { NewScreenDefault = NewScreenMode.NotchOnly } }).ReconcileMonitors);
        Assert.True(DockSettingsDiff.Compare(
            new AppSettings(),
            new AppSettings { Displays = new DisplaysSettings { Monitors = new() { ["a"] = new MonitorSettings { DockEnabled = false } } } }).ReconcileMonitors);
    }

    [Fact]
    public void Tray_only_change_reconciles_tray_and_rebuilds_without_touching_taskbar_or_appbar()
    {
        var d = DockSettingsDiff.Compare(new AppSettings { Dock = new DockSettings { ShowTray = true } }, new AppSettings { Dock = new DockSettings { ShowTray = false } });

        Assert.True(d.ReconcileTray);
        Assert.True(d.RebuildViewModels);
        Assert.False(d.ReconcileTaskbar);
        Assert.False(d.ReconcileAppBar);
    }

    [Theory]
    [InlineData("Material")]
    [InlineData("AnimationSpeed")]
    public void Appearance_changes_only_rebuild_view_models(string field)
    {
        var after = new AppSettings();
        switch (field)
        {
            case "Material": after.Appearance.Material = MaterialLevel.Frosted; break;
            default: after.Appearance.AnimationSpeed = AnimationSpeed.Fast; break;
        }

        var d = DockSettingsDiff.Compare(new AppSettings(), after);

        Assert.True(d.RebuildViewModels);
        Assert.False(d.ReconcileAppBar);
        Assert.False(d.ReconcileMonitors);
        Assert.False(d.ReconcileTaskbar);
    }

    [Fact]
    public void Accent_color_does_not_touch_the_dock_it_updates_live_through_dynamic_resources()
        => Assert.True(DockSettingsDiff.Compare(new AppSettings(), new AppSettings { Appearance = new AppearanceSettings { AccentColor = "#FF0000", UseWindowsAccent = true } }).IsEmpty);

    [Fact]
    public void Monitor_change_reconciles_monitors_only()
    {
        var d = DockSettingsDiff.Compare(new AppSettings(), new AppSettings { Displays = new DisplaysSettings { Monitors = new() { ["x"] = new MonitorSettings { DockEnabled = false } } } });

        Assert.True(d.ReconcileMonitors);
        Assert.False(d.RebuildViewModels);
        Assert.False(d.ReconcileAppBar);
    }

    [Fact]
    public void Reserve_to_dodge_removes_the_appbar()
    {
        var d = DockSettingsDiff.Compare(
            new AppSettings { Dock = new DockSettings { Proximity = DockProximityMode.ReserveSpace } },
            new AppSettings { Dock = new DockSettings { Proximity = DockProximityMode.Dodge } });

        Assert.True(d.ReconcileAppBar);
        Assert.True(d.RebuildViewModels);
    }

    [Fact]
    public void Battery_saver_toggle_reconciles_power_and_rebuilds()
    {
        var d = DockSettingsDiff.Compare(new AppSettings { Appearance = new AppearanceSettings { BatterySaverReduceMotion = true } }, new AppSettings { Appearance = new AppearanceSettings { BatterySaverReduceMotion = false } });

        Assert.True(d.ReconcilePower);
        Assert.True(d.RebuildViewModels);
    }

    [Fact]
    public void Edge_change_reconciles_appbar_and_rebuilds()
    {
        var d = DockSettingsDiff.Compare(
            new AppSettings { Dock = new DockSettings { Edge = DockEdge.Bottom } },
            new AppSettings { Dock = new DockSettings { Edge = DockEdge.Top } });

        Assert.True(d.ReconcileAppBar);
        Assert.True(d.RebuildViewModels);
    }

    [Fact]
    public void LengthPercent_change_reconciles_appbar_and_rebuilds()
    {
        var d = DockSettingsDiff.Compare(
            new AppSettings { Dock = new DockSettings { LengthPercent = 100 } },
            new AppSettings { Dock = new DockSettings { LengthPercent = 80 } });

        Assert.True(d.ReconcileAppBar);
        Assert.True(d.RebuildViewModels);
    }

    [Fact]
    public void EdgeOffsetDip_change_reconciles_appbar_and_rebuilds()
    {
        var d = DockSettingsDiff.Compare(
            new AppSettings { Dock = new DockSettings { EdgeOffsetDip = 0.0 } },
            new AppSettings { Dock = new DockSettings { EdgeOffsetDip = 10.0 } });

        Assert.True(d.ReconcileAppBar);
        Assert.True(d.RebuildViewModels);
    }
}
