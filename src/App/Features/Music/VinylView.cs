using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Koy.App.Resources;
using Koy.Core.Media;

namespace Koy.App.Features.Music;

/// <summary>
/// Plak modu (issue #182, hareket <see cref="VinylMotion"/>): kapak, plağın ortasındaki etiket olur. Oluklu disk ve
/// ışık yansıması sabittir (oluklar dairesel, dönmesi görünmez); yalnız etiket döner, böylece her karede çizilen alan
/// küçük kalır. Çalarken döner, duraklatınca yayla yavaşlayıp durur, ton kolu kalkar. Kare yalnız görünürken ve
/// hareket sürerken dinlenir, 30 fps ve ortak müzik saatinde (akan kapak rengiyle aynı vuruş, <see cref="MusicFrameClock"/>). Shader efekti yok.
/// </summary>
public sealed class VinylView : Grid
{
    private const double Size = 200;
    private const double LabelSize = 88;
    private const double Fps = 30;

    public static readonly DependencyProperty CoverProperty = DependencyProperty.Register(
        nameof(Cover), typeof(ImageSource), typeof(VinylView), new PropertyMetadata(null, OnCoverChanged));

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(VinylView), new PropertyMetadata(false, (d, e) => ((VinylView)d).OnPlayingChanged((bool)e.NewValue)));

    public ImageSource? Cover
    {
        get => (ImageSource?)GetValue(CoverProperty);
        set => SetValue(CoverProperty, value);
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    private static readonly Brush Grooves = MakeGrooves();
    private static readonly Brush Sheen = MakeSheen();

    private readonly VinylMotion _motion = new();
    private readonly RotateTransform _labelTurn = new(0, Size / 2, Size / 2);
    private readonly RotateTransform _armTurn = new(VinylMotion.ArmRestAngle, 7, 7);
    private readonly Ellipse _label;

    private object? _clock;

    public VinylView()
    {
        Width = Size;
        Height = Size;
        IsHitTestVisible = false;

        Children.Add(new Ellipse { Fill = Grooves });

        // Dönen katman: etiket (kapak), etiketteki küçük parlak nokta (dönüşü gösterir) ve orta delik.
        var spinning = new Grid { RenderTransform = _labelTurn };
        _label = new Ellipse { Width = LabelSize, Height = LabelSize, Fill = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)) };
        spinning.Children.Add(_label);
        spinning.Children.Add(new Ellipse
        {
            Width = 12, Height = 12, Fill = new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -60, 0, 0),
        });
        spinning.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0B)) });
        Children.Add(spinning);

        Children.Add(new Ellipse { Fill = Sheen });
        Children.Add(MakeArm(_armTurn));

        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                // Görünüm açılınca hareket yarım kalmış gibi başlamasın: kol ve hız o anki duruma oturur.
                _motion.SetPlaying(IsPlaying);
                _motion.Snap();
                Apply();
            }
            UpdateClock();
        };
        Unloaded += (_, _) => StopClock();
    }

    private static void OnCoverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (VinylView)d;
        if (e.NewValue is ImageSource cover)
        {
            var brush = new ImageBrush(cover) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            view._label.Fill = brush;
        }
        else
        {
            view._label.Fill = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
        }
    }

    private void OnPlayingChanged(bool playing)
    {
        _motion.SetPlaying(playing);
        if (!Motion.Enabled)
            _motion.Snap();
        Apply();
        UpdateClock();
    }

    private void UpdateClock()
    {
        if (IsVisible && Motion.Enabled && !_motion.IsAtRest)
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
        _motion.Step(step);
        Apply();
        UpdateClock();
    }

    private void Apply()
    {
        _labelTurn.Angle = _motion.Angle;
        _armTurn.Angle = _motion.ArmAngle;
    }

    // Ton kolu: sağ üstte, döndüğü nokta (7, 7); tasarımdaki ölçüler.
    private static Canvas MakeArm(RotateTransform turn)
    {
        var arm = new Canvas
        {
            Width = 14, Height = 140,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -8, -14, 0),
            RenderTransform = turn,
        };
        var rod = new Rectangle
        {
            Width = 4, Height = 118, RadiusX = 2, RadiusY = 2,
            Fill = new LinearGradientBrush(Color.FromRgb(0xA1, 0xA1, 0xAA), Color.FromRgb(0xE4, 0xE4, 0xE7), 90),
        };
        Canvas.SetLeft(rod, 5);
        Canvas.SetTop(rod, 7);
        var head = new Rectangle { Width = 12, Height = 18, RadiusX = 3, RadiusY = 3, Fill = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD8)) };
        Canvas.SetLeft(head, 1);
        Canvas.SetTop(head, 120);
        var pivot = new Ellipse { Width = 14, Height = 14, Fill = new SolidColorBrush(Color.FromRgb(0x71, 0x71, 0x7A)) };
        arm.Children.Add(rod);
        arm.Children.Add(head);
        arm.Children.Add(pivot);
        return arm;
    }

    // Oluklar: koyu zeminde iç içe ince halkalar, bir kez çizilip dondurulur.
    private static DrawingBrush MakeGrooves()
    {
        var group = new DrawingGroup();
        var center = new Point(Size / 2, Size / 2);
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x13)), null,
            new EllipseGeometry(center, Size / 2, Size / 2)));
        var groove = new Pen(new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x23)), 1);
        groove.Freeze();
        for (var r = LabelSize / 2 + 6; r < Size / 2 - 3; r += 4)
            group.Children.Add(new GeometryDrawing(null, groove, new EllipseGeometry(center, r, r)));
        group.Freeze();
        var brush = new DrawingBrush(group) { Stretch = Stretch.Uniform };
        brush.Freeze();
        return brush;
    }

    // Sabit ışık yansıması: çapraz ince bir parlaklık bandı.
    private static LinearGradientBrush MakeSheen()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.2),
            EndPoint = new Point(1, 0.8),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 0.4),
                new GradientStop(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), 0.5),
                new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 0.6),
            },
        };
        brush.Freeze();
        return brush;
    }
}
