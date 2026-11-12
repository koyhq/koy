using Koy.Core.Settings;
using Koy.Core.Shortcuts;

namespace Koy.Core.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+N", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4E)]
    [InlineData("ctrl+alt+space", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20)]
    [InlineData("Win+Shift+F5", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x74)]
    [InlineData("Control + Alt + 7", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x37)]
    [InlineData("Alt+PageDown", HotkeyModifiers.Alt, 0x22)]
    [InlineData("N", HotkeyModifiers.None, 0x4E)]
    [InlineData("F9", HotkeyModifiers.None, 0x78)]
    [InlineData("Shift+N", HotkeyModifiers.Shift, 0x4E)]
    [InlineData("Win", HotkeyModifiers.Win, 0)]
    [InlineData("Win+Shift", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0)]
    [InlineData("Ctrl+Alt", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0)]
    public void TryParse_accepts_valid_text(string text, HotkeyModifiers modifiers, int vk)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey(modifiers, vk), hotkey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Alt+N+M")]   // iki ana tuş
    [InlineData("Ctrl++N")]
    [InlineData("Ctrl+Alt+Foo")]
    [InlineData("Ctrl+F25")]
    public void TryParse_rejects_invalid_text(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void TryParse_rejects_null()
    {
        Assert.False(Hotkey.TryParse(null, out _));
    }

    [Theory]
    [InlineData("Ctrl+Alt+N")]
    [InlineData("Ctrl+Alt+Shift+Win+F12")]
    [InlineData("Alt+Left")]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Win+Tab")]
    [InlineData("Win")]
    [InlineData("Shift+Win")]
    [InlineData("F9")]
    [InlineData("Shift+N")]
    public void Format_then_parse_round_trips(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(text, hotkey.Format());
        Assert.True(Hotkey.TryParse(hotkey.Format(), out var again));
        Assert.Equal(hotkey, again);
    }

    [Fact]
    public void Format_orders_modifiers_canonically()
    {
        Assert.True(Hotkey.TryParse("Win+Alt+Ctrl+Shift+A", out var hotkey));
        Assert.Equal("Ctrl+Alt+Shift+Win+A", hotkey.Format());
    }

    [Fact]
    public void ModifierKeys_are_not_valid_main_keys()
    {
        // VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN
        foreach (var vk in new[] { 0x11, 0x10, 0x12, 0x5B })
            Assert.False(new Hotkey(HotkeyModifiers.Control, vk).IsValid);
    }

    [Fact]
    public void Modifier_values_match_win32_mod_flags()
    {
        Assert.Equal(1, (int)HotkeyModifiers.Alt);
        Assert.Equal(2, (int)HotkeyModifiers.Control);
        Assert.Equal(4, (int)HotkeyModifiers.Shift);
        Assert.Equal(8, (int)HotkeyModifiers.Win);
    }
}

public class ShortcutDefaultsTests
{
    [Fact]
    public void Defaults_parse_are_unique_and_avoid_win_shortcuts()
    {
        var seen = new HashSet<Hotkey>();
        foreach (var action in ShortcutBindings.All)
        {
            // Boş varsayılan = atanmamış (kullanıcı kendisi bağlar); yalnız dolu olanlar çözülür ve tekil olmalı.
            if (string.IsNullOrEmpty(ShortcutBindings.DefaultText(action)))
                continue;

            Assert.True(Hotkey.TryParse(ShortcutBindings.DefaultText(action), out var hotkey), action.ToString());
            Assert.True(seen.Add(hotkey), $"{action} varsayılanı başka eylemle aynı");
            // Arama paneli proje sahibinin kararıyla Win+Shift+Q kullanır; sistem çakışması yine aşağıda denetlenir.
            if (action != ShortcutAction.OpenSearch)
                Assert.False((hotkey.Modifiers & HotkeyModifiers.Win) != 0, $"{action} Win kullanıyor");
            Assert.False(ShortcutConflicts.IsReservedBySystem(hotkey), $"{action} sistem kısayoluyla çakışıyor");
        }
    }

    [Theory]
    [InlineData("Win+Shift+Q", true)]
    [InlineData("Shift+Win+Q", true)]
    [InlineData("win + shift + q", true)]
    [InlineData("Win+Shift+Z", false)]
    [InlineData("Alt+F2", false)]
    [InlineData("", false)]
    public void IsDefault_compares_the_combination_not_the_spelling(string text, bool expected)
    {
        Assert.Equal(expected, ShortcutBindings.IsDefault(ShortcutAction.OpenSearch, text));
    }

    [Fact]
    public void AppSettings_defaults_match_ShortcutBindings_defaults()
    {
        var settings = new AppSettings();
        foreach (var action in ShortcutBindings.All)
            Assert.Equal(ShortcutBindings.DefaultText(action), ShortcutBindings.GetText(settings, action));
    }

    [Fact]
    public void WithText_changes_only_the_given_action()
    {
        var settings = new AppSettings();
        var changed = ShortcutBindings.WithText(settings, ShortcutAction.OpenShelf, "Ctrl+Alt+Q");

        Assert.Equal("Ctrl+Alt+Q", ShortcutBindings.GetText(changed, ShortcutAction.OpenShelf));
        Assert.Equal(ShortcutBindings.GetText(settings, ShortcutAction.OpenMenu), ShortcutBindings.GetText(changed, ShortcutAction.OpenMenu));
        Assert.NotEqual(settings, changed);
    }

    [Fact]
    public void Shortcuts_survive_settings_save_and_load()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-shortcut-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            var settings = ShortcutBindings.WithText(new AppSettings(), ShortcutAction.PlayPause, "Ctrl+Shift+F9");
            settings = ShortcutBindings.WithText(settings, ShortcutAction.OpenMenu, "");

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal("Ctrl+Shift+F9", loaded.Shortcuts.PlayPause);
            Assert.Equal("", loaded.Shortcuts.OpenMenu);
            Assert.Equal(settings, loaded);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}

public class ShortcutConflictsTests
{
    private static Hotkey Parse(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey), text);
        return hotkey;
    }

    [Theory]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("Win+L")]
    public void Only_combinations_windows_never_gives_up_are_reserved(string text)
    {
        Assert.True(ShortcutConflicts.IsReservedBySystem(Parse(text)));
    }

    [Theory]
    [InlineData("Alt+Tab")]
    [InlineData("Alt+F4")]
    [InlineData("Ctrl+Esc")]
    [InlineData("Ctrl+Shift+Esc")]
    [InlineData("Win+D")]
    [InlineData("Win+S")]
    [InlineData("Win+Tab")]
    [InlineData("Win+Shift+S")]
    public void Windows_shortcuts_can_be_overridden(string text)
    {
        Assert.False(ShortcutConflicts.IsReservedBySystem(Parse(text)));
        Assert.True(ShortcutConflicts.IsWindowsOwned(Parse(text)));
    }

    [Theory]
    [InlineData("Ctrl+Alt+N")]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Ctrl+Shift+F9")]
    [InlineData("Win+Alt+K")]
    public void Ordinary_shortcuts_are_not_reserved(string text)
    {
        Assert.False(ShortcutConflicts.IsReservedBySystem(Parse(text)));
    }

    [Fact]
    public void Check_reports_duplicate_with_the_other_action()
    {
        var current = ShortcutBindings.ReadAll(new AppSettings());

        var result = ShortcutConflicts.Check(Parse("Ctrl+Alt+R"), ShortcutAction.ToggleNotch, current);

        Assert.NotNull(result);
        Assert.Equal(ShortcutStatus.DuplicateOfOtherAction, result.Status);
        Assert.Equal(ShortcutAction.OpenShelf, result.ConflictsWith);
    }

    [Fact]
    public void Check_allows_keeping_the_own_shortcut()
    {
        var current = ShortcutBindings.ReadAll(new AppSettings());

        Assert.Null(ShortcutConflicts.Check(Parse("Ctrl+Alt+N"), ShortcutAction.ToggleNotch, current));
    }

    [Fact]
    public void Check_rejects_reserved_and_invalid()
    {
        var current = ShortcutBindings.ReadAll(new AppSettings());

        Assert.Equal(ShortcutStatus.ReservedBySystem, ShortcutConflicts.Check(Parse("Win+L"), ShortcutAction.OpenMenu, current)?.Status);
        Assert.Equal(ShortcutStatus.Invalid, ShortcutConflicts.Check(default, ShortcutAction.OpenMenu, current)?.Status);
    }
}

public class ShortcutManagerTests
{
    private sealed class FakeHotkeyService : IHotkeyService
    {
        public Dictionary<int, Hotkey> Registered { get; } = [];
        public HashSet<Hotkey> TakenByOthers { get; } = [];
        public List<string> Log { get; } = [];
        public bool Disposed { get; private set; }

        public event EventHandler<int>? HotkeyPressed;

        public HotkeyRegistrationResult Register(int id, Hotkey hotkey)
        {
            Log.Add("reg " + hotkey);
            if (TakenByOthers.Contains(hotkey))
                return HotkeyRegistrationResult.AlreadyInUse;
            Registered[id] = hotkey;
            return HotkeyRegistrationResult.Success;
        }

        public void Unregister(int id)
        {
            Log.Add("unreg " + id);
            Registered.Remove(id);
        }

        public void Press(int id) => HotkeyPressed?.Invoke(this, id);

        public void Dispose() => Disposed = true;
    }

    /// <summary>Varsayılanı dolu (kayıt edilecek) eylem sayısı; boş varsayılanlar atanmamış gelir.</summary>
    private static int DefaultBoundCount =>
        ShortcutBindings.All.Count(a => ShortcutBindings.DefaultText(a).Length > 0);

    private static Dictionary<ShortcutAction, string> Desired(AppSettings? settings = null) =>
        ShortcutBindings.ReadAll(settings ?? new AppSettings());

    [Fact]
    public void Apply_registers_all_defaults()
    {
        var fake = new FakeHotkeyService();
        using var manager = new ShortcutManager(fake);

        var results = manager.Apply(Desired());

        var withDefault = ShortcutBindings.All.Where(a => ShortcutBindings.DefaultText(a).Length > 0).ToList();
        Assert.Equal(withDefault.Count, fake.Registered.Count);
        Assert.All(withDefault, a => Assert.Equal(ShortcutStatus.Registered, results[a].Status));
        Assert.Equal(ShortcutStatus.Unassigned, results[ShortcutAction.OpenKoyWindow].Status);
    }

    [Fact]
    public void Apply_reports_other_app_holding_the_shortcut_and_keeps_the_rest()
    {
        var fake = new FakeHotkeyService();
        Assert.True(Hotkey.TryParse("Ctrl+Alt+R", out var taken));
        fake.TakenByOthers.Add(taken);
        using var manager = new ShortcutManager(fake);

        var results = manager.Apply(Desired());

        Assert.Equal(ShortcutStatus.InUseByOtherApp, results[ShortcutAction.OpenShelf].Status);
        Assert.True(results[ShortcutAction.OpenShelf].IsError);
        Assert.Equal(ShortcutStatus.Registered, results[ShortcutAction.ToggleNotch].Status);
        Assert.Equal(DefaultBoundCount - 1, fake.Registered.Count);
    }

    [Fact]
    public void Apply_treats_empty_text_as_unassigned_without_error()
    {
        var fake = new FakeHotkeyService();
        using var manager = new ShortcutManager(fake);
        var settings = ShortcutBindings.WithText(new AppSettings(), ShortcutAction.OpenMenu, "");

        var results = manager.Apply(Desired(settings));

        Assert.Equal(ShortcutStatus.Unassigned, results[ShortcutAction.OpenMenu].Status);
        Assert.False(results[ShortcutAction.OpenMenu].IsError);
        Assert.Equal(DefaultBoundCount - 1, fake.Registered.Count);
    }

    [Fact]
    public void Apply_marks_unparseable_text_invalid_and_never_registers_it()
    {
        var fake = new FakeHotkeyService();
        using var manager = new ShortcutManager(fake);
        var settings = ShortcutBindings.WithText(new AppSettings(), ShortcutAction.OpenMenu, "Ctrl+Banana");

        var results = manager.Apply(Desired(settings));

        Assert.Equal(ShortcutStatus.Invalid, results[ShortcutAction.OpenMenu].Status);
        Assert.DoesNotContain(fake.Log, l => l.Contains("Banana"));
    }

    [Fact]
    public void Apply_gives_a_duplicate_to_the_first_action_only()
    {
        var fake = new FakeHotkeyService();
        using var manager = new ShortcutManager(fake);
        var settings = ShortcutBindings.WithText(new AppSettings(), ShortcutAction.OpenMenu, "Ctrl+Alt+N");

        var results = manager.Apply(Desired(settings));

        Assert.Equal(ShortcutStatus.Registered, results[ShortcutAction.ToggleNotch].Status);
        Assert.Equal(ShortcutStatus.DuplicateOfOtherAction, results[ShortcutAction.OpenMenu].Status);
        Assert.Equal(ShortcutAction.ToggleNotch, results[ShortcutAction.OpenMenu].ConflictsWith);
    }

    [Fact]
    public void Apply_does_not_try_to_register_system_reserved_shortcuts()
    {
        var fake = new FakeHotkeyService();
        using var manager = new ShortcutManager(fake);
        var settings = ShortcutBindings.WithText(new AppSettings(), ShortcutAction.OpenMenu, "Win+L");

        var results = manager.Apply(Desired(settings));

        Assert.Equal(ShortcutStatus.ReservedBySystem, results[ShortcutAction.OpenMenu].Status);
        Assert.DoesNotContain(fake.Log, l => l == "reg Win+L");
    }

    [Fact]
    public void Reapply_unregisters_old_registrations_first()
    {
        var fake = new FakeHotkeyService();
        using var manager = new ShortcutManager(fake);
        manager.Apply(Desired());
        fake.Log.Clear();

        var settings = ShortcutBindings.WithText(new AppSettings(), ShortcutAction.ToggleNotch, "Ctrl+Alt+Q");
        manager.Apply(Desired(settings));

        Assert.Equal("unreg", fake.Log[0].Split(' ')[0]);
        Assert.Equal(DefaultBoundCount, fake.Registered.Count);
        Assert.Contains(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x51), fake.Registered.Values);
        Assert.DoesNotContain(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4E), fake.Registered.Values);
    }

    [Fact]
    public void Pressing_a_registered_hotkey_raises_Triggered_with_its_action()
    {
        var fake = new FakeHotkeyService();
        using var manager = new ShortcutManager(fake);
        manager.Apply(Desired());
        var triggered = new List<ShortcutAction>();
        manager.Triggered += (_, a) => triggered.Add(a);

        var id = fake.Registered.Single(kv => kv.Value == new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20)).Key;
        fake.Press(id);

        Assert.Equal([ShortcutAction.PlayPause], triggered);
    }

    [Fact]
    public void Pressing_an_unregistered_id_is_ignored()
    {
        var fake = new FakeHotkeyService();
        using var manager = new ShortcutManager(fake);
        manager.Apply(Desired(ShortcutBindings.WithText(new AppSettings(), ShortcutAction.OpenMenu, "")));
        var triggered = new List<ShortcutAction>();
        manager.Triggered += (_, a) => triggered.Add(a);

        fake.Press((int)ShortcutAction.OpenMenu + 1);
        fake.Press(999);

        Assert.Empty(triggered);
    }

    [Fact]
    public void Dispose_unregisters_everything_and_disposes_the_service()
    {
        var fake = new FakeHotkeyService();
        var manager = new ShortcutManager(fake);
        manager.Apply(Desired());

        manager.Dispose();

        Assert.Empty(fake.Registered);
        Assert.True(fake.Disposed);
    }
}
