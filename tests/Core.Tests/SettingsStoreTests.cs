using Koy.Core.Notch;
using Koy.Core.Dock;
using Koy.Core.Appearance;
using Koy.Core.Settings;
using Koy.Core.Visibility;

namespace Koy.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-tests-" + Guid.NewGuid());

    private SettingsStore NewStore() => new(Path.Combine(_dir, "settings.json"));

    [Fact]
    public void Missing_file_returns_defaults()
    {
        Assert.Equal(new AppSettings(), NewStore().Load());
    }

    [Fact]
    public void Round_trips()
    {
        var store = NewStore();
        var settings = new AppSettings { General = new GeneralSettings { Language = "en" }, Dock = new DockSettings { Visibility = VisibilityMode.Peek }, Appearance = new AppearanceSettings { AccentColor = "#FF8800" } };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Partial_file_keeps_defaults_for_missing_fields()
    {
        // Eski ya da elle yazılmış dosyada olmayan alanlar varsayılanını korumalı (NotchEnabled false olursa çentik hiç açılmaz).
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "DockEnabled": true }""");

        var loaded = NewStore().Load();

        Assert.Equal(new AppSettings { Dock = new DockSettings { Enabled = true } }, loaded);
    }

    [Fact]
    public void Corrupt_file_returns_defaults()
    {
        var store = NewStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.Path, "{ bozuk");

        Assert.Equal(new AppSettings(), store.Load());
    }

    [Fact]
    public void Enums_are_written_as_text()
    {
        var store = NewStore();
        store.Save(new AppSettings { Dock = new DockSettings { Visibility = VisibilityMode.Smart } });

        Assert.Contains("\"Smart\"", File.ReadAllText(store.Path));
    }

    [Fact]
    public void Round_trips_with_custom_panels()
    {
        var store = NewStore();
        var settings = new AppSettings { General = new GeneralSettings { Language = "en" }, Notch = new NotchSettings { EnabledPanels = ["music", "calendar"], ControlCenterLayout = ["bluetooth", "wifi"] } };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings, loaded);
        Assert.Equal(["music", "calendar"], loaded.Notch.EnabledPanels);
    }

    [Fact]
    public void Round_trips_with_dock_and_monitor_settings()
    {
        var store = NewStore();
        var settings = new AppSettings { Dock = new DockSettings { Enabled = true, Visibility = VisibilityMode.Fixed, HoverMagnify = false, HideInFullscreen = false } };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings, loaded);
        Assert.True(loaded.Dock.Enabled);
        Assert.Equal(VisibilityMode.Fixed, loaded.Dock.Visibility);
        Assert.False(loaded.Dock.HoverMagnify);
        Assert.False(loaded.Dock.HideInFullscreen);
    }

    [Fact]
    public void Equals_HandlesNullListsGracefully()
    {
        var s1 = new AppSettings { Notch = new NotchSettings { ControlCenterLayout = null!, EnabledPanels = null! } };
        var s2 = new AppSettings();

        Assert.False(s1.Equals(s2));
        Assert.False(s2.Equals(s1));
        Assert.True(s1.Equals(s1));
    }

    [Fact]
    public void Round_trips_with_first_run_completed_and_theme()
    {
        var store = NewStore();
        var settings = new AppSettings { General = new GeneralSettings { FirstRunCompleted = true }, Appearance = new AppearanceSettings { Theme = AppTheme.Light, CornerRadius = 14.0, AnimationSpeed = AnimationSpeed.Fast } };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings, loaded);
        Assert.True(loaded.General.FirstRunCompleted);
        Assert.Equal(AppTheme.Light, loaded.Appearance.Theme);
        Assert.Equal(14.0, loaded.Appearance.CornerRadius);
    }

    [Fact]
    public void Round_trips_with_new_ui_settings()
    {
        var store = NewStore();
        var settings = new AppSettings { Dock = new DockSettings { IconSize = 24.0, MiddleClickAction = MiddleClickAction.Close, IndicatorStyle = DockIndicatorStyle.Line }, Notch = new NotchSettings { IdleDisplay = IdleDisplayMode.ClockOnly, OpenBehavior = NotchOpenBehavior.Hover, WheelAction = WheelAction.Volume }, Appearance = new AppearanceSettings { Material = MaterialLevel.Frosted } };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings, loaded);
        Assert.Equal(24.0, loaded.Dock.IconSize);
        Assert.Equal(IdleDisplayMode.ClockOnly, loaded.Notch.IdleDisplay);
        Assert.Equal(NotchOpenBehavior.Hover, loaded.Notch.OpenBehavior);
        Assert.Equal(MaterialLevel.Frosted, loaded.Appearance.Material);
        Assert.Equal(WheelAction.Volume, loaded.Notch.WheelAction);
        Assert.Equal(MiddleClickAction.Close, loaded.Dock.MiddleClickAction);
        Assert.Equal(DockIndicatorStyle.Line, loaded.Dock.IndicatorStyle);
    }

    [Fact]
    public void Default_dock_proximity_is_ReserveSpace()
    {
        Assert.Equal(Koy.Core.Dock.DockProximityMode.ReserveSpace, new AppSettings().Dock.Proximity);
        Assert.Equal(Koy.Core.Dock.DockProximityMode.ReserveSpace, NewStore().Load().Dock.Proximity);
    }

    [Fact]
    public void Dock_proximity_missing_in_existing_file_gets_new_default()
    {
        var store = NewStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.Path, """{ "DockEnabled": true }""");

        Assert.Equal(Koy.Core.Dock.DockProximityMode.ReserveSpace, store.Load().Dock.Proximity);
    }

    [Fact]
    public void DockDown_is_kept_on_load_and_file_is_untouched()
    {
        var store = NewStore();
        Directory.CreateDirectory(_dir);
        const string json = """{ "DockProximity": "DockDown" }""";
        File.WriteAllText(store.Path, json);

        var loaded = store.Load();

        Assert.Equal(Koy.Core.Dock.DockProximityMode.DockDown, loaded.Dock.Proximity);
        Assert.Equal(json, File.ReadAllText(store.Path));
    }

    [Fact]
    public void Dodge_stays_selectable()
    {
        var store = NewStore();
        store.Save(new AppSettings { Dock = new DockSettings { Proximity = Koy.Core.Dock.DockProximityMode.Dodge } });

        Assert.Equal(Koy.Core.Dock.DockProximityMode.Dodge, store.Load().Dock.Proximity);
    }

    [Fact]
    public void AlwaysFloating_round_trips_and_is_written_by_name()
    {
        // Enum ADIYLA yazılır (UseStringEnumConverter): yeni değerin sayısı eski dosyaları etkilemez.
        var store = NewStore();
        store.Save(new AppSettings { Dock = new DockSettings { Proximity = Koy.Core.Dock.DockProximityMode.AlwaysFloating } });

        Assert.Contains("\"Proximity\": \"AlwaysFloating\"", File.ReadAllText(store.Path));
        Assert.Equal(Koy.Core.Dock.DockProximityMode.AlwaysFloating, store.Load().Dock.Proximity);
    }

    [Theory]
    [InlineData("ReserveSpace", Koy.Core.Dock.DockProximityMode.ReserveSpace)]
    [InlineData("DockDown", Koy.Core.Dock.DockProximityMode.DockDown)]
    [InlineData("Dodge", Koy.Core.Dock.DockProximityMode.Dodge)]
    [InlineData("AlwaysHidden", Koy.Core.Dock.DockProximityMode.AlwaysHidden)]
    [InlineData("AlwaysFloating", Koy.Core.Dock.DockProximityMode.AlwaysFloating)]
    public void Existing_proximity_names_load_unchanged_and_file_is_untouched(string name, Koy.Core.Dock.DockProximityMode expected)
    {
        // Eski dosyalar göçsüz okunur: bilinen adlar aynı değere döner, dosya yeniden yazılmaz.
        var store = NewStore();
        Directory.CreateDirectory(_dir);
        var json = "{ \"DockProximity\": \"" + name + "\" }";
        File.WriteAllText(store.Path, json);

        Assert.Equal(expected, store.Load().Dock.Proximity);
        Assert.Equal(json, File.ReadAllText(store.Path));
    }

    [Fact]
    public void Existing_numeric_proximity_values_keep_their_meaning()
    {
        // Elle ya da eski sürümle sayı yazılmış olsa bile yeni değer en sonda olduğu için anlamlar kaymaz.
        Assert.Equal(0, (int)Koy.Core.Dock.DockProximityMode.DockDown);
        Assert.Equal(1, (int)Koy.Core.Dock.DockProximityMode.ReserveSpace);
        Assert.Equal(2, (int)Koy.Core.Dock.DockProximityMode.Dodge);
        Assert.Equal(3, (int)Koy.Core.Dock.DockProximityMode.AlwaysHidden);
        Assert.Equal(4, (int)Koy.Core.Dock.DockProximityMode.AlwaysFloating);
    }

    [Fact]
    public void DockShowTray_DefaultsOnForOldFilesAndRoundTripsOff()
    {
        // Tepsi alanı olmayan eski dosyada tepsi açık gelmeli; kapatılınca kapalı kalmalı.
        var store = NewStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.Path, """{ "DockEnabled": true }""");
        Assert.True(store.Load().Dock.ShowTray);

        store.Save(new AppSettings { Dock = new DockSettings { ShowTray = false } });
        var loaded = store.Load();

        Assert.False(loaded.Dock.ShowTray);
        Assert.NotEqual(new AppSettings(), loaded);
    }

    [Fact]
    public void VoiceSettings_RoundTrips_WithJsonContext()
    {
        var store = NewStore();
        var original = new AppSettings { Shortcuts = new ShortcutsSettings { VoiceCommand = "Ctrl+Shift+V" }, Voice = new VoiceSettings { Harness = Koy.Core.Voice.VoiceHarnessType.Codex, CustomCommand = "my-ai {prompt}", WorkingDirectory = @"C:\Workspace", TimeoutSeconds = 120 } };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original, loaded);
        Assert.Equal("Ctrl+Shift+V", loaded.Shortcuts.VoiceCommand);
        Assert.Equal(Koy.Core.Voice.VoiceHarnessType.Codex, loaded.Voice.Harness);
        Assert.Equal("my-ai {prompt}", loaded.Voice.CustomCommand);
        Assert.Equal(@"C:\Workspace", loaded.Voice.WorkingDirectory);
        Assert.Equal(120, loaded.Voice.TimeoutSeconds);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
