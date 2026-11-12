using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Koy.App.Features.ControlCenter;

/// <summary>
/// Wi-Fi sinyal çubukları: dört çubuktan <see cref="Bars"/> kadarı dolu, kalanı soluk çizilir. Renk
/// <see cref="Control.Foreground"/> değerinden gelir. Çizim yalnız değer ya da renk değişince yenilenir.
/// </summary>
public sealed class SignalBarsIcon : Control
{
    public static readonly DependencyProperty BarsProperty = DependencyProperty.Register(
        nameof(Bars), typeof(int), typeof(SignalBarsIcon),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double BarWidth = 3;
    private const double BarGap = 1.8;
    private const double FullHeight = 14;
    private const int Count = 4;

    static SignalBarsIcon()
    {
        ForegroundProperty.OverrideMetadata(typeof(SignalBarsIcon), new FrameworkPropertyMetadata(
            Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(SignalBarsIcon), new UIPropertyMetadata(false));
        FocusableProperty.OverrideMetadata(typeof(SignalBarsIcon), new FrameworkPropertyMetadata(false));
    }

    public int Bars
    {
        get => (int)GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    protected override Size MeasureOverride(Size constraint) =>
        new(Count * BarWidth + (Count - 1) * BarGap, FullHeight);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var filled = Foreground;
        for (var i = 0; i < Count; i++)
        {
            var height = FullHeight * (i + 1) / Count;
            var rect = new Rect(i * (BarWidth + BarGap), FullHeight - height, BarWidth, height);
            drawingContext.PushOpacity(i < Bars ? 1.0 : 0.28);
            drawingContext.DrawRoundedRectangle(filled, null, rect, 1, 1);
            drawingContext.Pop();
        }
    }
}
