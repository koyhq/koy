using Koy.Core.Settings;
using Koy.Core.Shortcuts;

namespace Koy.Core.Tests;

public class KeyboardHookEngineTests
{
    private const int LWin = 0x5B;
    private const int LShift = 0xA0;
    private const int LCtrl = 0xA2;
    private const int LAlt = 0xA4;
    private const int S = 0x53;
    private const int X = 0x58;
    private const int Esc = 0x1B;
    private const int Id = 6;

    private static Hotkey Parse(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey), text);
        return hotkey;
    }

    private static KeyboardHookEngine Engine(params string[] bindings)
    {
        var engine = new KeyboardHookEngine();
        var map = new Dictionary<int, Hotkey>();
        for (var i = 0; i < bindings.Length; i++)
            map[Id + i] = Parse(bindings[i]);
        engine.SetBindings(map);
        return engine;
    }

    private static HookOutcome Down(KeyboardHookEngine e, int vk, long now = 0, Func<int, bool>? down = null) => e.Process(vk, true, now, down);

    private static HookOutcome Up(KeyboardHookEngine e, int vk, long now = 0) => e.Process(vk, false, now);

    [Fact]
    public void Win_alone_fires_on_release_and_masks_the_start_menu()
    {
        var e = Engine("Win");

        Assert.Equal(HookOutcome.Pass, Down(e, LWin));

        var up = Up(e, LWin);
        Assert.Equal(Id, up.FiredId);
        Assert.True(up.MaskModifier);
        Assert.False(up.Swallow);
    }

    [Fact]
    public void Win_with_another_key_does_not_fire_win_alone()
    {
        var e = Engine("Win");

        Down(e, LWin);
        Assert.Equal(HookOutcome.Pass, Down(e, X));
        Assert.Equal(HookOutcome.Pass, Up(e, X));

        Assert.Equal(HookOutcome.Pass, Up(e, LWin));
    }

    [Fact]
    public void Win_alone_is_left_to_windows_when_not_bound()
    {
        var e = Engine("Win+Shift");

        Down(e, LWin);

        Assert.Equal(HookOutcome.Pass, Up(e, LWin));
    }

    [Fact]
    public void Two_modifiers_fire_when_both_are_released_in_either_order()
    {
        var e = Engine("Win+Shift");

        Down(e, LWin);
        Down(e, LShift);

        var winUp = Up(e, LWin);
        Assert.Null(winUp.FiredId);
        Assert.True(winUp.MaskModifier);

        Assert.Equal(Id, Up(e, LShift).FiredId);
    }

    [Fact]
    public void Shift_alone_does_not_need_a_mask()
    {
        var e = Engine("Shift");

        Down(e, LShift);
        var up = Up(e, LShift);

        Assert.Equal(Id, up.FiredId);
        Assert.False(up.MaskModifier);
    }

    [Fact]
    public void Windows_shortcut_is_overridden_and_start_stays_closed()
    {
        var e = Engine("Win+S");

        Down(e, LWin);
        var key = Down(e, S);
        Assert.True(key.Swallow);
        Assert.Equal(Id, key.FiredId);

        Assert.True(Up(e, S).Swallow);

        var winUp = Up(e, LWin);
        Assert.True(winUp.MaskModifier);
        Assert.Null(winUp.FiredId);
    }

    [Fact]
    public void Holding_the_key_fires_once_and_keeps_it_from_windows()
    {
        var e = Engine("Win+S");
        Down(e, LWin);
        Down(e, S);

        var repeat = Down(e, S);

        Assert.True(repeat.Swallow);
        Assert.Null(repeat.FiredId);
    }

    [Fact]
    public void Other_shortcuts_pass_through()
    {
        var e = Engine("Win+S");
        Down(e, LCtrl);

        Assert.Equal(HookOutcome.Pass, Down(e, S));
        Assert.Equal(HookOutcome.Pass, Up(e, S));
        Assert.Equal(HookOutcome.Pass, Up(e, LCtrl));
    }

    [Fact]
    public void Alt_is_masked_after_an_override_so_the_menu_bar_stays_closed()
    {
        var e = Engine("Alt+Tab");
        Down(e, LAlt);
        Down(e, 0x09);
        Up(e, 0x09);

        Assert.True(Up(e, LAlt).MaskModifier);
    }

    [Fact]
    public void Missed_release_does_not_leave_a_modifier_stuck()
    {
        var e = Engine("Win+S", "S");
        Down(e, LWin); // Win+L ile kilitlenince bırakılma olayı gelmez

        var key = Down(e, S, down: _ => false);

        Assert.Equal(Id + 1, key.FiredId);
    }

    [Fact]
    public void Capture_swallows_keys_and_reports_the_combination()
    {
        var e = Engine();
        e.BeginCapture(0);

        Assert.True(Down(e, LCtrl).Swallow);
        var key = Down(e, X);

        Assert.True(key.Swallow);
        Assert.Equal(Parse("Ctrl+X"), key.Captured);
    }

    [Fact]
    public void Capture_reports_win_alone_when_it_is_released()
    {
        var e = Engine();
        e.BeginCapture(0);

        Assert.Null(Down(e, LWin).Captured);
        var up = Up(e, LWin);

        Assert.True(up.Swallow);
        Assert.Equal(Parse("Win"), up.Captured);
        Assert.False(up.MaskModifier);
    }

    [Fact]
    public void Capture_reports_a_bare_key_such_as_escape()
    {
        var e = Engine();
        e.BeginCapture(0);

        Assert.Equal(new Hotkey(HotkeyModifiers.None, Esc), Down(e, Esc).Captured);
    }

    [Fact]
    public void Capture_does_not_fire_existing_shortcuts()
    {
        var e = Engine("Win");
        e.BeginCapture(0);

        Down(e, LWin);

        Assert.Null(Up(e, LWin).FiredId);
    }

    [Fact]
    public void Keys_of_a_captured_chord_stay_swallowed_after_capture_ends()
    {
        var e = Engine();
        e.BeginCapture(0);
        Down(e, LCtrl);
        Down(e, X);

        e.EndCapture();

        Assert.True(Up(e, X).Swallow);
        Assert.True(Up(e, LCtrl).Swallow);
        Assert.Equal(HookOutcome.Pass, Down(e, X));
    }

    [Fact]
    public void Abandoned_capture_stops_swallowing_the_keyboard()
    {
        var e = Engine();
        e.BeginCapture(0);

        Assert.Equal(HookOutcome.Pass, Down(e, X, now: 15_001));
    }

    [Fact]
    public void Right_side_modifiers_count_too()
    {
        var e = Engine("Win");

        Down(e, 0x5C);

        Assert.Equal(Id, Up(e, 0x5C).FiredId);
    }
}

public class ShortcutManagerHookTests
{
    private sealed class FakeHotkeys : IHotkeyService
    {
        public HashSet<Hotkey> Taken { get; } = [];
        public List<Hotkey> Registered { get; } = [];
#pragma warning disable CS0067
        public event EventHandler<int>? HotkeyPressed;
#pragma warning restore CS0067

        public HotkeyRegistrationResult Register(int id, Hotkey hotkey)
        {
            if (Taken.Contains(hotkey))
                return HotkeyRegistrationResult.AlreadyInUse;
            Registered.Add(hotkey);
            return HotkeyRegistrationResult.Success;
        }

        public void Unregister(int id)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeHook : IKeyboardHookService
    {
        public IReadOnlyDictionary<int, Hotkey> Bindings { get; private set; } = new Dictionary<int, Hotkey>();
        public bool Installs { get; init; } = true;
        public event EventHandler<int>? HotkeyPressed;

        public bool SetBindings(IReadOnlyDictionary<int, Hotkey> bindings)
        {
            Bindings = bindings;
            return Installs || bindings.Count == 0;
        }

        public bool BeginCapture(Action<Hotkey> onCaptured) => true;

        public void EndCapture()
        {
        }

        public void Press(int id) => HotkeyPressed?.Invoke(this, id);

        public void Dispose()
        {
        }
    }

    private static Hotkey Parse(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey), text);
        return hotkey;
    }

    private static Dictionary<ShortcutAction, string> Search(string text) => new() { [ShortcutAction.OpenSearch] = text };

    [Theory]
    [InlineData("Win")]
    [InlineData("Win+Shift")]
    [InlineData("Win+S")]
    [InlineData("Alt+Tab")]
    public void Windows_owned_and_modifier_only_use_the_hook(string text)
    {
        var hotkeys = new FakeHotkeys();
        var hook = new FakeHook();
        using var manager = new ShortcutManager(hotkeys, hook);

        var results = manager.Apply(Search(text));

        Assert.Equal(ShortcutStatus.Registered, results[ShortcutAction.OpenSearch].Status);
        Assert.Contains(Parse(text), hook.Bindings.Values);
        Assert.Empty(hotkeys.Registered);
    }

    [Fact]
    public void Ordinary_shortcut_does_not_install_the_hook()
    {
        var hotkeys = new FakeHotkeys();
        var hook = new FakeHook();
        using var manager = new ShortcutManager(hotkeys, hook);

        manager.Apply(Search("Win+Shift+Q"));

        Assert.Empty(hook.Bindings);
        Assert.Single(hotkeys.Registered);
    }

    [Fact]
    public void Win_combination_held_by_windows_falls_back_to_the_hook()
    {
        var hotkeys = new FakeHotkeys();
        hotkeys.Taken.Add(Parse("Win+Shift+Space"));
        var hook = new FakeHook();
        using var manager = new ShortcutManager(hotkeys, hook);

        var results = manager.Apply(Search("Win+Shift+Space"));

        Assert.Equal(ShortcutStatus.Registered, results[ShortcutAction.OpenSearch].Status);
        Assert.Single(hook.Bindings);
    }

    [Fact]
    public void Combination_held_by_another_app_is_still_an_error()
    {
        var hotkeys = new FakeHotkeys();
        hotkeys.Taken.Add(Parse("Ctrl+Alt+F9"));
        var hook = new FakeHook();
        using var manager = new ShortcutManager(hotkeys, hook);

        var results = manager.Apply(Search("Ctrl+Alt+F9"));

        Assert.Equal(ShortcutStatus.InUseByOtherApp, results[ShortcutAction.OpenSearch].Status);
        Assert.Empty(hook.Bindings);
    }

    [Theory]
    [InlineData("Win+L")]
    [InlineData("Ctrl+Alt+Delete")]
    public void Combinations_windows_never_gives_up_are_rejected(string text)
    {
        var hook = new FakeHook();
        using var manager = new ShortcutManager(new FakeHotkeys(), hook);

        var results = manager.Apply(Search(text));

        Assert.Equal(ShortcutStatus.ReservedBySystem, results[ShortcutAction.OpenSearch].Status);
    }

    [Fact]
    public void Hook_press_triggers_the_action()
    {
        var hook = new FakeHook();
        using var manager = new ShortcutManager(new FakeHotkeys(), hook);
        manager.Apply(Search("Win"));
        ShortcutAction? fired = null;
        manager.Triggered += (_, action) => fired = action;

        hook.Press(hook.Bindings.Keys.Single());

        Assert.Equal(ShortcutAction.OpenSearch, fired);
    }

    [Fact]
    public void Hook_that_cannot_install_is_reported_as_failed()
    {
        var hook = new FakeHook { Installs = false };
        using var manager = new ShortcutManager(new FakeHotkeys(), hook);

        var results = manager.Apply(Search("Win"));

        Assert.Equal(ShortcutStatus.Failed, results[ShortcutAction.OpenSearch].Status);
    }

    [Fact]
    public void Modifier_only_without_a_hook_fails_instead_of_reaching_register_hotkey()
    {
        var hotkeys = new FakeHotkeys();
        using var manager = new ShortcutManager(hotkeys);

        var results = manager.Apply(Search("Win"));

        Assert.Equal(ShortcutStatus.Failed, results[ShortcutAction.OpenSearch].Status);
        Assert.Empty(hotkeys.Registered);
    }

    [Theory]
    [InlineData("N", true)]
    [InlineData("Shift+N", true)]
    [InlineData("Space", true)]
    [InlineData("F9", false)]
    [InlineData("Shift+F9", false)]
    [InlineData("Ctrl+N", false)]
    [InlineData("Win", false)]
    public void Typing_keys_are_flagged(string text, bool expected)
    {
        Assert.Equal(expected, Parse(text).CapturesTyping);
    }

    [Fact]
    public void Default_settings_do_not_need_the_hook()
    {
        var hook = new FakeHook();
        using var manager = new ShortcutManager(new FakeHotkeys(), hook);

        manager.Apply(ShortcutBindings.ReadAll(new AppSettings()));

        Assert.Empty(hook.Bindings);
    }
}
