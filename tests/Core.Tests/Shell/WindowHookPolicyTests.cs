using Koy.Core.Shell;
using Koy.Core.Visibility;

namespace Koy.Core.Tests.Shell;

public class WindowHookPolicyTests
{
    [Fact]
    public void Notch_alone_never_needs_window_list_or_title_hooks()
    {
        foreach (var mode in Enum.GetValues<VisibilityMode>())
        foreach (var hideInFullscreen in new[] { true, false })
        {
            var hooks = WindowHookPolicy.HooksFor(WindowHookPolicy.ForNotch(mode, hideInFullscreen));

            Assert.True(hooks.HasFlag(WinEventHooks.Foreground));
            Assert.False(hooks.HasFlag(WinEventHooks.Lifecycle));
            Assert.False(hooks.HasFlag(WinEventHooks.Cloak));
            Assert.False(hooks.HasFlag(WinEventHooks.Name));
        }
    }

    [Fact]
    public void Notch_in_fixed_mode_without_fullscreen_hiding_needs_only_the_foreground_hook()
    {
        var needs = WindowHookPolicy.ForNotch(VisibilityMode.Fixed, hideInFullscreen: false);

        Assert.Equal(WindowHookNeeds.Foreground, needs);
        Assert.Equal(WinEventHooks.Foreground, WindowHookPolicy.HooksFor(needs));
    }

    [Theory]
    [InlineData(VisibilityMode.Smart, false)]
    [InlineData(VisibilityMode.Peek, false)]
    [InlineData(VisibilityMode.Fixed, true)]
    [InlineData(VisibilityMode.Smart, true)]
    public void Notch_follows_foreground_bounds_when_visibility_or_fullscreen_depend_on_them(VisibilityMode mode, bool hideInFullscreen)
    {
        var needs = WindowHookPolicy.ForNotch(mode, hideInFullscreen);

        Assert.True(needs.HasFlag(WindowHookNeeds.ForegroundMove));
        Assert.Equal(WinEventHooks.Foreground | WinEventHooks.Location, WindowHookPolicy.HooksFor(needs));
    }

    [Fact]
    public void Dock_needs_every_hook()
    {
        Assert.Equal(WinEventHooks.All, WindowHookPolicy.HooksFor(WindowHookPolicy.ForDock()));
    }

    [Theory]
    [InlineData(false, WinEventHooks.Foreground)]
    [InlineData(true, WinEventHooks.Foreground | WinEventHooks.Location)]
    public void Context_rules_need_bounds_only_for_the_fullscreen_condition(bool needsFullscreen, WinEventHooks expected)
    {
        Assert.Equal(expected, WindowHookPolicy.HooksFor(WindowHookPolicy.ForContextRules(needsFullscreen)));
    }

    [Fact]
    public void Nothing_requested_installs_nothing()
    {
        Assert.Equal(WinEventHooks.None, WindowHookPolicy.HooksFor(WindowHookNeeds.None));
        Assert.Equal(WindowHookNeeds.None, WindowHookPolicy.Combine([]));
    }

    [Fact]
    public void Move_alone_still_installs_the_foreground_hook_because_the_event_reports_the_handle()
    {
        Assert.Equal(
            WinEventHooks.Foreground | WinEventHooks.Location,
            WindowHookPolicy.HooksFor(WindowHookNeeds.ForegroundMove));
    }

    [Fact]
    public void Window_list_installs_lifecycle_and_cloak_but_not_name()
    {
        var hooks = WindowHookPolicy.HooksFor(WindowHookNeeds.WindowList);

        Assert.Equal(WinEventHooks.Lifecycle | WinEventHooks.Cloak, hooks);
    }

    [Fact]
    public void Title_changes_install_only_the_name_hook()
    {
        Assert.Equal(WinEventHooks.Name, WindowHookPolicy.HooksFor(WindowHookNeeds.TitleChanges));
    }

    [Fact]
    public void Combining_consumers_shares_hooks_and_drops_them_when_a_consumer_leaves()
    {
        var notch = WindowHookPolicy.ForNotch(VisibilityMode.Fixed, hideInFullscreen: false);
        var dock = WindowHookPolicy.ForDock();

        Assert.Equal(WinEventHooks.All, WindowHookPolicy.HooksFor(WindowHookPolicy.Combine([notch, dock])));
        // Dock kapanınca çentiğin isteği kalır.
        Assert.Equal(WinEventHooks.Foreground, WindowHookPolicy.HooksFor(WindowHookPolicy.Combine([notch])));
        // Çentik kapanınca dock'unki kalır.
        Assert.Equal(WinEventHooks.All, WindowHookPolicy.HooksFor(WindowHookPolicy.Combine([dock])));
    }

    [Theory]
    [InlineData(0x0003u, WinEventKind.Foreground)]
    [InlineData(0x800Bu, WinEventKind.Location)]
    [InlineData(0x8000u, WinEventKind.Lifecycle)]
    [InlineData(0x8001u, WinEventKind.Lifecycle)]
    [InlineData(0x8002u, WinEventKind.Lifecycle)]
    [InlineData(0x8003u, WinEventKind.Lifecycle)]
    [InlineData(0x8017u, WinEventKind.Cloak)]
    [InlineData(0x8018u, WinEventKind.Cloak)]
    [InlineData(0x800Cu, WinEventKind.Name)]
    [InlineData(0x8004u, WinEventKind.Other)]
    [InlineData(0x0000u, WinEventKind.Other)]
    public void Event_codes_map_to_their_kind(uint winEvent, WinEventKind expected)
    {
        Assert.Equal(expected, WindowHookPolicy.Classify(winEvent));
    }

    [Fact]
    public void Every_kind_maps_to_exactly_one_hook_and_other_maps_to_none()
    {
        Assert.Equal(WinEventHooks.Foreground, WindowHookPolicy.HookFor(WinEventKind.Foreground));
        Assert.Equal(WinEventHooks.Location, WindowHookPolicy.HookFor(WinEventKind.Location));
        Assert.Equal(WinEventHooks.Lifecycle, WindowHookPolicy.HookFor(WinEventKind.Lifecycle));
        Assert.Equal(WinEventHooks.Cloak, WindowHookPolicy.HookFor(WinEventKind.Cloak));
        Assert.Equal(WinEventHooks.Name, WindowHookPolicy.HookFor(WinEventKind.Name));
        Assert.Equal(WinEventHooks.None, WindowHookPolicy.HookFor(WinEventKind.Other));
    }

    [Theory]
    [InlineData(true, 0, 0, true)]
    [InlineData(false, 0, 0, false)] // imleç / düzenleme imleci: pencere tutamacı yok
    [InlineData(true, -2, 0, false)] // OBJID_CURSOR
    [InlineData(true, -8, 0, false)] // OBJID_CARET
    [InlineData(true, 0, 1, false)] // pencerenin alt öğesi
    [InlineData(true, 1, 0, false)]
    public void Window_level_prefilter_drops_cursor_caret_and_child_events(bool hasWindow, int idObject, int idChild, bool expected)
    {
        Assert.Equal(expected, WindowHookPolicy.IsWindowLevelEvent(hasWindow, idObject, idChild));
    }
}
