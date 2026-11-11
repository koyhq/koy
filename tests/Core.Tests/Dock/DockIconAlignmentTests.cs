using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

public sealed class DockIconAlignmentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-tests-" + Guid.NewGuid());

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    [Fact]
    public void Default_is_center()
        => Assert.Equal(DockIconAlignment.Center, new AppSettings().Dock.IconAlignment);

    [Fact]
    public void Old_settings_file_without_the_field_stays_centered()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{ "DockStyle": "FullWidth" }""");

        Assert.Equal(DockIconAlignment.Center, new SettingsStore(SettingsPath).Load().Dock.IconAlignment);
    }

    [Theory]
    [InlineData(DockIconAlignment.Left)]
    [InlineData(DockIconAlignment.Right)]
    public void Round_trips_by_name(DockIconAlignment alignment)
    {
        var store = new SettingsStore(SettingsPath);

        store.Save(new AppSettings { Dock = new DockSettings { IconAlignment = alignment } });

        Assert.Contains($"\"IconAlignment\": \"{alignment}\"", File.ReadAllText(SettingsPath));
        Assert.Equal(alignment, store.Load().Dock.IconAlignment);
    }

    [Fact]
    public void Alignment_takes_part_in_equality()
        => Assert.NotEqual(new AppSettings(), new AppSettings { Dock = new DockSettings { IconAlignment = DockIconAlignment.Left } });

    [Fact]
    public void Changing_alignment_only_rebuilds_view_models()
    {
        var d = DockSettingsDiff.Compare(new AppSettings(), new AppSettings { Dock = new DockSettings { IconAlignment = DockIconAlignment.Right } });

        Assert.True(d.RebuildViewModels);
        Assert.False(d.ReconcileAppBar);
        Assert.False(d.ReconcileTaskbar);
        Assert.False(d.ReconcileMonitors);
        Assert.False(d.ReconcileTray);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
