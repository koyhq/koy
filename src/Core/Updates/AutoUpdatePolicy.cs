namespace Koy.Core.Updates;

/// <summary>
/// İsteğe bağlı otomatik güncelleme kurulumunun ne zaman arka planda indirme başlatacağına karar verir.
/// Ayar varsayılan kapalıdır; pil tasarrufunda indirme ertelenir, zaten hazır olan sürüm tekrar indirilmez.
/// </summary>
public static class AutoUpdatePolicy
{
    public static bool ShouldAutoStage(
        bool autoCheckUpdates,
        bool autoInstallUpdates,
        bool hasAvailableUpdate,
        bool alreadyReady,
        bool isEnergySaverOn)
        => autoCheckUpdates && autoInstallUpdates && hasAvailableUpdate && !alreadyReady && !isEnergySaverOn;
}
