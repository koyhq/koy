using System.Windows;
using System.Windows.Media;
using Koy.Core.Dock;

namespace Koy.App.Features.Dock;

/// <summary>
/// <see cref="DockMaterialLook"/> kararını dondurulmuş fırçalara çevirir (karar Core'da, burada yalnız çizim).
/// Katı'da ipucu yoktur: yüzey bugünkü düz fırça, kenarlık bugünkü kaynak fırçadır (piksel aynı).
/// </summary>
internal static class DockSurfaceBrushes
{
    /// <summary>Yüzey: ipucu yoksa düz renk, varsa iç kenardan dış kenara doğru taban renge inen hafif parlama.</summary>
    public static Brush Surface(DockMaterialLook look, Color baseColor)
    {
        var plain = Color.FromArgb(look.SurfaceAlpha, baseColor.R, baseColor.G, baseColor.B);
        Brush brush;
        if (look.SheenLighten <= 0)
        {
            brush = new SolidColorBrush(plain);
        }
        else
        {
            // Beyaza doğru karıştırma: alfa değişmez, yalnız renk açılır (altındaki masaüstü saydamlığı korunur).
            var lit = Lighten(baseColor, look.SheenLighten, look.SurfaceAlpha);
            var stops = new GradientStopCollection { new GradientStop(lit, 0), new GradientStop(plain, look.SheenReach) };
            if (look.SheenReach < 1)
                stops.Add(new GradientStop(plain, 1));
            brush = new LinearGradientBrush(stops, ToPoint(look.GradientStart), ToPoint(look.GradientEnd));
        }

        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Kenarlık: ipucu yoksa verilen kaynak fırça aynen. Tam genişlikte kalınlık zaten iç kenardadır, çizgi düz
    /// <paramref name="highlight"/> rengidir. Kapsülde (<paramref name="gradient"/>) çizgi iç kenarda parlak, dış kenarda
    /// kaynak kenarlığa iner; köşe yarıçapını Border kendisi izler.
    /// </summary>
    public static Brush Border(DockMaterialLook look, Brush legacy, Color highlight, bool gradient)
    {
        if (look.HighlightAlpha == 0)
            return legacy;

        var line = Color.FromArgb(look.HighlightAlpha, highlight.R, highlight.G, highlight.B);
        Brush brush;
        if (!gradient)
        {
            // Tam genişlik: çizgi ortada tam parlak, uçlara doğru söner (ışık vuran kenar).
            var ends = Scale(line, look.LineEndsFade);
            brush = look.LineEndsFade >= 1
                ? new SolidColorBrush(line)
                : new LinearGradientBrush(
                    new GradientStopCollection { new GradientStop(ends, 0), new GradientStop(line, 0.5), new GradientStop(ends, 1) },
                    ToPoint(look.RimStart), ToPoint(look.RimEnd));
        }
        else
        {
            // Kapsül: köşegen gradyan, iki karşı köşede parlak, ortada sönük (özel parıltı).
            var far = legacy is SolidColorBrush s ? s.Color : Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
            var mid = Scale(line, look.RimMidAlpha);
            if (mid.A < far.A) mid = far;
            brush = new LinearGradientBrush(
                new GradientStopCollection { new GradientStop(line, 0), new GradientStop(mid, 0.4), new GradientStop(mid, 0.6), new GradientStop(Scale(line, 0.7), 1) },
                ToPoint(look.RimStart), ToPoint(look.RimEnd));
        }

        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Cam karosu (etkin / üstüne gelinen simgenin zemini): üstten alta çok hafif beyaz dolgu. Katı'da (karo yok) verilen
    /// eski fırça aynen döner.
    /// </summary>
    public static Brush TileFill(DockMaterialLook look, Brush legacy, Color highlight, bool hover)
    {
        if (!look.HasTile)
            return legacy;

        var boost = hover ? look.TileHoverBoost : 0;
        var brush = new LinearGradientBrush(
            Color.FromArgb((byte)Math.Min(255, look.TileFillTopAlpha + boost), highlight.R, highlight.G, highlight.B),
            Color.FromArgb((byte)Math.Min(255, look.TileFillBottomAlpha + boost), highlight.R, highlight.G, highlight.B),
            new Point(0.5, 0), new Point(0.5, 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>Cam karosunun 135° köşegen 1 px kenarlığı: sol üst parlak, ortası sönük, sağ alt orta. Karo yoksa şeffaf.</summary>
    public static Brush TileBorder(DockMaterialLook look, Color highlight)
    {
        if (!look.HasTile)
            return Brushes.Transparent;

        var peak = Color.FromArgb(look.TileBorderAlpha, highlight.R, highlight.G, highlight.B);
        var brush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(peak, 0),
                new GradientStop(Scale(peak, look.TileBorderMidFactor), 0.45),
                new GradientStop(Scale(peak, look.TileBorderMidFactor * 0.8), 0.6),
                new GradientStop(Scale(peak, 0.55), 1)
            },
            new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }

    private static Color Scale(Color c, double alphaFactor) =>
        Color.FromArgb((byte)Math.Round(c.A * alphaFactor), c.R, c.G, c.B);

    private static Color Lighten(Color c, double t, byte alpha) => Color.FromArgb(
        alpha,
        (byte)Math.Round(c.R + (255 - c.R) * t),
        (byte)Math.Round(c.G + (255 - c.G) * t),
        (byte)Math.Round(c.B + (255 - c.B) * t));

    private static Point ToPoint(DockUnitPoint p) => new(p.X, p.Y);
}
