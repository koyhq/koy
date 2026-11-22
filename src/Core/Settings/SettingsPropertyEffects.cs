using System.Collections.Frozen;

namespace Koy.Core.Settings;

/// <summary>
/// Ayarlar penceresi ViewModel'inde bir özellik değişince ne olacağı: kaydedilir mi, tema yeniden uygulanır mı, dock ya da
/// çentik yeniden kurulur mu. Eskiden <c>KoyWindowViewModel.OnPropertyChanged</c> içinde ~110 adlık elle yazılmış
/// <c>or</c> zincirleriydi; şimdi açık, adlandırılmış kümeler ve test. Adlar ViewModel özellik adlarıdır; ayar alanı olanlar
/// düz v3 adlarıyla <see cref="SettingsKeyMap.V3ToV4"/> içinde de bulunur (testle doğrulanır). Yeni ViewModel özelliği
/// eklenince: ayara yazılmayacaksa <see cref="NotSaved"/>'a, çentik/dock'u etkiliyorsa ilgili kümeye eklenir.
/// </summary>
public static class SettingsPropertyEffects
{
    /// <summary>Ayara yazılmayan özellikler: yalnız arayüz durumu (sekme, arama, onay adımı, bellek ölçümü, hakkında metinleri).</summary>
    public static readonly IReadOnlySet<string> NotSaved = new[]
    {
        "SelectedPageIndex", "GeneralSubTabIndex", "NotchSubTabIndex", "DockSubTabIndex", "AppearanceSubTabIndex",
        "MotionTogglesActive", "MotionTogglesInactive", "ClockCustomDateFormatInvalid", "IsApplyRevertPending",
        "ApplyRevertSecondsLeft", "AdvancedRows", "AdvancedGroups", "SettingsFileInvalid", "SettingsSaveFailed",
        "AdvancedFilter", "HasTrayAppRows", "SettingsSearchQuery", "IsSettingsSearching", "SettingsSearchResults",
        "SettingsSearchNoResult", "ShowChangedOnly", "CanResetPage", "IsPageResetConfirming", "PageResetDone",
        "MemoryUsageMb", "MemoryUsageText", "MemoryPct", "MemoryLevel", "IsResetConfirming", "UninstallAvailability",
        "CanUninstall", "UninstallRowDescription", "UserStyleStatusText", "StartIconFileName", "IconPackFolderName",
        "IsUninstallConfirming", "KeepSettingsOnUninstall", "UninstallError", "IsCheckingUpdates", "LastCheckText",
        "AboutStatusText", "CheckButtonText", "CanCheckUpdates", "DownloadProgressText"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Değişince tema, vurgu, yazı ve ölçek kaynaklarının yeniden uygulandığı özellikler (çentik ve dock yeniden kurulmaz).</summary>
    public static readonly IReadOnlySet<string> AppliesTheme = new[]
    {
        "Theme", "AccentColor", "UseWindowsAccent", "Material", "MaterialReadabilityGuard", "NotchFollowsMaterial",
        "CornerRadius", "AnimationSpeed", "RespectReduceMotion", "BatterySaverReduceMotion", "MotionNotchOpenClose",
        "MotionTabTransitions", "MotionOsd", "MotionDockMagnify", "MotionFolderPanel", "UiFontFamily", "ClockFontFamily",
        "BaseFontSize", "UiFontWeight", "UiScalePercent", "UserStyleOverridesEnabled"
    }.ToFrozenSet(StringComparer.Ordinal);

    // Dock yeniden kurulan adlar: "Dock" ile başlayanların hepsi (DockEnabled hariç, o ayrı açma/kapama yolundan) ve şunlar.
    private static readonly FrozenSet<string> DockExtra = new[]
    {
        "HideWindowsTaskbar", "BatterySaverReduceMotion", "MiddleClickAction", "Material", "IconPack",
        "IconPackFolder", "MaterialReadabilityGuard", "AnimationSpeed", "RespectReduceMotion"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Değişince çentik yeniden kurulan özellikler (NotchEnabled hariç, o ayrı açma/kapama yolundan).</summary>
    public static readonly IReadOnlySet<string> RebuildsNotch = new[]
    {
        "NotchVisibility", "NotchHideInFullscreen", "NotchEdgeLine", "NotchEdgeLineColor", "NotchEdgeLineThickness",
        "MusicShowWaveform", "MusicCardStyle", "MusicLyricsEnabled", "ShowVolumeOsd", "ShowBrightnessOsd",
        "ShowPrivacyIndicator", "NotchShowNotifications", "NotchNotificationsHideBody", "IdleDisplayMode", "NotchStyle",
        "Material", "MaterialReadabilityGuard", "NotchFollowsMaterial", "NotchOpenBehavior", "WheelAction",
        "BatterySaverReduceMotion", "NotchOpenDelayMs", "NotchCloseDelayMs", "NotchTabBarStyle", "NotchTabOrderVersion",
        "HomeCardOrderVersion", "VisualizerStyle", "VisualizerColorSource", "VisualizerBarCount", "LiveBackgroundTarget",
        "LiveBackgroundSource", "LiveBackgroundPreset", "LiveBackgroundFilePath", "LiveBackgroundScrimPercent",
        "NotchCollapsedBackground", "WeatherRefreshInterval", "PomodoroWorkMinutes", "PomodoroShortBreakMinutes",
        "PomodoroLongBreakMinutes", "PanelFocus", "PanelShelf", "PanelCalendar", "PanelControlCenter", "PanelClipboard",
        "PanelSystem", "ClipboardHistoryEnabled", "VoiceHarness", "VoiceClaudePermission", "VoiceModel",
        "VoiceCustomCommand", "VoiceWorkingDirectory", "VoiceTimeoutSeconds", "SearchRetainPrior", "SearchHistoryMode",
        "SearchSourcesVersion", "ClockHourFormat", "ClockShowSeconds", "ClockShowWeekNumber", "ClockFirstDayOfWeek",
        "ClockCustomDateFormat", "TemperatureUnit", "NetworkSpeedUnit", "ClockShowDate", "ClockSecondTimeZoneId",
        "WindSpeedUnit"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Özellik değişimi ayar dosyasına yazılır mı.</summary>
    public static bool IsSaved(string? propertyName) => propertyName is null || !NotSaved.Contains(propertyName);

    public static bool AppliesThemeTo(string? propertyName) => propertyName is not null && AppliesTheme.Contains(propertyName);

    public static bool RebuildsDock(string? propertyName) =>
        propertyName is not null && propertyName != "DockEnabled" &&
        (propertyName.StartsWith("Dock", StringComparison.Ordinal) || DockExtra.Contains(propertyName));

    public static bool RebuildsNotchFor(string? propertyName) => propertyName is not null && RebuildsNotch.Contains(propertyName);

    /// <summary>Dock'u yeniden kuran bütün adlar (testler için; "Dock" önekli olanlar dahil değil).</summary>
    public static IReadOnlySet<string> DockExtraNames => DockExtra;
}
