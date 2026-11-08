using Koy.Core.Settings;

namespace Koy.Core.Dock;

/// <summary>
/// Ayar değişince çalışan dock'ta NELERİN yeniden uygulanacağı. Dock'u durdurup başlatmak Windows görev çubuğunu geri
/// getirir, AppBar kaydını siler ve çalışma alanını oynatır (büyütülmüş pencereler sıçrar); bu yüzden ayar değişimi hiçbir
/// zaman yeniden başlatma gerektirmez: pencereler ve AppBar yerinde kalır, yalnız değişenler yeniden kurulur.
/// </summary>
public readonly record struct DockSettingsDiff(
    bool RebuildViewModels,
    bool ReconcileMonitors,
    bool ReconcileAppBar,
    bool ReconcileTaskbar,
    bool ReconcileTray,
    bool ReconcilePower)
{
    /// <summary>Dock'u ilgilendiren hiçbir şey değişmedi (örn. yalnız çentik ayarı).</summary>
    public bool IsEmpty => this == default;

    public static DockSettingsDiff Compare(AppSettings before, AppSettings after)
    {
        var monitors = before.MonitorTarget != after.MonitorTarget
                       || before.NewScreenDefault != after.NewScreenDefault
                       || !new AppSettings { Monitors = before.Monitors }.Equals(new AppSettings { Monitors = after.Monitors });

        var appBar = DockProximityMapping.Effective(before.DockStyle, before.DockProximity) == DockProximityMode.ReserveSpace
                     != (DockProximityMapping.Effective(after.DockStyle, after.DockProximity) == DockProximityMode.ReserveSpace)
                     || before.DockStyle != after.DockStyle
                     || before.HideWindowsTaskbar != after.HideWindowsTaskbar;

        var taskbar = before.HideWindowsTaskbar != after.HideWindowsTaskbar;
        var tray = before.DockShowTray != after.DockShowTray || taskbar;
        var power = before.BatterySaverReduceMotion != after.BatterySaverReduceMotion;

        // ViewModel / VisibilityController / EdgeTracker ayarı kurulurken okur: bunlardan biri değiştiyse yeniden kurulur
        // (pencere ve tutamaç aynı kalır).
        var rebuild = before.DockStyle != after.DockStyle
                      || before.DockIconAlignment != after.DockIconAlignment
                      || before.DockProximity != after.DockProximity
                      || before.DockVisibility != after.DockVisibility
                      || before.DockHoverMagnify != after.DockHoverMagnify
                      || before.DockHideInFullscreen != after.DockHideInFullscreen
                      || before.DockEdgeReveal != after.DockEdgeReveal
                      || before.DockShowStartButton != after.DockShowStartButton
                      || before.DockShowFolders != after.DockShowFolders
                      || before.DockShowRecents != after.DockShowRecents
                      || before.DockShowTray != after.DockShowTray
                      || before.DockSmallTray != after.DockSmallTray
                      || before.DockShowBadges != after.DockShowBadges
                      || before.DockThumbnailPreview != after.DockThumbnailPreview
                      || before.DockThumbnailDelayMs != after.DockThumbnailDelayMs
                      || before.DockDodgeDelayMs != after.DockDodgeDelayMs
                      || before.DockIconSize != after.DockIconSize
                      || before.DockIndicatorStyle != after.DockIndicatorStyle
                      || before.MiddleClickAction != after.MiddleClickAction
                      || before.Material != after.Material
                      || before.AnimationSpeed != after.AnimationSpeed
                      || before.RespectReduceMotion != after.RespectReduceMotion
                      || power;

        return new DockSettingsDiff(rebuild, monitors, appBar, taskbar, tray, power);
    }
}
