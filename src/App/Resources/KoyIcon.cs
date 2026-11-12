using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Koy.App.Resources;

/// <summary>
/// Tek ikon bileşeni: 24x24 birimlik Lucide geometrisini çizgi olarak çizer.
/// Renk <see cref="Control.Foreground"/> değerinden gelir (üst öğeden miras alınır), bu yüzden tema ve hover
/// durumları metinle aynı yoldan akar. Boyut 16 (varsayılan) ya da 20; çizgi kalınlığı 24 birimlik tuvalde 2.
/// Düz Unicode glif ya da emoji ikon kullanılmaz.
/// </summary>
public sealed class KoyIcon : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(KoyIcon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(KoyIcon),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.Register(
        nameof(StrokeWidth), typeof(double), typeof(KoyIcon),
        new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    static KoyIcon()
    {
        // Foreground değişince (tema, hover) yeniden çizilsin.
        ForegroundProperty.OverrideMetadata(typeof(KoyIcon), new FrameworkPropertyMetadata(
            Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(KoyIcon), new UIPropertyMetadata(false));
        FocusableProperty.OverrideMetadata(typeof(KoyIcon), new FrameworkPropertyMetadata(false));
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>24 birimlik tuvaldeki çizgi kalınlığı (Lucide varsayılanı 2).</summary>
    public double StrokeWidth
    {
        get => (double)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    protected override Size MeasureOverride(Size constraint) => new(Size, Size);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Data is null || Foreground is null)
            return;

        var scale = Size / 24.0;
        var pen = new Pen(Foreground, StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(null, pen, Data);
        drawingContext.Pop();
    }
}
