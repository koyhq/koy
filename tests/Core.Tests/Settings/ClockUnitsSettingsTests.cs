using Koy.Core.Clock;
using Koy.Core.Settings;
using Koy.Core.Tray;
using Koy.Core.Units;

namespace Koy.Core.Tests.Settings;

/// <summary>Saat, birim ve tepsi ayarlarının varsayılanları eski görünümü korur ve dosyada gidip gelir.</summary>
public sealed class ClockUnitsSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Defaults_keep_the_current_look()
    {
        var settings = new AppSettings();

        Assert.Equal(ClockHourFormat.Auto, settings.Notch.ClockHourFormat);
        Assert.False(settings.Notch.ClockShowSeconds);
        Assert.False(settings.Notch.ClockShowDate);
        Assert.False(settings.Notch.ClockShowWeekNumber);
        Assert.Equal(WeekStartSetting.System, settings.Notch.ClockFirstDayOfWeek);
        Assert.Equal("", settings.Notch.ClockCustomDateFormat);
        Assert.Equal("", settings.Notch.ClockSecondTimeZoneId);

        Assert.Equal(TemperatureUnit.Celsius, settings.General.TemperatureUnit);
        Assert.Equal(WindSpeedUnit.KilometersPerHour, settings.General.WindSpeedUnit);
        Assert.Equal(NetworkSpeedUnit.Bytes, settings.General.NetworkSpeedUnit);

        Assert.Equal(TrayIconVisibility.Overflow, settings.Dock.NewTrayIconVisibility);
        Assert.Empty(settings.Dock.TrayAppVisibility);
    }

    [Fact]
    public void New_fields_round_trip_through_the_settings_file()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        var settings = new AppSettings
        {
            Notch = new NotchSettings
            {
                ClockHourFormat = ClockHourFormat.Hour12,
                ClockShowSeconds = true,
                ClockShowDate = true,
                ClockShowWeekNumber = true,
                ClockFirstDayOfWeek = WeekStartSetting.Monday,
                ClockCustomDateFormat = "dd.MM.yyyy",
                ClockSecondTimeZoneId = "Tokyo Standard Time",
            },
            General = new GeneralSettings
            {
                TemperatureUnit = TemperatureUnit.Fahrenheit,
                WindSpeedUnit = WindSpeedUnit.Knots,
                NetworkSpeedUnit = NetworkSpeedUnit.Bits,
            },
            Dock = new DockSettings
            {
                NewTrayIconVisibility = TrayIconVisibility.Hidden,
                TrayAppVisibility = new ValueDictionary<TrayIconVisibility> { ["discord.exe"] = TrayIconVisibility.Shown },
            },
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Enum_values_are_written_as_names()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        store.Save(new AppSettings { General = new GeneralSettings { TemperatureUnit = TemperatureUnit.Fahrenheit } });

        var text = File.ReadAllText(Path.Combine(_dir, "settings.json"));

        Assert.Contains("\"TemperatureUnit\": \"Fahrenheit\"", text);
    }

    [Fact]
    public void Tray_app_visibility_changes_settings_equality()
    {
        var empty = new AppSettings();
        var changed = new AppSettings
        {
            Dock = new DockSettings { TrayAppVisibility = new ValueDictionary<TrayIconVisibility> { ["a.exe"] = TrayIconVisibility.Hidden } }
        };

        Assert.NotEqual(empty, changed);
        Assert.NotEqual(empty.GetHashCode(), changed.GetHashCode());
    }
}
