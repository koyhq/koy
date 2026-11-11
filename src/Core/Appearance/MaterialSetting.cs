namespace Koy.Core.Appearance;

/// <summary>
/// Malzeme ayarı: Katı (opak), Buzlu (%86) ya da Cam (%72). Bulanıklık yoktur: katmanlı pencerede (AllowsTransparency)
/// DWM akrilik uygulanmadığından saydamlık saf alfadır. Onaylı kaynak: docs/design/source/CamKoy.dc.html ve CamCentik.dc.html.
/// Eski "Yarı saydam" değeri (iki seviyeli dönem) Buzlu'ya düşer; dosyadaki değer ilk kaydedişte yeni adla yazılır.
/// </summary>
public static class MaterialSetting
{
    public const string Solid = "Katı";
    public const string Frosted = "Buzlu";
    public const string Glass = "Cam";

    public const double FrostedOpacity = 0.86;
    public const double GlassOpacity = 0.72;

    /// <summary>Okunabilirlik korumasının denediği sıra: en saydamdan opağa.</summary>
    public static IReadOnlyList<string> Levels { get; } = [Glass, Frosted, Solid];

    public static string Normalize(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return v switch
        {
            "cam" or "glass" => Glass,
            "buzlu" or "frosted" or "yarı saydam" or "yari saydam" or "translucent" or "acrylic" or "mica" => Frosted,
            _ => Solid
        };
    }

    /// <summary>Katı dışındaki her seviye (yüzeyler saydam).</summary>
    public static bool IsTranslucent(string? value) => Normalize(value) != Solid;

    /// <summary>Yüzey opaklığı: Katı 1, Buzlu 0,86, Cam 0,72.</summary>
    public static double SurfaceOpacity(string? value) => Normalize(value) switch
    {
        Glass => GlassOpacity,
        Frosted => FrostedOpacity,
        _ => 1.0
    };

    /// <summary>Opaklığın 0–255 alfa karşılığı.</summary>
    public static byte SurfaceAlpha(string? value) => (byte)Math.Round(255 * SurfaceOpacity(value));

    // Bulanıklıksız yüzeyler (çentik, dock): katmanlı pencere oldukları için DWM bulanıklığı alamazlar. Demodaki %86 / %72
    // gerçek masaüstünde arkadaki metni okunur bırakıyordu (proje sahibi elle sınaması, 2026-11-11: "fazla saydam");
    // bu yüzden aynı seviyeler burada daha opaktır. Koy penceresi gerçek akrilik aldığı için SurfaceOpacity'de kalır.
    public const double LayeredFrostedOpacity = 0.94;
    public const double LayeredGlassOpacity = 0.88;

    /// <summary>Bulanıklıksız yüzeyin (çentik, dock) opaklığı: Katı 1, Buzlu 0,94, Cam 0,88.</summary>
    public static double LayeredOpacity(string? value) => Normalize(value) switch
    {
        Glass => LayeredGlassOpacity,
        Frosted => LayeredFrostedOpacity,
        _ => 1.0
    };

    /// <summary>Bulanıklıksız yüzey opaklığının 0–255 alfa karşılığı.</summary>
    public static byte LayeredAlpha(string? value) => (byte)Math.Round(255 * LayeredOpacity(value));
}
