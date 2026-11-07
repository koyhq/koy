using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class DockProximityMappingTests
{
    [Theory]
    [InlineData(DockProximityMode.ReserveSpace)]
    [InlineData(DockProximityMode.DockDown)]
    [InlineData(DockProximityMode.Dodge)]
    [InlineData(DockProximityMode.AlwaysHidden)]
    public void Capsule_keeps_every_mode(DockProximityMode mode)
    {
        Assert.True(DockProximityMapping.IsAvailable(DockStyleMode.FloatingCapsule, mode));
        Assert.Equal(mode, DockProximityMapping.Effective(DockStyleMode.FloatingCapsule, mode));
    }

    [Theory]
    [InlineData(DockProximityMode.ReserveSpace, true, DockProximityMode.ReserveSpace)]
    [InlineData(DockProximityMode.AlwaysHidden, true, DockProximityMode.AlwaysHidden)]
    [InlineData(DockProximityMode.DockDown, false, DockProximityMode.ReserveSpace)]
    [InlineData(DockProximityMode.Dodge, false, DockProximityMode.AlwaysHidden)]
    public void FullWidth_offers_only_always_show_and_auto_hide(
        DockProximityMode mode, bool available, DockProximityMode effective)
    {
        Assert.Equal(available, DockProximityMapping.IsAvailable(DockStyleMode.FullWidth, mode));
        Assert.Equal(effective, DockProximityMapping.Effective(DockStyleMode.FullWidth, mode));
    }

    [Fact]
    public void Effective_mode_is_always_available_in_its_style()
    {
        foreach (var style in Enum.GetValues<DockStyleMode>())
            foreach (var mode in Enum.GetValues<DockProximityMode>())
                Assert.True(DockProximityMapping.IsAvailable(style, DockProximityMapping.Effective(style, mode)));
    }
}
