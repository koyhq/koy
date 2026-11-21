using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Koy.App.Resources;

/// <summary>
/// Gölgeli <see cref="Border"/>: <c>DropShadowEffect</c> yerine kullanılır.
/// WPF'in shader efektleri (DropShadowEffect, BlurEffect) yazılımla çizimde bile ilk çizildikleri anda Direct3D yığınını
/// (d3d9on12, d3d12, NVIDIA / Intel sürücüleri) yükler; bu yaklaşık 140 MB private bellek ekler ve süreç kapanana dek
/// geri verilmez (ölçüm 2026-11-20, arastirma/ui-hareket-2026-11/07). Burada gölge, kenarın dışına doğru büyüyen ve
/// saydamlığı azalan birkaç yuvarlak halka olarak çizilir: shader yok, Direct3D yok. Halkalar yalnız kenarın dışını
/// boyar, yarı saydam zeminin altı kararmaz. Geometriler boyut ya da ayar değişince bir kez kurulup dondurulur.
/// </summary>
public class ShadowBorder : Border
{
    private const int Layers = 8;

    public static readonly DependencyProperty ShadowBlurProperty = DependencyProperty.Register(
        nameof(ShadowBlur), typeof(double), typeof(ShadowBorder),
        new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.AffectsRender, OnShadowChanged));

    public static readonly DependencyProperty ShadowDepthProperty = DependencyProperty.Register(
        nameof(ShadowDepth), typeof(double), typeof(ShadowBorder),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender, OnShadowChanged));

    public static readonly DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(
        nameof(ShadowOpacity), typeof(double), typeof(ShadowBorder),
        new FrameworkPropertyMetadata(0.35, FrameworkPropertyMetadataOptions.AffectsRender, OnShadowChanged));

    /// <summary>Gölgenin kenardan dışarı yayıldığı uzaklık (DIP); DropShadowEffect.BlurRadius'un yaklaşık yarısı kadar görünür.</summary>
    public double ShadowBlur
    {
        get => (double)GetValue(ShadowBlurProperty);
        set => SetValue(ShadowBlurProperty, value);
    }

    /// <summary>Gölgenin aşağı kayması (DIP).</summary>
    public double ShadowDepth
    {
        get => (double)GetValue(ShadowDepthProperty);
        set => SetValue(ShadowDepthProperty, value);
    }

    /// <summary>Kenara en yakın yerdeki gölge koyuluğu (0–1).</summary>
    public double ShadowOpacity
    {
        get => (double)GetValue(ShadowOpacityProperty);
        set => SetValue(ShadowOpacityProperty, value);
    }

    private DrawingGroup? _shadow;
    private Size _shadowSize;

    private static void OnShadowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ShadowBorder)d)._shadow = null;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _shadow = null;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == CornerRadiusProperty)
            _shadow = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = RenderSize;
        if (ShadowOpacity > 0 && ShadowBlur > 0 && size.Width > 0 && size.Height > 0)
        {
            if (_shadow is null || _shadowSize != size)
            {
                _shadow = BuildShadow(size);
                _shadowSize = size;
            }
            dc.DrawDrawing(_shadow);
        }
        base.OnRender(dc);
    }

    private DrawingGroup BuildShadow(Size size)
    {
        var group = new DrawingGroup();
        var blur = ShadowBlur;
        var radius = CornerRadius.TopLeft;
        var body = new RectangleGeometry(new Rect(size), radius, radius);
        body.Freeze();

        // Her katman bir öncekinden biraz daha geniş; üst üste binen katmanlar kenarda ShadowOpacity'ye, dışta sıfıra iner.
        var alpha = 1 - Math.Pow(1 - Math.Clamp(ShadowOpacity, 0, 1), 1.0 / Layers);
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(alpha * 255), 0, 0, 0));
        brush.Freeze();
        for (var i = 1; i <= Layers; i++)
        {
            var spread = blur * i / Layers;
            var rect = new Rect(-spread, -spread + ShadowDepth, size.Width + 2 * spread, size.Height + 2 * spread);
            var layer = new RectangleGeometry(rect, radius + spread, radius + spread);
            var ring = new CombinedGeometry(GeometryCombineMode.Exclude, layer, body);
            ring.Freeze();
            group.Children.Add(new GeometryDrawing(brush, null, ring));
        }
        group.Freeze();
        return group;
    }
}
