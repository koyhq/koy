namespace Koy.Core.Appearance;

/// <summary>
/// Malzeme ayarı (<see cref="MaterialLevel"/>): Katı (opak), Buzlu (%86) ya da Cam (%72). Bulanıklık yoktur: katmanlı pencerede
/// (AllowsTransparency) DWM akrilik uygulanmadığından saydamlık saf alfadır. Onaylı kaynak: docs/design/source/CamKoy.dc.html
/// ve CamCentik.dc.html. Eski dosyalardaki Türkçe adlar ("Katı", "Yarı saydam"…) ayar göçünde enum adına çevrilir.
/// </summary>
public static class MaterialSetting
{
    public const double FrostedOpacity = 0.86;
    public const double GlassOpacity = 0.72;

    /// <summary>Okunabilirlik korumasının denediği sıra: en saydamdan opağa.</summary>
    public static IReadOnlyList<MaterialLevel> Levels { get; } = [MaterialLevel.Glass, MaterialLevel.Frosted, MaterialLevel.Solid];

    /// <summary>Katı dışındaki her seviye (yüzeyler saydam).</summary>
    public static bool IsTranslucent(MaterialLevel value) => value != MaterialLevel.Solid;

    /// <summary>Yüzey opaklığı: Katı 1, Buzlu 0,86, Cam 0,72.</summary>
    public static double SurfaceOpacity(MaterialLevel value) => value switch
    {
        MaterialLevel.Glass => GlassOpacity,
        MaterialLevel.Frosted => FrostedOpacity,
        _ => 1.0
    };

    /// <summary>Opaklığın 0–255 alfa karşılığı.</summary>
    public static byte SurfaceAlpha(MaterialLevel value) => (byte)Math.Round(255 * SurfaceOpacity(value));

    // Bulanıklıksız yüzeyler (çentik, dock): katmanlı pencere oldukları için DWM bulanıklığı alamazlar. Demodaki %86 / %72
    // gerçek masaüstünde arkadaki metni okunur bırakıyordu (proje sahibi elle sınaması, 2026-11-11: "fazla saydam");
    // bu yüzden aynı seviyeler burada daha opaktır. Koy penceresi gerçek akrilik aldığı için SurfaceOpacity'de kalır.
    public const double LayeredFrostedOpacity = 0.94;
    public const double LayeredGlassOpacity = 0.88;

    /// <summary>Bulanıklıksız yüzeyin (çentik, dock) opaklığı: Katı 1, Buzlu 0,94, Cam 0,88.</summary>
    public static double LayeredOpacity(MaterialLevel value) => value switch
    {
        MaterialLevel.Glass => LayeredGlassOpacity,
        MaterialLevel.Frosted => LayeredFrostedOpacity,
        _ => 1.0
    };

    /// <summary>Bulanıklıksız yüzey opaklığının 0–255 alfa karşılığı.</summary>
    public static byte LayeredAlpha(MaterialLevel value) => (byte)Math.Round(255 * LayeredOpacity(value));

    /// <summary>
    /// Sistem arka planı (DWM akrilik/mika) desteklenmiyorsa (Windows 10) pencere yüzeyi opak <see cref="MaterialLevel.Solid"/> olur.
    /// </summary>
    public static MaterialLevel ResolveWindowMaterial(MaterialLevel requested, bool supportsBackdrop) =>
        supportsBackdrop ? requested : MaterialLevel.Solid;
}
