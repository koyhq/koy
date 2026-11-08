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
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractAppNameFromKey_ExtractsExpectedName(string? key, string? expected)
    {
        var actual = CaptureUsagePolicy.ExtractAppNameFromKey(key);
        Assert.Equal(expected, actual);
    }

    private const string Both = "Cam: {0}, Mic: {1}";
    private const string CamOnly = "Cam: {0}";
    private const string MicOnly = "Mic: {0}";

    [Theory]
    [InlineData(true, true, "CameraApp", "MicApp", "Cam: CameraApp, Mic: MicApp")]
    [InlineData(true, false, "CameraApp", null, "Cam: CameraApp")]
    [InlineData(false, true, null, "MicApp", "Mic: MicApp")]
    [InlineData(true, true, null, null, "Cam: Unknown, Mic: Unknown")]
    [InlineData(true, false, null, null, "Cam: Unknown")]
    [InlineData(false, false, null, null, "")]
    public void FormatToolTip_UsesTemplatesAndUnknownName(bool cam, bool mic, string? camApp, string? micApp, string expected)
    {
        var state = new PrivacyState(cam, mic, camApp, micApp);
        Assert.Equal(expected, CaptureUsagePolicy.FormatToolTip(state, Both, CamOnly, MicOnly, "Unknown"));
    }
}
