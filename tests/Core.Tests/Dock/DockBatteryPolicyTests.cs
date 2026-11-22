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

    [Theory]
    [InlineData(82, "82%")]
    [InlineData(0, "0%")]
    [InlineData(100, "100%")]
    [InlineData(-5, "0%")]
    [InlineData(140, "100%")]
    public void FormatPercent_sinirlar_ve_bicim(int percent, string expected) =>
        Assert.Equal(expected, DockBatteryPolicy.FormatPercent(percent));

    [Theory]
    [InlineData(20, false, true)]
    [InlineData(21, false, false)]
    [InlineData(5, true, false)]
    [InlineData(15, false, true)]
    [InlineData(82, false, false)]
    public void IsLow_esik_yirmi_ve_sarjda_degil(int percent, bool charging, bool expected) =>
        Assert.Equal(expected, DockBatteryPolicy.IsLow(percent, charging));
}
