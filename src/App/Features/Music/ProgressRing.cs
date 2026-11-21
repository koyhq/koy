using System.Windows;
using System.Windows.Media;

namespace Koy.App.Features.Music;

/// <summary>
/// Kapalı çentikte kapağın çevresindeki ilerleme halkası (issue #182). Yalnız <see cref="Progress"/> değişince
/// yeniden çizilir (saniyede bir, ilerleme zamanlayıcısıyla); kendi saati yok. Shader efekti yok.
/// </summary>
public sealed class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ProgressRing),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(ProgressRing),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(ProgressRing),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(ProgressRing),
        new FrameworkPropertyMetadata(2.5, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0–1. Canlı yayında (süre yok) yalnız iz çizilir.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Track
    {
        get => (Brush?)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness)
            return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - Thickness) / 2;

        if (Track is { } track)
            drawingContext.DrawEllipse(null, new Pen(track, Thickness), center, radius, radius);

        var p = double.IsFinite(Progress) ? Math.Clamp(Progress, 0, 1) : 0;
        if (p <= 0)
            return;

        var pen = new Pen(Stroke, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (p >= 0.999)
        {
            drawingContext.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        // Saat 12'den saat yönünde yay.
        var angle = p * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0, isLargeArc: p > 0.5, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }
}
