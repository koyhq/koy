namespace Koy.Core.Dock;

/// <summary>
/// Pencere olayı fırtınasında (tarayıcı, Gezgin, menü açılışı) dock'un yeniden denetimi. Her ham olayda bütün
/// pencereleri sayıp z-sırasını tazelemek (çok monitörde EnumWindows) UI iş parçacığını yorar; olaylar bu gecikmeyle
/// tek denetime birleşir (<see cref="RefreshDebouncer"/>). Görev çubuğunu yeniden gizleme güvenlik işidir (AGENTS #7)
/// ve gecikmeden çalışır.
/// </summary>
public static class DockWindowEventPolicy
{
    /// <summary>İlk olaydan sonra denetimin bekletileceği süre (gözle algılanmaz).</summary>
    public static readonly TimeSpan RecheckDelay = TimeSpan.FromMilliseconds(75);
}
