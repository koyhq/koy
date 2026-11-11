using Koy.Core.Settings;
using Koy.Core.Timers;
using Koy.Core.Weather;

namespace Koy.Core.Tests;

public class TimerAndWeatherSettingsTests
{
    [Theory]
    [InlineData(WeatherRefreshInterval.Every30Minutes, 30)]
    [InlineData(WeatherRefreshInterval.Hourly, 60)]
    public void Weather_cache_duration_follows_the_setting(WeatherRefreshInterval setting, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), WeatherRefreshPolicy.CacheDuration(setting));
    }

    [Fact]
    public void Weather_startup_only_never_expires()
    {
        var duration = WeatherRefreshPolicy.CacheDuration(WeatherRefreshInterval.OnStartup);

        Assert.True(TimeSpan.FromDays(3650) < duration);
    }

    [Fact]
    public void Weather_default_setting_is_thirty_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), WeatherRefreshPolicy.CacheDuration(new AppSettings().Weather.RefreshInterval));
    }

    [Fact]
    public void Pomodoro_settings_defaults_match_classic_25_5_15()
    {
        var s = new AppSettings();
        var config = PomodoroConfig.FromMinutes(s.Focus.PomodoroWorkMinutes, s.Focus.PomodoroShortBreakMinutes, s.Focus.PomodoroLongBreakMinutes);

        Assert.Equal(PomodoroConfig.Default, config);
    }

    [Fact]
    public void Pomodoro_config_uses_custom_minutes()
    {
        var config = PomodoroConfig.FromMinutes(50, 10, 30);

        Assert.Equal(TimeSpan.FromMinutes(50), config.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(10), config.ShortBreakDuration);
        Assert.Equal(TimeSpan.FromMinutes(30), config.LongBreakDuration);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100000, 180)]
    public void Pomodoro_config_clamps_absurd_values(int minutes, int expected)
    {
        var config = PomodoroConfig.FromMinutes(minutes, minutes, minutes);

        Assert.Equal(TimeSpan.FromMinutes(expected), config.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(expected), config.ShortBreakDuration);
        Assert.Equal(TimeSpan.FromMinutes(expected), config.LongBreakDuration);
    }

    [Fact]
    public void Pomodoro_starts_with_the_configured_work_duration()
    {
        using var pomodoro = new Pomodoro(PomodoroConfig.FromMinutes(40, 8, 20));

        Assert.Equal(TimeSpan.FromMinutes(40), pomodoro.Remaining);
    }
}
