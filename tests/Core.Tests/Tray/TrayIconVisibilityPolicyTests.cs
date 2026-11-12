using Koy.Core.Settings;
using Koy.Core.Tray;

namespace Koy.Core.Tests.Tray;

public class TrayIconVisibilityPolicyTests
{
    [Fact]
    public void New_icons_default_to_overflow()
        => Assert.Equal(TrayIconVisibility.Overflow, TrayIconVisibilityPolicy.DefaultNewIcon);

    [Fact]
    public void Unknown_app_gets_the_new_icon_default()
    {
        var perApp = new ValueDictionary<TrayIconVisibility>();

        Assert.Equal(TrayIconVisibility.Hidden, TrayIconVisibilityPolicy.Resolve("unknown.exe", perApp, TrayIconVisibility.Hidden));
        Assert.Equal(TrayIconVisibility.Overflow, TrayIconVisibilityPolicy.Resolve("unknown.exe", perApp, TrayIconVisibilityPolicy.DefaultNewIcon));
    }

    [Fact]
    public void Per_app_record_wins_over_the_default()
    {
        var perApp = new ValueDictionary<TrayIconVisibility> { ["discord.exe"] = TrayIconVisibility.Shown };

        Assert.Equal(TrayIconVisibility.Shown, TrayIconVisibilityPolicy.Resolve("discord.exe", perApp, TrayIconVisibility.Hidden));
    }

    [Fact]
    public void App_key_ignores_case_and_surrounding_spaces()
    {
        var perApp = new ValueDictionary<TrayIconVisibility> { [TrayIconVisibilityPolicy.AppKey("  Discord.EXE ")] = TrayIconVisibility.Hidden };

        Assert.Equal("discord.exe", TrayIconVisibilityPolicy.AppKey("  Discord.EXE "));
        Assert.Equal(TrayIconVisibility.Hidden, TrayIconVisibilityPolicy.Resolve("DISCORD.exe", perApp, TrayIconVisibility.Shown));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_exe_name_falls_back_to_the_default(string? exe)
    {
        var perApp = new ValueDictionary<TrayIconVisibility> { [""] = TrayIconVisibility.Hidden };

        Assert.Equal(TrayIconVisibility.Shown, TrayIconVisibilityPolicy.Resolve(exe, perApp, TrayIconVisibility.Shown));
    }

    [Fact]
    public void Settings_list_merges_seen_and_saved_apps_as_sorted_keys()
    {
        var apps = TrayIconVisibilityPolicy.SettingsApps(["Steam.exe", null, " ", "discord.exe"], ["DISCORD.EXE", "onedrive.exe"]);

        Assert.Equal(["discord.exe", "onedrive.exe", "steam.exe"], apps);
    }
}
