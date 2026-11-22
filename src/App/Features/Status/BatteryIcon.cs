using System.Windows;
using System.Windows.Media;
using Koy.Core.Power;

namespace Koy.App.Features.Status;

/// <summary>
/// Windows 11 tarzı pil simgesi: yatay gövde, dolum, içinde şarj (yıldırım), prizde (fiş) ya da pil tasarrufu (yaprak) işareti.
/// Karar <see cref="BatteryDisplay"/>'dedir (Core); burası yalnız çizer. Gölge ya da efekt yok, yalnız dolgu ve çizgi (AGENTS #3),
/// yeniden çizim yalnız değer değişince.
/// </summary>
public sealed class BatteryIcon : FrameworkElement
{
    public static readonly DependencyProperty DisplayProperty = DependencyProperty.Register(
        nameof(Display), typeof(BatteryDisplay), typeof(BatteryIcon),
        new FrameworkPropertyMetadata(BatteryDisplay.None, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Gövde çizgisi ve ucu; tema metin rengi (dock: KoyDockGlyph, çentik: KoyNotchText).</summary>
    public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.Register(
        nameof(OutlineBrush), typeof(Brush), typeof(BatteryIcon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Normal ve şarj dolumu (yeşil).</summary>
    public static readonly DependencyProperty OkBrushProperty = DependencyProperty.Register(
        nameof(OkBrush), typeof(Brush), typeof(BatteryIcon),
        new FrameworkPropertyMetadata(Frozen(0x4A, 0xDE, 0x80), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Kritik dolum (kırmızı).</summary>
    public static readonly DependencyProperty CriticalBrushProperty = DependencyProperty.Register(
        nameof(CriticalBrush), typeof(Brush), typeof(BatteryIcon),
        new FrameworkPropertyMetadata(Frozen(0xFB, 0x71, 0x85), FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush WarningBrush = Frozen(0xFB, 0xBF, 0x24);
    private static readonly Brush MarkBrush = Frozen(0x1A, 0x1A, 0x1A);
    private static readonly Pen MarkHalo = FrozenPen(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF), 2.6);

    public BatteryDisplay Display
    {
        get => (BatteryDisplay)GetValue(DisplayProperty);
        set => SetValue(DisplayProperty, value);
    }

    public Brush OutlineBrush
    {
        get => (Brush)GetValue(OutlineBrushProperty);
        set => SetValue(OutlineBrushProperty, value);
    }

    public Brush OkBrush
    {
        get => (Brush)GetValue(OkBrushProperty);
        set => SetValue(OkBrushProperty, value);
    }

    public Brush CriticalBrush
    {
        get => (Brush)GetValue(CriticalBrushProperty);
        set => SetValue(CriticalBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(22, 12);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dc = drawingContext;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 8 || h < 5)
            return;

        var d = Display;
        var outline = OutlineBrush;
        const double nubW = 2;
        var bodyW = w - nubW;

        // Gövde (1 px çizgi) ve uç.
        var pen = new Pen(outline, 1);
        dc.DrawRoundedRectangle(null, pen, new Rect(0.5, 0.5, bodyW - 1, h - 1), 2.5, 2.5);
        var nubH = Math.Max(3, h * 0.4);
        dc.DrawRoundedRectangle(outline, null, new Rect(bodyW, (h - nubH) / 2, nubW - 0.5, nubH), 1, 1);

        // Dolum: yüzde sıfırın üstündeyse en az 1,5 px görünür.
        var inner = new Rect(2, 2, bodyW - 4, h - 4);
        if (d.Percent > 0)
        {
            var fillW = Math.Max(1.5, inner.Width * d.FillRatio);
            var brush = d.Tone switch
            {
                BatteryTone.Critical => CriticalBrush,
                BatteryTone.Warning => WarningBrush,
                _ => OkBrush
            };
            dc.DrawRoundedRectangle(brush, null, new Rect(inner.X, inner.Y, fillW, inner.Height), 1, 1);
        }

        var mark = BatteryMarks.For(d.Overlay);
        if (mark is null)
            return;

        // İşaret gövdenin ortasında, gövde yüksekliğine sığacak kadar büyük.
        var size = Math.Min(h - 1.5, bodyW - 3);
        var scale = size / 24.0;
        dc.PushTransform(new TranslateTransform((bodyW - size) / 2, (h - size) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(null, MarkHalo, mark);
        dc.DrawGeometry(MarkBrush, null, mark);
        dc.Pop();
        dc.Pop();
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Color color, double thickness)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new Pen(brush, thickness) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }
}
