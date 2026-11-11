using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class FirstRunPolicyTests
{
    [Fact]
    public void SihirbazTamamlanmadiysaDockKapaliKalir()
    {
        var settings = new AppSettings(); // FirstRunCompleted = false, DockEnabled varsayılan true

        var result = FirstRunPolicy.ApplySafeDefaultsIfIncomplete(settings);

        Assert.False(result.Dock.Enabled);
        Assert.False(result.Dock.HideWindowsTaskbar);
        Assert.True(result.Notch.Enabled);
    }

    [Fact]
    public void SihirbazTamamlandiysaAyarlarDegismez()
    {
        var settings = new AppSettings { General = new GeneralSettings { FirstRunCompleted = true }, Dock = new DockSettings { Enabled = true, HideWindowsTaskbar = true } };

        var result = FirstRunPolicy.ApplySafeDefaultsIfIncomplete(settings);

        Assert.Same(settings, result);
    }

    [Fact]
    public void OrijinalAyarNesnesiniDegistirmez()
    {
        var settings = new AppSettings();

        FirstRunPolicy.ApplySafeDefaultsIfIncomplete(settings);

        Assert.True(settings.Dock.Enabled);
    }

    [Fact]
    public void CreateReset_keeps_FirstRunCompleted_and_restores_defaults()
    {
        var current = new AppSettings { General = new GeneralSettings { FirstRunCompleted = true, Language = "en" }, Notch = new NotchSettings { Enabled = false }, Dock = new DockSettings { Proximity = Koy.Core.Dock.DockProximityMode.Dodge } };

        var reset = FirstRunPolicy.CreateReset(current);

        Assert.True(reset.General.FirstRunCompleted);
        Assert.Equal(new AppSettings { General = new GeneralSettings { FirstRunCompleted = true } }, reset);
    }

    [Fact]
    public void CreateReset_does_not_mark_unfinished_setup_as_completed()
    {
        var reset = FirstRunPolicy.CreateReset(new AppSettings { General = new GeneralSettings { FirstRunCompleted = false } });

        Assert.False(reset.General.FirstRunCompleted);
    }
}
