using Koy.Core.Dock.Items;

namespace Koy.Core.Tests;

public class DockBatteryPolicyTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void IsShown_yalniz_pil_varken_yer_kaplar(bool hasBattery, bool expected) =>
        Assert.Equal(expected, DockBatteryPolicy.IsShown(hasBattery));

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void CanAdd_pil_varsa_ve_ekli_degilse_acik(bool hasBattery, bool present, bool expected) =>
        Assert.Equal(expected, DockBatteryPolicy.CanAdd(hasBattery, present));
}
