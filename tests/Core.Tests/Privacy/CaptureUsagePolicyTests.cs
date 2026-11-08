using Koy.Core.Privacy;
using Xunit;

namespace Koy.Core.Tests.Privacy;

public sealed class CaptureUsagePolicyTests
{
    [Fact]
    public void IsActivelyUsing_ZeroStart_ReturnsFalse()
    {
        Assert.False(CaptureUsagePolicy.IsActivelyUsing(0, 0));
        Assert.False(CaptureUsagePolicy.IsActivelyUsing(0, 100));
    }

    [Fact]
    public void IsActivelyUsing_StartGreaterThanZero_StopZero_ReturnsTrue()
    {
        Assert.True(CaptureUsagePolicy.IsActivelyUsing(100, 0));
    }

    [Fact]
    public void IsActivelyUsing_StartGreaterThanStop_ReturnsTrue()
    {
        Assert.True(CaptureUsagePolicy.IsActivelyUsing(200, 150));
    }

    [Fact]
    public void IsActivelyUsing_StopGreaterThanOrEqualStart_ReturnsFalse()
    {
        Assert.False(CaptureUsagePolicy.IsActivelyUsing(100, 100));
        Assert.False(CaptureUsagePolicy.IsActivelyUsing(100, 200));
    }

    [Theory]
    [InlineData("C:#Program Files#obs-studio#bin#64bit#obs64.exe", "obs64")]
    [InlineData("C:#Program Files#Google#Chrome#Application#chrome.exe", "chrome")]
    [InlineData("C:\\path\\to\\app.exe", "app")]
    [InlineData("Microsoft.WindowsCamera_8wekyb3d8bbwe!App", "WindowsCamera")]
    [InlineData("5319275A.WhatsAppDesktop_cv1g1gvanyjgm", "WhatsAppDesktop")]
    [InlineData("", "Bilinmeyen uygulama")]
    [InlineData(null, "Bilinmeyen uygulama")]
    public void ExtractAppNameFromKey_ExtractsExpectedName(string? key, string expected)
    {
        var actual = CaptureUsagePolicy.ExtractAppNameFromKey(key);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatToolTip_BothInUse_FormatsBoth()
    {
        var state = new PrivacyState(true, true, "CameraApp", "MicApp");
        var text = CaptureUsagePolicy.FormatToolTip(state);
        Assert.Equal("Kamera (CameraApp) ve Mikrofon (MicApp) kullanımda", text);
    }

    [Fact]
    public void FormatToolTip_OnlyCamera_FormatsCamera()
    {
        var state = new PrivacyState(true, false, "CameraApp", null);
        var text = CaptureUsagePolicy.FormatToolTip(state);
        Assert.Equal("Kamera kullanımda (CameraApp)", text);
    }

    [Fact]
    public void FormatToolTip_OnlyMic_FormatsMic()
    {
        var state = new PrivacyState(false, true, null, "MicApp");
        var text = CaptureUsagePolicy.FormatToolTip(state);
        Assert.Equal("Mikrofon kullanımda (MicApp)", text);
    }

    [Fact]
    public void FormatToolTip_NoneInUse_ReturnsEmpty()
    {
        var text = CaptureUsagePolicy.FormatToolTip(PrivacyState.Empty);
        Assert.Empty(text);
    }
}
