namespace Koy.Core.Settings;

/// <summary>
/// v3 (düz) ayar anahtarlarının v4'teki yeri: tek tablo. Yeni bir v3 alanı eklenmediği sürece bu tablo değişmez
/// (v3 artık donmuş şemadır); tutarlılığı testle korunur: v3 altın dosyasındaki her anahtar burada ya da
/// <see cref="Dropped"/> içinde olmak zorundadır.
/// </summary>
public static class SettingsKeyMap
{
    /// <summary>Düz anahtar → (bölüm, bölümdeki yeni ad).</summary>
    public static IReadOnlyDictionary<string, (string Section, string Name)> V3ToV4 { get; } = Build();

    /// <summary>v4'te karşılığı olmayan, bilerek atılan v3 anahtarları (MonitorTarget v2 kalıntısıydı; v2→v3 adımı tüketir).</summary>
    public static IReadOnlySet<string> Dropped { get; } = new HashSet<string> { "MonitorTarget" };

    /// <summary>Bölüme taşınmayan, kökte kalan anahtarlar.</summary>
    public static IReadOnlySet<string> TopLevel { get; } = new HashSet<string> { "Version" };

    private static Dictionary<string, (string Section, string Name)> Build()
    {
        var map = new Dictionary<string, (string, string)>();

        void Add(string section, params string[] keys)
        {
            // "DockShowTray" gibi düz anahtar → bölümdeki yeni ad ("ShowTray"); eşlemesi açık yazılmış anahtarlar "eski=yeni".
            foreach (var key in keys)
            {
                var parts = key.Split('=');
                map[parts[0]] = (section, parts.Length == 2 ? parts[1] : parts[0]);
            }
        }

        Add("General", "Language", "FirstRunCompleted", "LastSeenVersion", "AutoCheckUpdates", "UpdateChannel",
            "TemperatureUnit", "WindSpeedUnit", "NetworkSpeedUnit");
        Add("Appearance", "Theme", "AccentColor", "UseWindowsAccent", "CornerRadius", "AnimationSpeed",
            "RespectReduceMotion", "BatterySaverReduceMotion", "Material", "MaterialReadabilityGuard");
        Add("Dock", "DockEnabled=Enabled", "HideWindowsTaskbar", "DockVisibility=Visibility", "DockStyle=Style",
            "DockIconAlignment=IconAlignment", "DockProximity=Proximity", "DockHideInFullscreen=HideInFullscreen",
            "DockEdgeReveal=EdgeReveal", "DockHoverMagnify=HoverMagnify", "DockShowStartButton=ShowStartButton",
            "DockStartIcon=StartIcon", "DockStartIconPath=StartIconPath", "DockShowFolders=ShowFolders",
            "DockShowRecents=ShowRecents", "DockShowTray=ShowTray", "DockSmallTray=SmallTray", "DockShowBadges=ShowBadges",
            "DockThumbnailPreview=ThumbnailPreview", "DockThumbnailDelayMs=ThumbnailDelayMs", "DockDodgeDelayMs=DodgeDelayMs",
            "DockIconSize=IconSize", "DockIndicatorStyle=IndicatorStyle", "MiddleClickAction");
        Add("DockItems", "DockPinnedOrder=PinnedOrder", "DockFolders=Folders");
        Add("Notch", "NotchEnabled=Enabled", "NotchVisibility=Visibility", "NotchStyle=Style", "NotchOpenBehavior=OpenBehavior",
            "IdleDisplayMode=IdleDisplay", "WheelAction", "NotchHideInFullscreen=HideInFullscreen",
            "NotchFollowsMaterial=FollowsMaterial", "ShowVolumeOsd", "ShowBrightnessOsd", "ShowPrivacyIndicator",
            "NotchShowNotifications=ShowNotifications", "NotchNotificationsHideBody=NotificationsHideBody",
            "EnabledPanels", "ControlCenterLayout");
        Add("Music", "MusicShowWaveform=ShowWaveform", "MusicCardStyle=CardStyle", "MusicLyricsEnabled=LyricsEnabled");
        Add("Displays", "Monitors", "NewScreenDefault");
        Add("Shortcuts", "ShortcutToggleNotch=ToggleNotch", "ShortcutOpenShelf=OpenShelf", "ShortcutToggleDock=ToggleDock",
            "ShortcutOpenMenu=OpenMenu", "ShortcutPlayPause=PlayPause", "ShortcutOpenSearch=OpenSearch",
            "ShortcutOpenSearchClipboard=OpenSearchClipboard", "ShortcutVoiceCommand=VoiceCommand");
        Add("HotCorners", "HotCornerTopLeft=TopLeft", "HotCornerTopRight=TopRight", "HotCornerBottomLeft=BottomLeft",
            "HotCornerBottomRight=BottomRight", "HotCornerDwellMs=DwellMs", "HotCornerDisableInFullscreen=DisableInFullscreen");
        Add("Voice", "VoiceHarness=Harness", "VoiceClaudePermission=ClaudePermission", "VoiceModel=Model",
            "VoiceCustomCommand=CustomCommand", "VoiceWorkingDirectory=WorkingDirectory", "VoiceTimeoutSeconds=TimeoutSeconds");
        Add("Search", "SearchRetainPrior=RetainPrior", "SearchHistoryMode=HistoryMode", "SearchDisabledSources=DisabledSources",
            "SearchPinned=Pinned");
        Add("Focus", "PomodoroWorkMinutes", "PomodoroShortBreakMinutes", "PomodoroLongBreakMinutes");
        Add("Weather", "WeatherRefreshInterval=RefreshInterval", "WeatherLatitude=Latitude", "WeatherLongitude=Longitude",
            "WeatherCity=City", "WeatherLocationSource=LocationSource", "WeatherUseLocation=UseLocation",
            "WeatherLocationUpdatedUnix=LocationUpdatedUnix");
        Add("Clipboard", "ClipboardHistoryEnabled=HistoryEnabled");
        Add("QuickLaunch", "QuickLaunchItems=Items");

        return map;
    }
}
