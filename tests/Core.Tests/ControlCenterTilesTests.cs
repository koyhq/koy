using Koy.Core.Notch;
using Koy.Core.Settings;

namespace Koy.Core.Tests;

public class ControlCenterTilesTests
{
    [Fact]
    public void Default_settings_resolve_to_the_default_order()
    {
        Assert.Equal(ControlCenterTiles.DefaultOrder, ControlCenterTiles.Resolve(new AppSettings().Notch.ControlCenterLayout));
    }

    [Fact]
    public void Saved_order_is_respected()
    {
        Assert.Equal(["donotdisturb", "wifi", "bluetooth"], ControlCenterTiles.Resolve(["donotdisturb", "wifi", "bluetooth"]));
    }

    [Fact]
    public void Missing_tiles_are_appended_in_default_order()
    {
        Assert.Equal(["bluetooth", "wifi", "donotdisturb"], ControlCenterTiles.Resolve(["bluetooth"]));
    }

    [Fact]
    public void Unknown_duplicate_blank_and_null_entries_are_dropped_and_case_is_ignored()
    {
        var resolved = ControlCenterTiles.Resolve(["WiFi", "wifi", "ses", "", null!, " Bluetooth "]);

        Assert.Equal(["wifi", "bluetooth", "donotdisturb"], resolved);
    }

    [Fact]
    public void Null_or_empty_layout_gives_the_default_order()
    {
        Assert.Equal(ControlCenterTiles.DefaultOrder, ControlCenterTiles.Resolve(null));
        Assert.Equal(ControlCenterTiles.DefaultOrder, ControlCenterTiles.Resolve([]));
    }

    [Theory]
    [InlineData("wifi", 2, new[] { "bluetooth", "donotdisturb", "wifi" })]
    [InlineData("donotdisturb", 0, new[] { "donotdisturb", "wifi", "bluetooth" })]
    [InlineData("bluetooth", 1, new[] { "wifi", "bluetooth", "donotdisturb" })]
    [InlineData("wifi", 99, new[] { "bluetooth", "donotdisturb", "wifi" })]
    [InlineData("wifi", -4, new[] { "wifi", "bluetooth", "donotdisturb" })]
    public void Move_places_the_tile_at_the_target_slot(string id, int to, string[] expected)
    {
        Assert.Equal(expected, ControlCenterTiles.Move(ControlCenterTiles.DefaultOrder, id, to));
    }

    [Fact]
    public void Move_does_not_change_the_input_and_ignores_unknown_ids()
    {
        var original = new List<string>(ControlCenterTiles.DefaultOrder);

        var moved = ControlCenterTiles.Move(original, "wifi", 2);
        var same = ControlCenterTiles.Move(original, "yok", 1);

        Assert.Equal(ControlCenterTiles.DefaultOrder, original);
        Assert.NotEqual(original, moved);
        Assert.Equal(original, same);
    }

    [Theory]
    [InlineData(-30, 0)]
    [InlineData(10, 0)]
    [InlineData(116, 0)]
    [InlineData(130, 1)]
    [InlineData(260, 2)]
    [InlineData(9999, 2)]
    public void SlotAt_maps_the_drag_position_to_a_slot(double centerX, int expected)
    {
        // Üç yuva, her biri 116 geniş, 8 boşluk: sınırlar 124 ve 248.
        Assert.Equal(expected, ControlCenterTiles.SlotAt(centerX, 116, 8, 3));
    }

    [Fact]
    public void SlotAt_is_safe_for_degenerate_input()
    {
        Assert.Equal(0, ControlCenterTiles.SlotAt(50, 0, 8, 3));
        Assert.Equal(0, ControlCenterTiles.SlotAt(50, 100, 8, 1));
    }

    [Fact]
    public void SameOrder_compares_sequences()
    {
        Assert.True(ControlCenterTiles.SameOrder(["a", "b"], ["a", "b"]));
        Assert.False(ControlCenterTiles.SameOrder(["a", "b"], ["b", "a"]));
    }

    [Fact]
    public void Order_survives_a_settings_save_and_load()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-cc-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            store.Save(new AppSettings { Notch = new NotchSettings { ControlCenterLayout = new ValueList<string>(ControlCenterTiles.Move(ControlCenterTiles.DefaultOrder, "donotdisturb", 0)) } });

            Assert.Equal(["donotdisturb", "wifi", "bluetooth"], ControlCenterTiles.Resolve(store.Load().Notch.ControlCenterLayout));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
