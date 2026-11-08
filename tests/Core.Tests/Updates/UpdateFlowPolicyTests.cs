using Koy.Core.Updates;

namespace Koy.Core.Tests.Updates;

public class UpdateFlowPolicyTests
{
    [Theory]
    [InlineData("0.2.0", "0.1.1", 1)]
    [InlineData("0.1.1", "0.2.0", -1)]
    [InlineData("0.2.0", "0.2.0", 0)]
    [InlineData("v0.2.0", "0.2.0", 0)]
    [InlineData("0.2.0+abc", "0.2.0+def", 0)]
    [InlineData("0.2.1", "0.2.0", 1)]
    [InlineData("1.0.0", "0.9.9", 1)]
    [InlineData("0.3.0", "0.3.0-rc.2", 1)]
    [InlineData("0.3.0-rc.2", "0.3.0", -1)]
    [InlineData("0.3.0-rc.10", "0.3.0-rc.2", 1)]
    [InlineData("0.3.0-rc.1", "0.3.0-rc.1", 0)]
    [InlineData("0.3.0-beta", "0.3.0-alpha", 1)]
    public void CompareVersions_DogruKarsilastirir(string vA, string vB, int expectedSign)
    {
        var result = UpdateFlowPolicy.CompareVersions(vA, vB);
        Assert.Equal(expectedSign, Math.Sign(result));
    }

    [Theory]
    [InlineData("0.1.1", "0.2.0", true)]
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("0.3.0", "0.2.0", false)]
    [InlineData("v0.1.1", "0.2.0", true)]
    public void IsNewerVersion_YalnizDahaYuksekSurumlerdeTrue(string current, string available, bool expected)
    {
        Assert.Equal(expected, UpdateFlowPolicy.IsNewerVersion(current, available));
    }

    [Fact]
    public void ShouldShowWhatsNew_IlkKurulumda_False()
    {
        // İlk açılış sihirbazı çalışır, Neler Geldi gösterilmez.
        Assert.False(UpdateFlowPolicy.ShouldShowWhatsNew("0.2.0", "", firstRunCompleted: false));
        Assert.False(UpdateFlowPolicy.ShouldShowWhatsNew("0.2.0", null, firstRunCompleted: false));
    }

    [Fact]
    public void ShouldShowWhatsNew_EskiSurumdenGeciste_True()
    {
        // Daha önce sürüm kaydedilmemiş (v0.1.1/0.2.0'da bu ayar yoktu) ama firstRunCompleted=true
        Assert.True(UpdateFlowPolicy.ShouldShowWhatsNew("0.2.0", "", firstRunCompleted: true));
        Assert.True(UpdateFlowPolicy.ShouldShowWhatsNew("0.2.0", null, firstRunCompleted: true));
    }

    [Fact]
    public void ShouldShowWhatsNew_YeniSurumde_True()
    {
        Assert.True(UpdateFlowPolicy.ShouldShowWhatsNew("0.2.1", "0.2.0", firstRunCompleted: true));
        Assert.True(UpdateFlowPolicy.ShouldShowWhatsNew("0.3.0", "0.2.0", firstRunCompleted: true));
    }

    [Fact]
    public void ShouldShowWhatsNew_AyniVeyaEskiSurumde_False()
    {
        Assert.False(UpdateFlowPolicy.ShouldShowWhatsNew("0.2.0", "0.2.0", firstRunCompleted: true));
        Assert.False(UpdateFlowPolicy.ShouldShowWhatsNew("0.1.9", "0.2.0", firstRunCompleted: true));
    }

    [Theory]
    [InlineData("0.2.0", false, "0.2.0")]
    [InlineData("v0.2.0", false, "0.2.0")]
    [InlineData("0.2.0", true, "v0.2.0")]
    [InlineData("v0.2.0", true, "v0.2.0")]
    public void FormatDisplayVersion_Formatlar(string input, bool prefixWithV, string expected)
    {
        Assert.Equal(expected, UpdateFlowPolicy.FormatDisplayVersion(input, prefixWithV));
    }

    [Fact]
    public void ShouldShowWhatsNew_OnSurumdenSonSurumeGecinceGosterir()
    {
        Assert.True(UpdateFlowPolicy.ShouldShowWhatsNew("0.3.0", "0.3.0-rc.2"));
        Assert.False(UpdateFlowPolicy.ShouldShowWhatsNew("0.3.0-rc.2", "0.3.0"));
    }
}
