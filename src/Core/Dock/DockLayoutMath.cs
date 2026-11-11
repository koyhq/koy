namespace Koy.Core.Dock;

/// <summary>Ekranda DIP cinsinden dikdörtgen (sol, üst, genişlik, yükseklik).</summary>
public readonly record struct DockRectDip(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    /// <summary>Her kenardan <paramref name="amount"/> kadar içeri alınmış dikdörtgen; boyut 0'ın altına inmez.</summary>
    public DockRectDip Inset(double amount) =>
        new(Left + amount, Top + amount, Math.Max(0.0, Width - 2 * amount), Math.Max(0.0, Height - 2 * amount));
}

/// <summary>Dock'un dış dikdörtgeni ve iç (dolgu düşülmüş) içerik alanı.</summary>
public sealed record DockLayoutResult(DockRectDip Outer, DockRectDip Content);

/// <summary>
/// Kenar, uzunluk, kenar ofseti ve iç dolgudan dock dikdörtgenini hesaplayan saf mantık (Core).
/// Dikdörtgen çalışma alanına göre kurulur. Varsayılan ayarlarla (alt kenar, %100, ofset 0, dolgu 0)
/// sonuç <see cref="DockPositionResolver"/>'ın tam genişlik dock'uyla aynıdır; bu yüzden geometri tek yerde kalır.
/// </summary>
public static class DockLayoutMath
{
    /// <summary>
    /// <paramref name="thicknessDip"/> kenara dik ölçüdür (yatayda yükseklik, dikeyde genişlik). Uzunluk çalışma
    /// alanının kenara paralel ölçüsünün yüzdesidir ve ortalanır. Girdiler kenetlenir (<see cref="DockLayoutLimits"/>).
    /// </summary>
    public static DockLayoutResult Compute(
        DockEdge edge,
        int lengthPercent,
        double edgeOffsetDip,
        double innerPaddingDip,
        double thicknessDip,
        DockRectDip workArea)
    {
        var lengthFraction = DockLayoutLimits.ClampLengthPercent(lengthPercent) / 100.0;
        var offset = DockLayoutLimits.ClampEdgeOffsetDip(edgeOffsetDip);
        var padding = DockLayoutLimits.ClampInnerPaddingDip(innerPaddingDip);
        var thickness = Math.Max(0.0, thicknessDip);

        DockRectDip outer;
        if (edge is DockEdge.Bottom or DockEdge.Top)
        {
            var width = workArea.Width * lengthFraction;
            var left = workArea.Left + (workArea.Width - width) / 2.0;
            var top = edge == DockEdge.Bottom
                ? workArea.Bottom - offset - thickness
                : workArea.Top + offset;
            outer = new DockRectDip(left, top, width, thickness);
        }
        else
        {
            var height = workArea.Height * lengthFraction;
            var top = workArea.Top + (workArea.Height - height) / 2.0;
            var left = edge == DockEdge.Left
                ? workArea.Left + offset
                : workArea.Right - offset - thickness;
            outer = new DockRectDip(left, top, thickness, height);
        }

        return new DockLayoutResult(outer, outer.Inset(padding));
    }
}
