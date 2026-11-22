using Koy.Core.Dock;
using Koy.Core.Network;

namespace Koy.Core.Tests;

public class TrayGlyphsTests
{
    [Theory]
    [InlineData(0.5f, true, "\uE74F")]
    [InlineData(0f, false, "\uE74F")]
    [InlineData(0.2f, false, "\uE993")]
    [InlineData(0.5f, false, "\uE994")]
    [InlineData(0.9f, false, "\uE995")]
    public void Volume_MapsLevelAndMute(float volume, bool muted, string expected)
    {
        Assert.Equal(expected, TrayGlyphs.Volume(volume, muted));
    }

    [Fact]
    public void Network_ShowsOfflineWhenDisconnected()
    {
        Assert.Equal(TrayGlyphs.Offline, TrayGlyphs.Network(NetworkStatus.Disconnected));
        Assert.Equal(string.Empty, TrayGlyphs.WiFiLevel(NetworkStatus.Disconnected));
    }

    [Fact]
    public void Network_ShowsOfflineWhenConnectedWithoutInternet()
    {
        var status = new NetworkStatus(NetworkKind.WiFi, false, 4);
        Assert.Equal(TrayGlyphs.Offline, TrayGlyphs.Network(status));
        Assert.Equal(string.Empty, TrayGlyphs.WiFiLevel(status));
    }

    [Fact]
    public void Network_ShowsEthernetWithoutSignalOverlay()
    {
        var status = new NetworkStatus(NetworkKind.Ethernet, true, 0);
        Assert.Equal(TrayGlyphs.Ethernet, TrayGlyphs.Network(status));
        Assert.Equal(string.Empty, TrayGlyphs.WiFiLevel(status));
    }

    [Theory]
    [InlineData(1, "\uE872")]
    [InlineData(2, "\uE873")]
    [InlineData(4, "\uE874")]
    [InlineData(5, "")]
    public void WiFiLevel_MapsSignalBars(int bars, string expected)
    {
        var status = new NetworkStatus(NetworkKind.WiFi, true, bars);
        Assert.Equal(TrayGlyphs.WiFiFull, TrayGlyphs.Network(status));
        Assert.Equal(expected, TrayGlyphs.WiFiLevel(status));
    }
}
