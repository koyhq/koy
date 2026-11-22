namespace Koy.Core.Dock.Items;

/// <summary>
/// Dock pil öğesinin yer kararları: ne zaman yer kaplar, "Öğe ekle" menüsü.
/// Durum, simge, renk ve yüzde metni <see cref="Power.BatteryDisplayPolicy"/>'dedir.
/// </summary>
public static class DockBatteryPolicy
{
    /// <summary>Pil yoksa öğe şeritte hiç yer tutmaz (masaüstü bilgisayar).</summary>
    public static bool IsShown(bool hasBattery) => hasBattery;

    /// <summary>"Öğe ekle > Pil" yalnız pili olan makinede ve şeritte henüz pil öğesi yokken açıktır.</summary>
    public static bool CanAdd(bool hasBattery, bool alreadyPresent) => hasBattery && !alreadyPresent;
}
