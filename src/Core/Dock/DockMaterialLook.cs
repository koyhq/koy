using Koy.Core.Appearance;

namespace Koy.Core.Dock;

/// <summary>Birim kare içinde (0..1) bir nokta: gradyanın başı ve sonu.</summary>
public readonly record struct DockUnitPoint(double X, double Y);

/// <summary>
/// Dock yüzeyinin malzemeye göre görsel ipuçları. <paramref name="SurfaceAlpha"/> yüzeyin alfası (0–255);
/// <paramref name="SheenLighten"/> yüzeyin ekran merkezine bakan kenarda beyaza doğru ne kadar açıldığı (0..1, 0 = parlama yok);
/// <paramref name="HighlightAlpha"/> iç kenar çizgisinin beyaz alfası (0 = ek çizgi yok, eski kenarlık kalır).
/// Gradyan <paramref name="GradientStart"/> (iç kenar) → <paramref name="GradientEnd"/> (dış kenar) yönündedir.
/// Tam genişlikte iç kenar çizgisi uçlara doğru <paramref name="LineEndsFade"/> katına solar (1 = düz; ortada parlak, ucu sönük
/// "ışık vuran kenar"); çizginin ekseni <paramref name="RimStart"/> → <paramref name="RimEnd"/>. Kapsülde aynı iki nokta köşegen
/// kenarlık gradyanıdır (iç köşede parlak, ortada <paramref name="RimMidAlpha"/> katına sönük, karşı köşede yeniden parlar).
/// </summary>
public readonly record struct DockMaterialLook(
    byte SurfaceAlpha,
    double SheenLighten,
    byte HighlightAlpha,
    DockUnitPoint GradientStart,
    DockUnitPoint GradientEnd,
    double LineEndsFade = 1,
    DockUnitPoint RimStart = default,
    DockUnitPoint RimEnd = default,
    double RimMidAlpha = 1)
{
    /// <summary>Ek ipucu var mı: yoksa (Katı) yüzey bugünkü düz fırça ve kenarlıkla çizilir.</summary>
    public bool HasCues => SheenLighten > 0 || HighlightAlpha > 0;
}

/// <summary>
/// Katı / Buzlu / Cam için dock yüzey görünümü kararı (saf). Bulanıklık olmadığından (katmanlı pencere) cam hissi
/// görsel ipuçlarından gelir: ekran merkezine bakan kenara doğru hafif bir parlama ve o kenarda ince bir iç çizgi.
/// Katı bugünkü görünümü korur (ipucu yok). Opaklık tablosu <see cref="MaterialSetting.LayeredAlpha"/>'dır.
/// </summary>
public static class DockMaterialPolicy
{
    // Yüzen kapsül Katı'da da %90'dan opak olmaz (bugünkü davranış).
    private const byte CapsuleMaxAlpha = 0xE6;

    public static DockMaterialLook For(MaterialLevel level, DockEdge edge, bool fullWidth)
    {
        var alpha = MaterialSetting.LayeredAlpha(level);
        if (!fullWidth)
            alpha = Math.Min(CapsuleMaxAlpha, alpha);

        var (start, end) = Direction(edge);

        // Tam genişlikte çizgi, kenarlığın kendisidir (zaten ekran merkezine bakan kenarda); Katı'daki #26 çizgiden
        // aşağı düşmesin diye Buzlu/Cam'da daha parlaktır. Kapsülde çizgi kenarlığın ışıklı tarafıdır.
        var (rimStart, rimEnd) = fullWidth ? LineAxis(edge) : Diagonal(edge);

        return level switch
        {
            // Cam: belirgin parlama, ortada parlak ve uçlara sönen kenar çizgisi (tam genişlik), köşegen özel parıltılı kenarlık (kapsül).
            MaterialLevel.Glass => new DockMaterialLook(alpha, fullWidth ? 0.13 : 0.10, fullWidth ? (byte)0x4A : (byte)0x5C, start, end,
                fullWidth ? 0.25 : 1, rimStart, rimEnd, RimMidAlpha: 0.38),
            // Buzlu: aynı dil, daha yumuşak.
            MaterialLevel.Frosted => new DockMaterialLook(alpha, fullWidth ? 0.045 : 0.035, fullWidth ? (byte)0x30 : (byte)0x38, start, end,
                fullWidth ? 0.55 : 1, rimStart, rimEnd, RimMidAlpha: 0.55),
            _ => new DockMaterialLook(alpha, 0, 0, start, end)
        };
    }

    /// <summary>Tam genişlik çizgisinin ekseni: yatay kenarda soldan sağa, dikey kenarda yukarıdan aşağı.</summary>
    public static (DockUnitPoint Start, DockUnitPoint End) LineAxis(DockEdge edge) =>
        edge is DockEdge.Left or DockEdge.Right
            ? (new DockUnitPoint(0.5, 0), new DockUnitPoint(0.5, 1))
            : (new DockUnitPoint(0, 0.5), new DockUnitPoint(1, 0.5));

    /// <summary>Kapsül kenarlığının köşegeni: ekran merkezine bakan köşeden karşıya (alt dock'ta sol üst → sağ alt).</summary>
    public static (DockUnitPoint Start, DockUnitPoint End) Diagonal(DockEdge edge) => edge switch
    {
        DockEdge.Top => (new DockUnitPoint(0, 1), new DockUnitPoint(1, 0)),
        DockEdge.Right => (new DockUnitPoint(0, 0), new DockUnitPoint(1, 1)),
        DockEdge.Left => (new DockUnitPoint(1, 0), new DockUnitPoint(0, 1)),
        _ => (new DockUnitPoint(0, 0), new DockUnitPoint(1, 1))
    };

    /// <summary>İç kenar (ekran merkezine bakan) → dış kenar yönü: alt dock'ta üstten alta, sol dock'ta sağdan sola.</summary>
    public static (DockUnitPoint Start, DockUnitPoint End) Direction(DockEdge edge) => edge switch
    {
        DockEdge.Top => (new DockUnitPoint(0.5, 1), new DockUnitPoint(0.5, 0)),
        DockEdge.Left => (new DockUnitPoint(1, 0.5), new DockUnitPoint(0, 0.5)),
        DockEdge.Right => (new DockUnitPoint(0, 0.5), new DockUnitPoint(1, 0.5)),
        _ => (new DockUnitPoint(0.5, 0), new DockUnitPoint(0.5, 1))
    };
}
