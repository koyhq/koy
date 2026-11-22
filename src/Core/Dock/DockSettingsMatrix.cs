using Koy.Core.Settings;

namespace Koy.Core.Dock;

/// <summary>Bir dock ayarının hangi stilde geçerli olduğu.</summary>
public enum DockSettingScope
{
    /// <summary>Yalnız Koy'un yüzen kapsülünde anlamlı; tam genişlikte (Windows görev çubuğu gibi) görünmez.</summary>
    CapsuleOnly,

    /// <summary>Yalnız tam genişlikte (Windows 11 "Görev çubuğu davranışları/öğeleri") anlamlı; kapsülde görünmez.</summary>
    FullWidthOnly,

    /// <summary>İki stilde de geçerli.</summary>
    Both,

    /// <summary>Hiçbir stilde gösterilmez (alan uyumluluk için dosyada kalır). Ör. uzunluk: Windows çubuğu hep ekran genişliğinde, kapsülde etkisizdi.</summary>
    Never
}

/// <summary>
/// Her dock ayarı için hangi stilde geçerli olduğu (saf, testli; tek kaynak). Anahtar <see cref="DockSettings"/> özellik adıdır.
/// Ayarlar sayfası seçili stilde geçersiz satırları gizler (gri bırakmaz). Tam genişlikte
/// <see cref="SystemArea.TaskbarLayout.ApplyWindowsStyle"/> ile sabitlenen her ayar burada "yalnız kapsül"dür; test bu tutarlılığı denetler.
/// </summary>
public static class DockSettingsMatrix
{
    private static readonly Dictionary<string, DockSettingScope> Scopes = new(StringComparer.Ordinal)
    {
        // İki stilde de
        [nameof(DockSettings.Enabled)] = DockSettingScope.Both,
        [nameof(DockSettings.HideWindowsTaskbar)] = DockSettingScope.Both,
        [nameof(DockSettings.Edge)] = DockSettingScope.Both,
        [nameof(DockSettings.Style)] = DockSettingScope.Both,
        [nameof(DockSettings.Visibility)] = DockSettingScope.Both,
        [nameof(DockSettings.HideInFullscreen)] = DockSettingScope.Both,
        [nameof(DockSettings.ShowBadges)] = DockSettingScope.Both,
        [nameof(DockSettings.ThumbnailPreview)] = DockSettingScope.Both,
        [nameof(DockSettings.ThumbnailDelayMs)] = DockSettingScope.Both,
        [nameof(DockSettings.MiddleClickAction)] = DockSettingScope.Both,
        [nameof(DockSettings.WindowMonitorScope)] = DockSettingScope.Both,
        [nameof(DockSettings.NewTrayIconVisibility)] = DockSettingScope.Both,
        [nameof(DockSettings.TrayAppVisibility)] = DockSettingScope.Both,

        // Yalnız tam genişlik: Windows 11 görev çubuğu ayarları
        [nameof(DockSettings.IconAlignment)] = DockSettingScope.FullWidthOnly,
        [nameof(DockSettings.TaskbarSearch)] = DockSettingScope.FullWidthOnly,
        [nameof(DockSettings.TaskbarShowTaskView)] = DockSettingScope.FullWidthOnly,
        [nameof(DockSettings.TaskbarShowWidgets)] = DockSettingScope.FullWidthOnly,
        [nameof(DockSettings.TaskbarShowFlashing)] = DockSettingScope.FullWidthOnly,
        [nameof(DockSettings.TaskbarClockSeconds)] = DockSettingScope.FullWidthOnly,
        [nameof(DockSettings.TaskbarShowDesktopCorner)] = DockSettingScope.FullWidthOnly,

        // Hiçbir stilde: dosyada kalır, arayüzde yok
        [nameof(DockSettings.LengthPercent)] = DockSettingScope.Never,

        // Yalnız kapsül: Koy'a özgü ya da Windows'ta olmayan
        [nameof(DockSettings.EdgeOffsetDip)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.InnerPaddingDip)] = DockSettingScope.CapsuleOnly,     // tam genişlikte etkisizdi
        [nameof(DockSettings.IconSpacingDip)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.IconSize)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.HoverMagnify)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.Proximity)] = DockSettingScope.CapsuleOnly,           // tam genişlikte "otomatik gizle" anahtarı gösterilir
        [nameof(DockSettings.EdgeReveal)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.RevealTrigger)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.RevealDelayMs)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.HiddenEdgeLine)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.DodgeDelayMs)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.ShowStartButton)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.StartIcon)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.StartIconPath)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.ShowFolders)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.ShowRecents)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.ShowTray)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.SmallTray)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.IndicatorStyle)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.IndicatorColor)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.IndicatorCustomColor)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.IndicatorOnePerWindow)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.BadgeColor)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.BadgeCustomColor)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.BadgeCorner)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.BadgeStyle)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.GroupingMode)] = DockSettingScope.CapsuleOnly,        // tam genişlikte hep "Her zaman"
        [nameof(DockSettings.LeftClickAction)] = DockSettingScope.CapsuleOnly,
        [nameof(DockSettings.LabelMode)] = DockSettingScope.CapsuleOnly,
    };

    /// <summary>Bilinen bütün ayar adları.</summary>
    public static IReadOnlyCollection<string> Keys => Scopes.Keys;

    /// <summary>Ayarın kapsamı; bilinmeyen ad "iki stilde de" sayılır (yeni ayar sessizce gizlenmesin; test eksik adı yakalar).</summary>
    public static DockSettingScope ScopeOf(string setting) =>
        Scopes.TryGetValue(setting, out var scope) ? scope : DockSettingScope.Both;

    /// <summary>Ayar bu stilde geçerli mi (ayar sayfası yalnız geçerli satırları gösterir).</summary>
    public static bool IsApplicable(string setting, DockStyleMode style) => ScopeOf(setting) switch
    {
        DockSettingScope.Both => true,
        DockSettingScope.FullWidthOnly => style == DockStyleMode.FullWidth,
        DockSettingScope.Never => false,
        _ => style != DockStyleMode.FullWidth
    };
}
