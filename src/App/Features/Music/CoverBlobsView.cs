using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Koy.App.Resources;
using Koy.Core.Appearance;
using Koy.Core.Media;

namespace Koy.App.Features.Music;

/// <summary>
/// Müzik sayfasında kapak renginden akan zemin (issue #182, hesap <see cref="CoverBlobs"/>). Üç radyal gradyanlı
/// elips yavaşça kayar ve büyüyüp küçülür. Kare yalnız görünürken ve çalarken, 10 fps (hareket en hızlı noktada
/// saniyede ~11 DIP, kare başına ~1 DIP; gradyan kenarları yumuşak olduğu için akıcı görünür) ve ortak müzik saatinde
/// (<see cref="MusicFrameClock">plakla aynı vuruş</see>); duraklatınca lekeler oldukları yerde durur. Parça değişince renkler 0,6 sn'de yenilerine geçer (duraklatılmışken de).
/// Hareket kapalıysa (Windows "animasyonları azalt", pil tasarrufu, Animasyon: Kapalı) lekeler sabit durur.
/// Shader efekti yok: yalnız gradyan, kayma ve ölçek.
/// </summary>
public sealed class CoverBlobsView : Canvas
{
    private const double Fps = 10;

    // Lekelerin kartın sol üstüne göre yeri ve çapı (tasarım: 620×352 çentikte 420 / 460 / 360).
    private static readonly (double X, double Y, double Size, double Alpha)[] Layout =
    [
        (-80, -60, 420, 0.55),
        (300, 40, 460, 0.45),
        (120, 180, 360, 0.40),
    ];

    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors), typeof(Rgb[]), typeof(CoverBlobsView), new PropertyMetadata(null, OnColorsChanged));

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(CoverBlobsView), new PropertyMetadata(false, (d, _) => ((CoverBlobsView)d).UpdateClock()));

    /// <summary>Üç leke rengi (<see cref="CoverBlobs.ColorsFrom"/>); null ise lekeler gizlenir.</summary>
    public Rgb[]? Colors
    {
        get => (Rgb[]?)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    /// <summary>Kırpmanın köşe yarıçapı (kartınkiyle aynı).</summary>
    public double CornerRadius { get; set; } = 20;

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    private readonly (RadialGradientBrush Brush, TranslateTransform Move, ScaleTransform Scale)[] _blobs;
    private Rgb[] _from = [];
    private Rgb[] _to = [];
    private double _fade = 1;

    private object? _clock;
    private double _seconds;

    public CoverBlobsView()
    {
        IsHitTestVisible = false;
        _blobs = new (RadialGradientBrush, TranslateTransform, ScaleTransform)[Layout.Length];
        for (var i = 0; i < Layout.Length; i++)
        {
            var (x, y, size, _) = Layout[i];
            var brush = new RadialGradientBrush { GradientStops = { new GradientStop(), new GradientStop { Offset = 1 } } };
            var move = new TranslateTransform();
            var scale = new ScaleTransform();
            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup { Children = { scale, move } },
            };
            SetLeft(ellipse, x);
            SetTop(ellipse, y);
            Children.Add(ellipse);
            _blobs[i] = (brush, move, scale);
        }

        IsVisibleChanged += (_, _) => UpdateClock();
        // Kartın yuvarlak köşelerinden taşmasın.
        SizeChanged += (_, e) => Clip = new RectangleGeometry(new Rect(e.NewSize), CornerRadius, CornerRadius);
        Unloaded += (_, _) => StopClock();
        Visibility = Visibility.Collapsed;
    }

    private static void OnColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (CoverBlobsView)d;
        if (e.NewValue is not Rgb[] { Length: > 0 } next)
        {
            view.Visibility = Visibility.Collapsed;
            view._from = view._to = [];
            return;
        }

        // İlk renk ya da hareket kapalıyken doğrudan; değilse o anki renkten yenisine geçiş.
        var first = view._to.Length == 0;
        view._from = first ? next : view.CurrentColors();
        view._to = next;
        view._fade = first || !Motion.Enabled || !view.IsVisible ? 1 : 0;
        view.Visibility = Visibility.Visible;
        view.ApplyColors();
        view.UpdateClock();
    }

    private Rgb[] CurrentColors()
    {
        var colors = new Rgb[_to.Length];
        for (var i = 0; i < colors.Length; i++)
            colors[i] = CoverBlobs.Blend(_from.Length > i ? _from[i] : _to[i], _to[i], _fade);
        return colors;
    }

    private void ApplyColors()
    {
        var colors = CurrentColors();
        for (var i = 0; i < _blobs.Length && i < colors.Length; i++)
        {
            var c = colors[i];
            var alpha = (byte)(Layout[i].Alpha * 255);
            _blobs[i].Brush.GradientStops[0].Color = Color.FromArgb(alpha, c.R, c.G, c.B);
            _blobs[i].Brush.GradientStops[1].Color = Color.FromArgb(0, c.R, c.G, c.B);
        }
    }

    private void ApplyMotion()
    {
        for (var i = 0; i < _blobs.Length; i++)
        {
            var f = CoverBlobs.FrameAt(i, _seconds);
            _blobs[i].Move.X = f.OffsetX;
            _blobs[i].Move.Y = f.OffsetY;
            _blobs[i].Scale.ScaleX = _blobs[i].Scale.ScaleY = f.Scale;
        }
    }

    // Saat yalnız görünürken ve (çalıyorsa ya da renk geçişi sürüyorsa) çalışır.
    private void UpdateClock()
    {
        var moving = IsPlaying && Motion.Enabled;
        var want = IsVisible && _to.Length > 0 && (moving || _fade < 1);
        if (want)
            StartClock();
        else
            StopClock();
    }

    private void StartClock() => _clock ??= MusicFrameClock.Subscribe(Fps, OnFrame);

    private void StopClock()
    {
        MusicFrameClock.Unsubscribe(_clock);
        _clock = null;
    }

    private void OnFrame(double step)
    {
        if (IsPlaying && Motion.Enabled)
        {
            _seconds += step;
            ApplyMotion();
        }

        if (_fade < 1)
        {
            _fade = Math.Min(1, _fade + step / CoverBlobs.ColorCrossfade.TotalSeconds);
            ApplyColors();
        }

        UpdateClock();
    }
}
