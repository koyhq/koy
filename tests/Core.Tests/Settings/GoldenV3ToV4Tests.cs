using Koy.Core.Appearance;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Input;
using Koy.Core.Media;
using Koy.Core.Notch;
using Koy.Core.Search;
using Koy.Core.Settings;
using Koy.Core.Visibility;
using Koy.Core.Voice;
using Koy.Core.Weather;

namespace Koy.Core.Tests.Settings;

/// <summary>
/// Altın göç: v0.2.6'nın yazdığı v3 dosyasındaki HER alan, v4'te beklenen bölüm ve alana gelmeli.
/// Bir alan göçte kaybolursa ya da yanlış bölüme düşerse bu test kırılır.
/// </summary>
public class GoldenV3ToV4Tests
{
    private static readonly AppSettings S = SettingsFile.Load(SettingsFile.FixtureV3);

    [Fact]
    public void Version_is_upgraded_to_current()
        => Assert.Equal(AppSettings.CurrentVersion, S.Version);

    [Fact]
    public void General_section()
    {
        Assert.Equal("tr", S.General.Language);
        Assert.True(S.General.FirstRunCompleted);
        Assert.Equal("0.2.5", S.General.LastSeenVersion);
        Assert.False(S.General.AutoCheckUpdates);
        Assert.Equal(UpdateChannel.Preview, S.General.UpdateChannel);
    }

    [Fact]
    public void Appearance_section()
    {
        Assert.Equal(AppTheme.Light, S.Appearance.Theme);
        Assert.Equal("#FF8800", S.Appearance.AccentColor);
        Assert.True(S.Appearance.UseWindowsAccent);
        Assert.Equal(17.0, S.Appearance.CornerRadius);
        Assert.Equal(AnimationSpeed.Fast, S.Appearance.AnimationSpeed);
        Assert.False(S.Appearance.RespectReduceMotion);
        Assert.False(S.Appearance.BatterySaverReduceMotion);
        Assert.Equal(MaterialLevel.Glass, S.Appearance.Material);
        Assert.False(S.Appearance.MaterialReadabilityGuard);
    }

    [Fact]
    public void Dock_section()
    {
        Assert.False(S.Dock.Enabled);
        Assert.False(S.Dock.HideWindowsTaskbar);
        Assert.Equal(VisibilityMode.Smart, S.Dock.Visibility);
        Assert.Equal(DockStyleMode.FullWidth, S.Dock.Style);
        Assert.Equal(DockIconAlignment.Right, S.Dock.IconAlignment);
        Assert.Equal(DockProximityMode.Dodge, S.Dock.Proximity);
        Assert.False(S.Dock.HideInFullscreen);
        Assert.False(S.Dock.EdgeReveal);
        Assert.False(S.Dock.HoverMagnify);
        Assert.False(S.Dock.ShowStartButton);
        Assert.Equal(DockStartIcon.Custom, S.Dock.StartIcon);
        Assert.Equal(@"C:\Icons\start.png", S.Dock.StartIconPath);
        Assert.False(S.Dock.ShowFolders);
        Assert.False(S.Dock.ShowRecents);
        Assert.False(S.Dock.ShowTray);
        Assert.False(S.Dock.SmallTray);
        Assert.False(S.Dock.ShowBadges);
        Assert.False(S.Dock.ThumbnailPreview);
        Assert.Equal(650, S.Dock.ThumbnailDelayMs);
        Assert.Equal(275, S.Dock.DodgeDelayMs);
        Assert.Equal(36.0, S.Dock.IconSize);
        Assert.Equal(DockIndicatorStyle.Line, S.Dock.IndicatorStyle);
        Assert.Equal(MiddleClickAction.Close, S.Dock.MiddleClickAction);
    }

    [Fact]
    public void DockItems_section_gets_pinned_order_and_folders()
    {
        Assert.Equal(new[] { "Obsidian.lnk", "Firefox.lnk", "Terminal.lnk" }, S.DockItems.PinnedOrder);
        Assert.Equal(2, S.DockItems.Folders.Count);
        Assert.Equal(new DockFolder("f1a2b3c4", "Araçlar", ["calc.lnk", "notepad.lnk"]), S.DockItems.Folders[0]);
        Assert.Equal(new DockFolder("d5e6f7a8", "Oyunlar", ["steam.lnk"]), S.DockItems.Folders[1]);
    }

    [Fact]
    public void Notch_section()
    {
        Assert.False(S.Notch.Enabled);
        Assert.Equal(VisibilityMode.Peek, S.Notch.Visibility);
        Assert.Equal(NotchStyleMode.Floating, S.Notch.Style);
        Assert.Equal(NotchOpenBehavior.Click, S.Notch.OpenBehavior);
        Assert.Equal(IdleDisplayMode.RingClockDay, S.Notch.IdleDisplay);
        Assert.Equal(WheelAction.Volume, S.Notch.WheelAction);
        Assert.False(S.Notch.HideInFullscreen);
        Assert.False(S.Notch.FollowsMaterial);
        Assert.False(S.Notch.ShowVolumeOsd);
        Assert.False(S.Notch.ShowBrightnessOsd);
        Assert.False(S.Notch.ShowPrivacyIndicator);
        Assert.False(S.Notch.ShowNotifications);
        Assert.True(S.Notch.NotificationsHideBody);
        Assert.Equal(new[] { "home", "calendar", "system" }, S.Notch.EnabledPanels);
        Assert.Equal(new[] { "bluetooth", "wifi" }, S.Notch.ControlCenterLayout);
    }

    [Fact]
    public void Music_section()
    {
        Assert.False(S.Music.ShowWaveform);
        Assert.Equal(MusicCardStyle.Artwork, S.Music.CardStyle);
        Assert.True(S.Music.LyricsEnabled);
    }

    [Fact]
    public void Displays_section_keeps_monitor_records_and_default()
    {
        Assert.Equal(NewScreenMode.NotchOnly, S.Displays.NewScreenDefault);
        Assert.Equal(2, S.Displays.Monitors.Count);

        var office = S.Displays.Monitors["kimlik-1"];
        Assert.True(office.NotchEnabled);
        Assert.False(office.DockEnabled);
        Assert.Equal("Ofis", office.Name);

        var lounge = S.Displays.Monitors["kimlik-2"];
        Assert.False(lounge.NotchEnabled);
        Assert.True(lounge.DockEnabled);
        Assert.Equal("Salon", lounge.Name);
    }

    [Fact]
    public void Shortcuts_section()
    {
        Assert.Equal("Ctrl+Alt+1", S.Shortcuts.ToggleNotch);
        Assert.Equal("Ctrl+Alt+2", S.Shortcuts.OpenShelf);
        Assert.Equal("Ctrl+Alt+3", S.Shortcuts.ToggleDock);
        Assert.Equal("Ctrl+Alt+4", S.Shortcuts.OpenMenu);
        Assert.Equal("Ctrl+Alt+5", S.Shortcuts.PlayPause);
        Assert.Equal("Ctrl+Alt+6", S.Shortcuts.OpenSearch);
        Assert.Equal("Ctrl+Alt+7", S.Shortcuts.OpenSearchClipboard);
        Assert.Equal("Ctrl+Alt+8", S.Shortcuts.VoiceCommand);
    }

    [Fact]
    public void Voice_section()
    {
        Assert.Equal(VoiceHarnessType.Codex, S.Voice.Harness);
        Assert.Equal(VoiceClaudePermission.AcceptEdits, S.Voice.ClaudePermission);
        Assert.Equal("opus", S.Voice.Model);
        Assert.Equal("my-ai {prompt}", S.Voice.CustomCommand);
        Assert.Equal(@"C:\Workspace", S.Voice.WorkingDirectory);
        Assert.Equal(120, S.Voice.TimeoutSeconds);
    }

    [Fact]
    public void Search_section()
    {
        Assert.False(S.Search.RetainPrior);
        Assert.Equal(SearchHistoryMode.ImmediateCompletion, S.Search.HistoryMode);
        Assert.Equal(new[] { "bookmarks", "files" }, S.Search.DisabledSources);
        Assert.True(S.Search.Pinned);
    }

    [Fact]
    public void Focus_weather_and_clipboard_sections()
    {
        Assert.Equal(40, S.Focus.PomodoroWorkMinutes);
        Assert.Equal(7, S.Focus.PomodoroShortBreakMinutes);
        Assert.Equal(20, S.Focus.PomodoroLongBreakMinutes);

        Assert.Equal(WeatherRefreshInterval.Hourly, S.Weather.RefreshInterval);
        Assert.Equal(41.0082, S.Weather.Latitude);
        Assert.Equal(28.9784, S.Weather.Longitude);
        Assert.Equal("İstanbul", S.Weather.City);
        Assert.Equal(WeatherLocationSource.City, S.Weather.LocationSource);
        Assert.True(S.Weather.UseLocation);
        Assert.Equal(1762800000L, S.Weather.LocationUpdatedUnix);

        Assert.False(S.Clipboard.HistoryEnabled);
    }

    [Fact]
    public void HotCorners_section()
    {
        Assert.Equal(CornerAction.OpenNotch, S.HotCorners.TopLeft);
        Assert.Equal(CornerAction.OpenSearch, S.HotCorners.TopRight);
        Assert.Equal(CornerAction.OpenKoyWindow, S.HotCorners.BottomLeft);
        Assert.Equal(CornerAction.ToggleDock, S.HotCorners.BottomRight);
        Assert.Equal(700, S.HotCorners.DwellMs);
        Assert.False(S.HotCorners.DisableInFullscreen);
    }

    [Fact]
    public void QuickLaunch_section()
        => Assert.Equal(new[] { @"C:\Apps\a.exe", @"C:\Docs" }, S.QuickLaunch.Items);

}
