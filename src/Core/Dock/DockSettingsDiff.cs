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
        var monitors = before.Displays != after.Displays;

        var appBar = DockProximityMapping.Effective(before.Dock.Style, before.Dock.Proximity) == DockProximityMode.ReserveSpace
                     != (DockProximityMapping.Effective(after.Dock.Style, after.Dock.Proximity) == DockProximityMode.ReserveSpace)
                     || before.Dock.Style != after.Dock.Style
                     || before.Dock.HideWindowsTaskbar != after.Dock.HideWindowsTaskbar;

        var taskbar = before.Dock.HideWindowsTaskbar != after.Dock.HideWindowsTaskbar;
        var tray = before.Dock.ShowTray != after.Dock.ShowTray || taskbar;
        var power = before.Appearance.BatterySaverReduceMotion != after.Appearance.BatterySaverReduceMotion;

        // ViewModel / VisibilityController / EdgeTracker ayarı kurulurken okur: Dock bölümündeki herhangi bir alan
        // (Enabled ve HideWindowsTaskbar hariç: onları DockFeature ayrıca ele alır) ya da dock'un çizimini etkileyen
        // görünüm alanları değiştiyse yeniden kurulur (pencere ve tutamaç aynı kalır). DockItems (sıra, klasörler)
        // bilerek dışarıda: dock sürüklenince kendini yeniden kurmamalı.
        var rebuild = Rebuildable(before.Dock) != Rebuildable(after.Dock)
                      || before.Appearance.Material != after.Appearance.Material
                      || before.Appearance.MaterialReadabilityGuard != after.Appearance.MaterialReadabilityGuard
                      || before.Appearance.AnimationSpeed != after.Appearance.AnimationSpeed
                      || before.Appearance.RespectReduceMotion != after.Appearance.RespectReduceMotion
                      || power;

        return new DockSettingsDiff(rebuild, monitors, appBar, taskbar, tray, power);
    }

    // Enabled ve HideWindowsTaskbar yeniden kurmayı değil, uzlaştırmayı (görev çubuğu, AppBar) ilgilendirir.
    private static DockSettings Rebuildable(DockSettings dock) => dock with { Enabled = default, HideWindowsTaskbar = default };
}
