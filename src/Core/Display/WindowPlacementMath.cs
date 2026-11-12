using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Display;

/// <summary>
/// Çentik ve dock penceresinin hedef monitördeki fiziksel piksel konumu. WPF'in Left/Top (DIP) alanları pencerenin
/// o anki DPI'ıyla yorumlanır; farklı ölçekli ekranlar arasında yanlış yere düşer. Bu yüzden konum, hedef
/// monitörün kendi ölçeğiyle pikselde hesaplanır ve SetWindowPos ile uygulanır.
/// </summary>
public static class WindowPlacementMath
{
    /// <summary>Monitörün üst kenarında yatay ortalı pencere (çentik).</summary>
    public static WindowRect TopCenter(DisplayMonitor monitor, double widthDip, double heightDip) =>
        TopCenter(monitor, widthDip, heightDip, 0);

    /// <summary>
    /// Üst kenardan <paramref name="topOffsetDip"/> aşağıda yatay ortalı pencere (yüzen ada). Ofset hedef monitörün
    /// kendi dikey ölçeğiyle piksele çevrilir; pencere boyutu değişmez, yalnız Top kayar.
    /// </summary>
    public static WindowRect TopCenter(DisplayMonitor monitor, double widthDip, double heightDip, double topOffsetDip)
    {
        var width = ToPixels(widthDip, monitor.DpiScaleX);
        var height = ToPixels(heightDip, monitor.DpiScaleY);
        var left = monitor.Bounds.Left + (monitor.Bounds.Width - width) / 2;
        var top = monitor.Bounds.Top + ToPixels(Math.Max(0, topOffsetDip), monitor.DpiScaleY);
        return new WindowRect(left, top, left + width, top + height);
    }

    /// <summary>Monitörün alt kenarında yatay ortalı, alttan boşluklu pencere (yüzen kapsül dock).</summary>
    public static WindowRect BottomCenter(DisplayMonitor monitor, double widthDip, double heightDip, double bottomMarginDip)
    {
        var width = Math.Min(ToPixels(widthDip, monitor.DpiScaleX), monitor.Bounds.Width);
        var height = ToPixels(heightDip, monitor.DpiScaleY);
        var margin = ToPixels(bottomMarginDip, monitor.DpiScaleY);
        var left = monitor.Bounds.Left + (monitor.Bounds.Width - width) / 2;
        var bottom = monitor.Bounds.Bottom - margin;
        return new WindowRect(left, bottom - height, left + width, bottom);
    }

    /// <summary>Monitörün tam ortasında pencere ("Ekranları tanımla" numarası).</summary>
    public static WindowRect Center(DisplayMonitor monitor, double widthDip, double heightDip)
    {
        var width = ToPixels(widthDip, monitor.DpiScaleX);
        var height = ToPixels(heightDip, monitor.DpiScaleY);
        var left = monitor.Bounds.Left + (monitor.Bounds.Width - width) / 2;
        var top = monitor.Bounds.Top + (monitor.Bounds.Height - height) / 2;
        return new WindowRect(left, top, left + width, top + height);
    }

    /// <summary>Monitörün tam genişliğinde, alt kenara yaslı pencere (tam genişlik dock).</summary>
    public static WindowRect BottomFullWidth(DisplayMonitor monitor, double heightDip)
    {
        var height = ToPixels(heightDip, monitor.DpiScaleY);
        return new WindowRect(monitor.Bounds.Left, monitor.Bounds.Bottom - height, monitor.Bounds.Right, monitor.Bounds.Bottom);
    }

    /// <summary>Verilen kenarda tam boy (veya uzunluk yüzdeli) dock penceresi yerleşimi (piksel).</summary>
    public static WindowRect EdgeBar(
        DisplayMonitor monitor,
        DockEdge edge,
        double thicknessDip,
        int lengthPercent = DockLayoutLimits.DefaultLengthPercent,
        double edgeOffsetDip = DockLayoutLimits.DefaultEdgeOffsetDip)
    {
        if (!Enum.IsDefined(edge)) edge = DockEdge.Bottom;
        var thickness = ToPixels(thicknessDip, edge is DockEdge.Bottom or DockEdge.Top ? monitor.DpiScaleY : monitor.DpiScaleX);
        var offset = ToPixels(Math.Max(0.0, edgeOffsetDip), edge is DockEdge.Bottom or DockEdge.Top ? monitor.DpiScaleY : monitor.DpiScaleX);
        var lengthFraction = DockLayoutLimits.ClampLengthPercent(lengthPercent) / 100.0;

        if (edge is DockEdge.Bottom or DockEdge.Top)
        {
            var width = (int)Math.Round(monitor.Bounds.Width * lengthFraction);
            var left = monitor.Bounds.Left + (monitor.Bounds.Width - width) / 2;
            var top = edge == DockEdge.Bottom
                ? monitor.Bounds.Bottom - offset - thickness
                : monitor.Bounds.Top + offset;
            return new WindowRect(left, top, left + width, top + thickness);
        }
        else
        {
            var height = (int)Math.Round(monitor.Bounds.Height * lengthFraction);
            var top = monitor.Bounds.Top + (monitor.Bounds.Height - height) / 2;
            var left = edge == DockEdge.Left
                ? monitor.Bounds.Left + offset
                : monitor.Bounds.Right - offset - thickness;
            return new WindowRect(left, top, left + thickness, top + height);
        }
    }

    /// <summary>Verilen kenarda kapsül dock penceresi yerleşimi (piksel).</summary>
    public static WindowRect EdgeCapsule(
        DisplayMonitor monitor,
        DockEdge edge,
        double lengthDip,
        double thicknessDip,
        double edgeMarginDip)
    {
        if (!Enum.IsDefined(edge)) edge = DockEdge.Bottom;
        var thickness = ToPixels(thicknessDip, edge is DockEdge.Bottom or DockEdge.Top ? monitor.DpiScaleY : monitor.DpiScaleX);
        var margin = ToPixels(Math.Max(0.0, edgeMarginDip), edge is DockEdge.Bottom or DockEdge.Top ? monitor.DpiScaleY : monitor.DpiScaleX);

        if (edge is DockEdge.Bottom or DockEdge.Top)
        {
            var width = Math.Min(ToPixels(lengthDip, monitor.DpiScaleX), monitor.Bounds.Width);
            var left = monitor.Bounds.Left + (monitor.Bounds.Width - width) / 2;
            var top = edge == DockEdge.Bottom
                ? monitor.Bounds.Bottom - margin - thickness
                : monitor.Bounds.Top + margin;
            return new WindowRect(left, top, left + width, top + thickness);
        }
        else
        {
            var height = Math.Min(ToPixels(lengthDip, monitor.DpiScaleY), monitor.Bounds.Height);
            var top = monitor.Bounds.Top + (monitor.Bounds.Height - height) / 2;
            var left = edge == DockEdge.Left
                ? monitor.Bounds.Left + margin
                : monitor.Bounds.Right - margin - thickness;
            return new WindowRect(left, top, left + thickness, top + height);
        }
    }

    /// <summary>
    /// DIP'ten fiziksel piksele TEK dönüşüm kuralı: yarım değerler yukarı yuvarlanır. Dock penceresinin yüksekliği ve
    /// AppBar'ın ayırdığı pay aynı fonksiyondan geçer; böylece büyütülmüş pencere ile çubuk arasında 1 piksellik boşluk
    /// ya da çakışma kalmaz (125 %, 150 % gibi kesirli ölçeklerde kesme/yuvarlama farkı bunu üretiyordu).
    /// </summary>
    public static int ToPixels(double dip, double scale) =>
        (int)Math.Round(dip * (scale > 0 ? scale : 1.0), MidpointRounding.AwayFromZero);
}
