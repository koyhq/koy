using System.Globalization;
using Koy.Core.Dock.Items;
using Koy.Core.Dock.SystemArea;

namespace Koy.Core.Tests.Dock;

public class DockSystemAreaTests
{
    [Fact]
    public void Full_width_horizontal_shows_every_part_in_windows_order()
    {
        var a = DockSystemArea.Resolve(isFullWidth: true, isVertical: false, hasTray: true, hasBattery: true);

        Assert.True(a.IsActive);
        Assert.True(a.ShowOverflowArrow);
        Assert.True(a.ShowVisibleTray);
        Assert.True(a.ShowStatusGroup);
        Assert.True(a.ShowBattery);
        Assert.True(a.ShowClock);
        Assert.True(a.ShowBell);
        Assert.True(a.ShowDesktopStrip);
    }

    [Fact]
    public void Machine_without_battery_hides_only_the_battery()
    {
        var a = DockSystemArea.Resolve(true, false, hasTray: true, hasBattery: false);

        Assert.False(a.ShowBattery);
        Assert.True(a.ShowStatusGroup);
        Assert.True(a.ShowClock);
    }

    [Fact]
    public void Without_the_tray_window_arrow_and_visible_icons_are_hidden_but_the_rest_stays()
    {
        // Windows görev çubuğu gizlenmediyse Koy'un tepsisi yoktur; sistem alanının geri kalanı yine çizilir.
        var a = DockSystemArea.Resolve(true, false, hasTray: false, hasBattery: true);

        Assert.False(a.ShowOverflowArrow);
        Assert.False(a.ShowVisibleTray);
        Assert.True(a.ShowStatusGroup);
        Assert.True(a.ShowClock);
        Assert.True(a.ShowBell);
    }

    [Theory]
    [InlineData(false, false)]   // kapsül: Koy'un kendi düzeni
    [InlineData(true, true)]     // dikey tam genişlik: Windows 11 yatay
    [InlineData(false, true)]
    public void Capsule_and_vertical_docks_have_no_system_area(bool fullWidth, bool vertical)
    {
        var a = DockSystemArea.Resolve(fullWidth, vertical, hasTray: true, hasBattery: true);

        Assert.False(a.IsActive);
        Assert.Equal(default, a);
    }

    [Theory]
    [InlineData(DockItemKind.Battery, true)]
    [InlineData(DockItemKind.Clock, true)]
    [InlineData(DockItemKind.Trash, false)]
    [InlineData(DockItemKind.Custom, false)]
    [InlineData(DockItemKind.Separator, false)]
    [InlineData(DockItemKind.Spacer, false)]
    public void Extra_items_drawn_by_the_system_area_are_not_drawn_twice(DockItemKind kind, bool hidden)
    {
        var area = DockSystemArea.Resolve(true, false, hasTray: true, hasBattery: true);

        Assert.Equal(hidden, DockSystemArea.HidesExtra(kind, area));
    }

    [Fact]
    public void Extra_items_are_kept_when_there_is_no_system_area()
    {
        var capsule = DockSystemArea.Resolve(false, false, true, true);

        Assert.False(DockSystemArea.HidesExtra(DockItemKind.Battery, capsule));
        Assert.False(DockSystemArea.HidesExtra(DockItemKind.Clock, capsule));
    }

    [Fact]
    public void Clock_uses_culture_short_time_and_short_date_and_long_date_tooltip()
    {
        var now = new DateTime(2026, 11, 22, 18, 34, 5);
        var tr = new CultureInfo("tr-TR");
        var en = new CultureInfo("en-US");

        Assert.Equal("18:34", SystemClockText.Time(now, tr));
        Assert.Equal("22.11.2026", SystemClockText.Date(now, tr));
        Assert.Equal("22 Kasım 2026 Pazar", SystemClockText.Tooltip(now, tr));

        Assert.Equal("6:34 PM", SystemClockText.Time(now, en));
        Assert.Equal("11/22/2026", SystemClockText.Date(now, en));
        Assert.Equal("Sunday, November 22, 2026", SystemClockText.Tooltip(now, en));
    }

    [Fact]
    public void Desktop_strip_is_thin()
    {
        Assert.InRange(DockSystemArea.DesktopStripWidthDip, 3, 10);
    }
}
