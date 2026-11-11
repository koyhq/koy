namespace Koy.Core.Dock;

/// <summary>Ekranda DIP cinsinden dikdörtgen (sol, üst, genişlik, yükseklik).</summary>
public readonly record struct DockRectDip(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    /// <summary>
    /// Her kenardan <paramref name="amount"/> kadar içeri alınmış dikdörtgen. Boyut 0'ın altına inmez; dolgu bir
    /// eksenin yarısını aşarsa o eksen orta çizgiye çöker (iç dikdörtgen dışının dışına çıkmaz).
    /// </summary>
    public DockRectDip Inset(double amount)
    {
        var dx = Math.Min(amount, Math.Max(0.0, Width) / 2.0);
        var dy = Math.Min(amount, Math.Max(0.0, Height) / 2.0);
        return new(Left + dx, Top + dy, Math.Max(0.0, Width - 2 * dx), Math.Max(0.0, Height - 2 * dy));
    }
}

/// <summary>Dock'un dış dikdörtgeni ve iç (dolgu düşülmüş) içerik alanı.</summary>
public sealed record DockLayoutResult(DockRectDip Outer, DockRectDip Content);

/// <summary>
/// Kenar, uzunluk, kenar ofseti ve iç dolgudan dock dikdörtgenini hesaplayan saf mantık (Core).
/// Dikdörtgen verilen alana göre kurulur. Varsayılan ayarlarla (alt kenar, %100, ofset 0, dolgu 0) sonuç
/// <see cref="DockPositionResolver"/>'ın tam genişlik dock'uyla aynıdır, AMA yalnız alan MONİTÖR SINIRLARI verilirse:
/// resolver bugün monitör sınırlarından hesaplar. Çalışma alanı (WorkArea) dock'un kendi AppBar payı kadar kısalır
/// (Yer ayır), onu verirsek dock kendi ayırdığı şeridin üstüne oturur. Yüzen kapsül stilindeki 10 DIP alt boşluk
/// (<see cref="DockPositionResolver.FloatingBottomMarginDip"/>) varsayılan ofsette (0) YOKTUR; bağlayan kod ekler.
/// Geçersiz girdiler (NaN, sonsuz, negatif boyut, tanımsız kenar) sessizce güvenli değere iner.
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
        // El ile düzenlenmiş JSON'dan gelen tanımsız sayı (örn. 7) Right dalına düşmesin; bugünkü davranış alt kenardır.
        if (!Enum.IsDefined(edge)) edge = DockEdge.Bottom;
        workArea = Normalize(workArea);
        var lengthFraction = DockLayoutLimits.ClampLengthPercent(lengthPercent) / 100.0;
        var offset = DockLayoutLimits.ClampEdgeOffsetDip(edgeOffsetDip);
        var padding = DockLayoutLimits.ClampInnerPaddingDip(innerPaddingDip);
        var thickness = double.IsNaN(thicknessDip) ? 0.0 : Math.Max(0.0, thicknessDip);
        // Kalınlık ve ofset çalışma alanının dışına taşamaz (küçük ekranda negatif konum olmasın).
        var across = edge is DockEdge.Bottom or DockEdge.Top ? workArea.Height : workArea.Width;
        thickness = Math.Min(thickness, Math.Max(0.0, across));
        offset = Math.Min(offset, Math.Max(0.0, across - thickness));

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

    // Sıfır / negatif boyutlu ya da sonlu olmayan alan negatif genişlikli dock üretmesin.
    private static DockRectDip Normalize(DockRectDip r)
    {
        static double Finite(double v) => double.IsFinite(v) ? v : 0.0;
        return new DockRectDip(Finite(r.Left), Finite(r.Top), Math.Max(0.0, Finite(r.Width)), Math.Max(0.0, Finite(r.Height)));
    }
}
