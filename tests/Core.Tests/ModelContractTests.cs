using Koy.Core.Audio;
using Koy.Core.Display;
using Koy.Core.Notch;
using Koy.Core.Power;
using Koy.Core.Radio;
using Koy.Core.Weather;
using Xunit;

namespace Koy.Core.Tests;

public sealed class ModelContractTests
{
    [Fact]
    public void AudioStateChangedEventArgs_InitializesProperly()
    {
        var args = new AudioStateChangedEventArgs(0.75f, true);
        Assert.Equal(0.75f, args.Volume);
        Assert.True(args.IsMuted);
    }

    [Fact]
    public void RadioStateChangedEventArgs_InitializesProperly()
    {
        var args = new RadioStateChangedEventArgs(RadioKind.WiFi, RadioState.On);
        Assert.Equal(RadioKind.WiFi, args.Kind);
        Assert.Equal(RadioState.On, args.State);
    }

    [Fact]
    public void PowerChangedEventArgs_InitializesProperly()
    {
        var args = new PowerChangedEventArgs(true, 85, true, BatteryStatus.Charging);
        Assert.True(args.HasBattery);
        Assert.Equal(85, args.RemainingPercent);
        Assert.True(args.IsCharging);
        Assert.Equal(BatteryStatus.Charging, args.Status);
    }

    [Fact]
    public void WeatherInfo_InitializesProperly()
    {
        var now = DateTimeOffset.UtcNow;
        var info = new WeatherInfo(21.5, 2, "WeatherCondPartlyCloudy", "PartlyCloudy", now);
        Assert.Equal(21.5, info.Temperature);
        Assert.Equal(2, info.WeatherCode);
        Assert.Equal("WeatherCondPartlyCloudy", info.DescriptionKey);
        Assert.Equal("PartlyCloudy", info.IconKind);
        Assert.Equal(now, info.FetchedAt);
    }

    [Fact]
    public void NotchController_CyclesThroughThreePanels()
    {
        using var controller = new NotchController();
        controller.SetEnabledPanels([NotchPanel.Home, NotchPanel.ControlCenter, NotchPanel.Focus]);

        Assert.Equal(NotchPanel.Home, controller.CurrentPanel);

        var transitions = new List<int>();
        controller.PanelTransitionTriggered += dir => transitions.Add(dir);

        controller.NextPanel();
        Assert.Equal(NotchPanel.ControlCenter, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.Focus, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.Home, controller.CurrentPanel);

        controller.PreviousPanel();
        Assert.Equal(NotchPanel.Focus, controller.CurrentPanel);

        Assert.Equal([1, 1, 1, -1], transitions);
    }

    [Fact]
    public void NotchController_CyclesThroughAllPanels()
    {
        using var controller = new NotchController();
        controller.SetEnabledPanels(NotchPanels.Order);

        Assert.Equal(NotchPanel.Home, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.Focus, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.Calendar, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.Shelf, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.Clipboard, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.System, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.ControlCenter, controller.CurrentPanel);

        controller.NextPanel();
        Assert.Equal(NotchPanel.Home, controller.CurrentPanel);

        controller.PreviousPanel();
        Assert.Equal(NotchPanel.ControlCenter, controller.CurrentPanel);
    }

    [Fact]
    public void ShelfItem_InitializesProperly()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new Koy.Core.Shelf.ShelfItem("id1", "file.txt", "C:\\file.txt", Koy.Core.Shelf.ShelfItemKind.File, now);

        Assert.Equal("id1", item.Id);
        Assert.Equal("file.txt", item.DisplayName);
        Assert.Equal("C:\\file.txt", item.PathOrContent);
        Assert.Equal(Koy.Core.Shelf.ShelfItemKind.File, item.Kind);
        Assert.Equal(now, item.AddedAt);
    }
}
