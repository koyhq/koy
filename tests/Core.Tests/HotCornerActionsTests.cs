using Koy.Core.Input;
using Koy.Core.Settings;
using Koy.Core.Shortcuts;

namespace Koy.Core.Tests;

public class HotCornerActionsTests
{
    [Fact]
    public void Defaults_bind_nothing_and_keep_tracking_off()
    {
        var settings = new AppSettings().HotCorners;

        Assert.False(CornerActions.IsActive(settings));
        Assert.Empty(CornerActions.ToBindings(settings));
        Assert.True(settings.DisableInFullscreen);
        Assert.Equal(CornerActions.DefaultDwellMs, settings.DwellMs);
    }

    [Fact]
    public void Tracking_turns_on_with_one_bound_corner_and_off_again()
    {
        var on = CornerActions.With(new HotCornersSettings(), ScreenCorner.BottomRight, CornerAction.OpenSearch);
        Assert.True(CornerActions.IsActive(on));

        var off = CornerActions.With(on, ScreenCorner.BottomRight, CornerAction.None);
        Assert.False(CornerActions.IsActive(off));
    }

    [Theory]
    [InlineData(CornerAction.OpenNotch, InputAction.OpenNotch)]
    [InlineData(CornerAction.OpenSearch, InputAction.OpenSearch)]
    [InlineData(CornerAction.OpenKoyWindow, InputAction.OpenKoyWindow)]
    [InlineData(CornerAction.ToggleDock, InputAction.ToggleDock)]
    public void Corner_actions_map_to_existing_input_actions(CornerAction action, InputAction expected)
        => Assert.Equal(expected, CornerActions.ToInputAction(action));

    [Fact]
    public void None_and_undefined_values_map_to_nothing()
    {
        Assert.Null(CornerActions.ToInputAction(CornerAction.None));
        Assert.Null(CornerActions.ToInputAction((CornerAction)999));
        Assert.False(CornerActions.IsActive(new HotCornersSettings { TopLeft = (CornerAction)999 }));
    }

    [Fact]
    public void ToBindings_builds_one_hot_corner_binding_per_bound_corner_with_clamped_dwell()
    {
        var settings = new HotCornersSettings { TopLeft = CornerAction.OpenNotch, BottomLeft = CornerAction.ToggleDock, DwellMs = 5 };

        var bindings = CornerActions.ToBindings(settings);

        Assert.Equal(2, bindings.Count);
        var first = Assert.IsType<HotCornerTrigger>(bindings[0].Trigger);
        Assert.Equal(ScreenCorner.TopLeft, first.Corner);
        Assert.Equal(CornerActions.MinDwellMs, first.DwellMs);
        Assert.Equal(InputAction.ToggleDock, bindings[1].Action);
    }

    [Fact]
    public void ToBindings_passes_the_input_binding_validator()
    {
        var settings = new HotCornersSettings
        {
            TopLeft = CornerAction.OpenNotch, TopRight = CornerAction.OpenSearch,
            BottomLeft = CornerAction.OpenKoyWindow, BottomRight = CornerAction.ToggleDock
        };

        var report = InputBindingValidator.Validate(CornerActions.ToBindings(settings), new HashSet<string>());

        Assert.Empty(report.Issues);
        Assert.Equal(4, report.Accepted.Count);
    }

    [Theory]
    [InlineData(-5, CornerActions.MinDwellMs)]
    [InlineData(400, 400)]
    [InlineData(99999, CornerActions.MaxDwellMs)]
    public void ClampDwell_stays_in_range(int input, int expected)
        => Assert.Equal(expected, CornerActions.ClampDwell(input));

    [Fact]
    public void Poll_interval_is_never_below_100_ms()
        => Assert.True(CornerActions.PollIntervalMs >= 100);

    [Fact]
    public void Settings_survive_a_save_and_load_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), "koy-corners-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            var settings = new AppSettings
            {
                HotCorners = new HotCornersSettings
                {
                    TopRight = CornerAction.OpenSearch, DwellMs = 700, DisableInFullscreen = false
                }
            };
            store.Save(settings);

            var loaded = store.Load();

            Assert.Equal(CornerAction.OpenSearch, loaded.HotCorners.TopRight);
            Assert.Equal(CornerAction.None, loaded.HotCorners.TopLeft);
            Assert.Equal(700, loaded.HotCorners.DwellMs);
            Assert.False(loaded.HotCorners.DisableInFullscreen);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OpenKoyWindow_shortcut_is_unassigned_by_default_and_counts_as_default()
    {
        var settings = new AppSettings();

        Assert.Equal("", ShortcutBindings.GetText(settings, ShortcutAction.OpenKoyWindow));
        Assert.True(ShortcutBindings.IsDefault(ShortcutAction.OpenKoyWindow, ""));
        Assert.False(ShortcutBindings.IsDefault(ShortcutAction.OpenKoyWindow, "Ctrl+Alt+W"));

        var changed = ShortcutBindings.WithText(settings, ShortcutAction.OpenKoyWindow, "Ctrl+Alt+W");
        Assert.Equal("Ctrl+Alt+W", changed.Shortcuts.OpenKoyWindow);
        Assert.Equal(settings.Shortcuts.ToggleNotch, changed.Shortcuts.ToggleNotch);
    }
}

public class HotCornerTrackerTests
{
    private const int Dwell = 400;

    [Fact]
    public void Fires_once_after_the_dwell_and_not_again_while_staying()
    {
        var tracker = new HotCornerTracker();

        Assert.Null(tracker.Update(ScreenCorner.TopLeft, false, 1000, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.TopLeft, false, 1300, Dwell));
        Assert.Equal(ScreenCorner.TopLeft, tracker.Update(ScreenCorner.TopLeft, false, 1400, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.TopLeft, false, 5000, Dwell));
    }

    [Fact]
    public void Leaving_and_reentering_rearms_and_restarts_the_dwell()
    {
        var tracker = new HotCornerTracker();
        tracker.Update(ScreenCorner.TopLeft, false, 0, Dwell);
        Assert.NotNull(tracker.Update(ScreenCorner.TopLeft, false, 500, Dwell));

        Assert.Null(tracker.Update(null, false, 600, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.TopLeft, false, 700, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.TopLeft, false, 1000, Dwell));
        Assert.NotNull(tracker.Update(ScreenCorner.TopLeft, false, 1100, Dwell));
    }

    [Fact]
    public void A_brief_pass_through_never_fires()
    {
        var tracker = new HotCornerTracker();

        Assert.Null(tracker.Update(ScreenCorner.BottomRight, false, 0, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.BottomRight, false, 120, Dwell));
        Assert.Null(tracker.Update(null, false, 240, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.BottomRight, false, 360, Dwell));
    }

    [Fact]
    public void Switching_to_another_corner_restarts_the_dwell()
    {
        var tracker = new HotCornerTracker();
        tracker.Update(ScreenCorner.TopLeft, false, 0, Dwell);

        Assert.Null(tracker.Update(ScreenCorner.TopRight, false, 450, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.TopRight, false, 800, Dwell));
        Assert.Equal(ScreenCorner.TopRight, tracker.Update(ScreenCorner.TopRight, false, 850, Dwell));
    }

    [Fact]
    public void Suppression_resets_the_dwell_so_it_never_fires_through_a_drag_or_fullscreen()
    {
        var tracker = new HotCornerTracker();
        tracker.Update(ScreenCorner.TopLeft, false, 0, Dwell);

        Assert.Null(tracker.Update(ScreenCorner.TopLeft, true, 450, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.TopLeft, false, 500, Dwell));
        Assert.Null(tracker.Update(ScreenCorner.TopLeft, false, 850, Dwell));
        Assert.NotNull(tracker.Update(ScreenCorner.TopLeft, false, 900, Dwell));
    }

    [Fact]
    public void Out_of_range_dwell_is_clamped()
    {
        var tracker = new HotCornerTracker();
        tracker.Update(ScreenCorner.TopLeft, false, 0, -1);

        Assert.Null(tracker.Update(ScreenCorner.TopLeft, false, CornerActions.MinDwellMs - 1, -1));
        Assert.NotNull(tracker.Update(ScreenCorner.TopLeft, false, CornerActions.MinDwellMs, -1));
    }
}
