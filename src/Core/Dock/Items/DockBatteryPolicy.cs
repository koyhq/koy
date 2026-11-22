using System.Globalization;

namespace Koy.Core.Dock.Items;

/// <summary>Dock pil öğesinin kararları: ne zaman yer kaplar, yüzde metni, düşük pil eşiği, "Öğe ekle" menüsü.</summary>
public static class DockBatteryPolicy
{
    /// <summary>Bu yüzdeye kadar (dahil) ve şarjda değilken pil düşük sayılır.</summary>
    public const int LowThreshold = 20;

    /// <summary>Pil yoksa öğe şeritte hiç yer tutmaz (masaüstü bilgisayar).</summary>
    public static bool IsShown(bool hasBattery) => hasBattery;

    /// <summary>"Öğe ekle > Pil" yalnız pili olan makinede ve şeritte henüz pil öğesi yokken açıktır.</summary>
    public static bool CanAdd(bool hasBattery, bool alreadyPresent) => hasBattery && !alreadyPresent;

    public static int Clamp(int percent) => Math.Clamp(percent, 0, 100);

    public static bool IsLow(int percent, bool charging) => !charging && Clamp(percent) <= LowThreshold;

    /// <summary>"82%" biçimi (kültürden bağımsız, simge yanında kısa yazı).</summary>
    public static string FormatPercent(int percent) =>
        Clamp(percent).ToString(CultureInfo.InvariantCulture) + "%";
}
