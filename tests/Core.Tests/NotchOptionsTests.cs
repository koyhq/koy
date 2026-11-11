using Koy.Core.Notch;
using Koy.Core.Settings;

namespace Koy.Core.Tests;

public class NotchOptionsTests
{
    [Theory]
    [InlineData(NotchOpenBehavior.Hover, NotchPointerEvent.Enter, false, NotchOpenAction.Open, 120)]
    [InlineData(NotchOpenBehavior.Hover, NotchPointerEvent.Enter, true, NotchOpenAction.None, 0)]
    [InlineData(NotchOpenBehavior.Hover, NotchPointerEvent.Leave, true, NotchOpenAction.Close, 300)]
    [InlineData(NotchOpenBehavior.Hover, NotchPointerEvent.Leave, false, NotchOpenAction.None, 0)]
    [InlineData(NotchOpenBehavior.Hover, NotchPointerEvent.Click, false, NotchOpenAction.None, 0)]
    [InlineData(NotchOpenBehavior.Click, NotchPointerEvent.Click, false, NotchOpenAction.Open, 0)]
    [InlineData(NotchOpenBehavior.Click, NotchPointerEvent.Click, true, NotchOpenAction.Close, 0)]
    [InlineData(NotchOpenBehavior.Click, NotchPointerEvent.Enter, false, NotchOpenAction.None, 0)]
    // Bugünkü davranış: açık çentik her kipte imleç çıkınca kapanır.
    [InlineData(NotchOpenBehavior.Click, NotchPointerEvent.Leave, true, NotchOpenAction.Close, 300)]
    [InlineData(NotchOpenBehavior.DragOver, NotchPointerEvent.Leave, true, NotchOpenAction.Close, 300)]
    [InlineData(NotchOpenBehavior.DragOver, NotchPointerEvent.DragEnter, false, NotchOpenAction.Open, 120)]
    [InlineData(NotchOpenBehavior.DragOver, NotchPointerEvent.DragEnter, true, NotchOpenAction.None, 0)]
    [InlineData(NotchOpenBehavior.DragOver, NotchPointerEvent.DragLeave, true, NotchOpenAction.Close, 300)]
    [InlineData(NotchOpenBehavior.DragOver, NotchPointerEvent.Enter, false, NotchOpenAction.None, 0)]
    [InlineData(NotchOpenBehavior.DragOver, NotchPointerEvent.Click, false, NotchOpenAction.None, 0)]
    // Bugünkü davranış: dosya sürüklenince çentik her kipte Raf için hemen açılır; gecikme yalnız DragOver kipinin.
    [InlineData(NotchOpenBehavior.Hover, NotchPointerEvent.DragEnter, false, NotchOpenAction.Open, 0)]
    [InlineData(NotchOpenBehavior.Click, NotchPointerEvent.DragEnter, false, NotchOpenAction.Open, 0)]
    [InlineData(NotchOpenBehavior.Hover, NotchPointerEvent.DragLeave, true, NotchOpenAction.Close, 300)]
    public void Open_policy_maps_event_to_action(NotchOpenBehavior behavior, NotchPointerEvent ev, bool isOpen, NotchOpenAction action, int expectedDelay)
    {
        var decision = NotchOpenPolicy.Decide(behavior, ev, isOpen, openDelayMs: 120, closeDelayMs: 300);

        Assert.Equal(action, decision.Action);
        Assert.Equal(action == NotchOpenAction.None ? 0 : expectedDelay, decision.DelayMs);
    }

    [Fact]
    public void Click_ignores_open_delay()
    {
        var decision = NotchOpenPolicy.Decide(NotchOpenBehavior.Click, NotchPointerEvent.Click, false, openDelayMs: 900, closeDelayMs: 0);

        Assert.Equal(new NotchOpenDecision(NotchOpenAction.Open, 0), decision);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(450, 450)]
    [InlineData(2000, 2000)]
    [InlineData(99999, 2000)]
    public void Delays_are_clamped_to_zero_through_max(int requested, int expected)
    {
        var decision = NotchOpenPolicy.Decide(NotchOpenBehavior.Hover, NotchPointerEvent.Enter, false, openDelayMs: requested, closeDelayMs: 0);

        Assert.Equal(expected, decision.DelayMs);
    }

    [Fact]
    public void Null_lists_in_the_file_are_restored()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "Notch": { "PanelOrder": null, "HomeCards": null } }""");

        Assert.Empty(loaded.Notch.PanelOrder);
        Assert.Equal(HomeCardLayout.Default, loaded.Notch.HomeCards);
    }

    [Fact]
    public void Default_home_cards_are_a_copy_not_the_shared_default()
    {
        new NotchSettings().HomeCards.Clear();

        Assert.Equal(HomeCardLayout.Default, new NotchSettings().HomeCards);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(5, 5)]
    [InlineData(16, 16)]
    [InlineData(40, 16)]
    public void Visualizer_bar_count_is_clamped(int requested, int expected)
    {
        Assert.Equal(expected, NotchVisualizer.ClampBarCount(requested));
    }

    [Fact]
    public void Panel_order_puts_valid_keys_first_and_appends_missing_panels()
    {
        var order = NotchPanels.NormalizeOrder(["system", "bogus", "home", "system", "shelf"]);

        Assert.Equal(
            [NotchPanel.System, NotchPanel.Home, NotchPanel.Shelf, NotchPanel.Focus, NotchPanel.Calendar, NotchPanel.Clipboard, NotchPanel.ControlCenter],
            order);
    }

    [Fact]
    public void Panel_order_without_keys_is_default_order()
    {
        Assert.Equal(NotchPanels.Order, NotchPanels.NormalizeOrder(null));
        Assert.Equal(NotchPanels.Order, NotchPanels.NormalizeOrder([]));
    }

    [Fact]
    public void Home_cards_keep_order_and_drop_duplicates()
    {
        var cards = HomeCardLayout.Normalize([HomeCard.Weather, HomeCard.Music, HomeCard.Weather, (HomeCard)99]);

        Assert.Equal([HomeCard.Weather, HomeCard.Music], cards);
    }

    [Fact]
    public void Home_cards_null_gives_default_and_empty_stays_empty()
    {
        Assert.Equal(HomeCardLayout.Default, HomeCardLayout.Normalize(null));
        Assert.Empty(HomeCardLayout.Normalize([]));
    }

    [Fact]
    public void New_notch_options_have_safe_defaults()
    {
        var notch = new AppSettings().Notch;
        var music = new AppSettings().Music;

        Assert.Equal(NotchTabBarStyle.IconOnly, notch.TabBarStyle);
        Assert.Equal(0, notch.OpenDelayMs);
        Assert.Equal(NotchOpenPolicy.DefaultCloseDelayMs, notch.CloseDelayMs);
        Assert.Empty(notch.PanelOrder);
        Assert.Equal(HomeCardLayout.Default, notch.HomeCards);
        Assert.Equal(NotchVisualizerStyle.Bars, music.VisualizerStyle);
        Assert.Equal(NotchVisualizerColorSource.Accent, music.VisualizerColorSource);
        Assert.Equal(5, music.VisualizerBarCount);
    }

    [Fact]
    public void New_notch_options_survive_save_and_load()
    {
        var path = Path.Combine(Path.GetTempPath(), "koy-notch-options-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var original = new AppSettings
            {
                Notch = new NotchSettings
                {
                    OpenBehavior = NotchOpenBehavior.DragOver,
                    OpenDelayMs = 250,
                    CloseDelayMs = 600,
                    TabBarStyle = NotchTabBarStyle.Hidden,
                    PanelOrder = ["system", "home"],
                    HomeCards = [HomeCard.Weather, HomeCard.Timers],
                },
                Music = new MusicSettings
                {
                    VisualizerStyle = NotchVisualizerStyle.Dots,
                    VisualizerColorSource = NotchVisualizerColorSource.Artwork,
                    VisualizerBarCount = 9,
                },
            };

            var store = new SettingsStore(path);
            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(original.Notch, loaded.Notch);
            Assert.Equal(original.Music, loaded.Music);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
