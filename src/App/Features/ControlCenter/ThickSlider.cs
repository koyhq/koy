using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Koy.App.Resources;

namespace Koy.App.Features.ControlCenter;

/// <summary>
/// Pil göstergesi gibi dolan kalın kaydırıcı (ses, parlaklık; D tasarımı). İçinde simge, ad ve yüzde yazar: dolu kısımdaki
/// yazı koyu, boş kısımdaki açık (koyu katman dolgu genişliğinde kırpılır). Basıp sürükleyerek, tekerlekle ya da ok
/// tuşlarıyla değişir. Simgenin üstüne tıklayıp sürüklemeden bırakmak <see cref="IconClicked"/> verir (sessize alma).
/// Yalnız kullanıcı değiştirdiğinde <see cref="UserValueChanged"/> gelir; bağlamadan gelen değer olay üretmez.
/// </summary>
public sealed class ThickSlider : FrameworkElement
{
    private const double Radius = 14;
    private const double IconZone = 46;
    private const double Step = 5;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ThickSlider),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(ThickSlider),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ThickSlider),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(ThickSlider),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush), typeof(Brush), typeof(ThickSlider),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush Track = Frozen(Color.FromArgb(0x17, 0xFF, 0xFF, 0xFF));
    private static readonly Brush Light = Frozen(Color.FromRgb(0xF4, 0xF4, 0xF5));
    private static readonly Brush LightMuted = Frozen(Color.FromArgb(0xB3, 0xF4, 0xF4, 0xF5));
    private static readonly Brush Dark = Frozen(Color.FromRgb(0x0A, 0x0A, 0x0B));

    private Point _pressAt;
    private bool _pressedOnIcon;
    private bool _dragged;

    public ThickSlider()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        FocusVisualStyle = null;
        Height = 46;
        SnapsToDevicePixels = true;
    }

    /// <summary>Yüzde, 0–100.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Dolgu rengi (varsayılan çentik yazı rengi).</summary>
    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Boş kısımdaki simge rengi (null ise yazıyla aynı; ör. sessizde kırmızı).</summary>
    public Brush? IconBrush
    {
        get => (Brush?)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    /// <summary>Kullanıcı sürükledi, tekerleği çevirdi ya da ok tuşuna bastı.</summary>
    public event EventHandler<double>? UserValueChanged;

    /// <summary>Simgeye tıklandı (sürüklenmeden).</summary>
    public event EventHandler? IconClicked;

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, Height);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dc = drawingContext;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        var bounds = new Rect(0, 0, w, h);
        var fillWidth = Math.Clamp(Value, 0, 100) / 100 * w;
        var shape = new RectangleGeometry(bounds, Radius, Radius);

        dc.PushClip(shape);
        dc.DrawRectangle(Track, null, bounds);
        if (fillWidth > 0)
            dc.DrawRectangle(Fill, null, new Rect(0, 0, fillWidth, h));

        // Açık katman (boş kısım), sonra dolgu genişliğinde kırpılmış koyu katman.
        DrawContent(dc, w, h, Light, IconBrush ?? Light);
        if (fillWidth > 0)
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, fillWidth, h)));
            DrawContent(dc, w, h, Dark, Dark);
            dc.Pop();
        }

        dc.Pop();

        if (IsKeyboardFocused)
            dc.DrawRoundedRectangle(null, new Pen(LightMuted, 1.5), new Rect(0.75, 0.75, w - 1.5, h - 1.5), Radius, Radius);
    }

    private void DrawContent(DrawingContext dc, double w, double h, Brush text, Brush icon)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var family = (TryFindResource("KoyNotchFont") as FontFamily) ?? new FontFamily("Segoe UI");
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var x = 16.0;

        if (Icon is { } geometry)
        {
            const double size = 17;
            var b = geometry.Bounds;
            var scale = b.Width > 0 && b.Height > 0 ? size / Math.Max(b.Width, b.Height) : 1;
            dc.PushTransform(new TranslateTransform(x - b.X * scale, (h - size) / 2 - b.Y * scale));
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawGeometry(null, new Pen(icon, 2 / scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, geometry);
            dc.Pop();
            dc.Pop();
            x += size + 10;
        }

        var label = new FormattedText(Label ?? "", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, text, dpi);
        dc.DrawText(label, new Point(x, (h - label.Height) / 2));

        var percent = PercentTextConverter.Instance.Convert((int)Math.Round(Value), typeof(string), string.Empty, System.Globalization.CultureInfo.CurrentCulture) as string ?? "";
        var value = new FormattedText(percent, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, text, dpi);
        dc.DrawText(value, new Point(w - 16 - value.Width, (h - value.Height) / 2));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
        _pressAt = e.GetPosition(this);
        _pressedOnIcon = _pressAt.X < IconZone;
        _dragged = false;
        if (!_pressedOnIcon)
            SetFromPointer(_pressAt.X);
        // Tıklama çentiğe ulaşıp "boşluğa tıklandı" sayılmasın.
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured)
            return;

        var p = e.GetPosition(this);
        if (!_dragged && Math.Abs(p.X - _pressAt.X) < 3)
            return;

        _dragged = true;
        SetFromPointer(p.X);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!IsMouseCaptured)
            return;

        ReleaseMouseCapture();
        if (_pressedOnIcon && !_dragged)
            IconClicked?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        Change(Value + (e.Delta > 0 ? Step : -Step));
        // Tekerlek çentiğe ulaşıp sekme değiştirmesin.
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var delta = e.Key switch
        {
            Key.Right or Key.Up => Step,
            Key.Left or Key.Down => -Step,
            _ => 0
        };
        if (delta != 0)
        {
            Change(Value + delta);
            e.Handled = true;
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        InvalidateVisual();
    }

    private void SetFromPointer(double x) => Change(ActualWidth > 0 ? x / ActualWidth * 100 : 0);

    private void Change(double value)
    {
        value = Math.Round(Math.Clamp(value, 0, 100));
        if (value == Value)
            return;

        Value = value;
        UserValueChanged?.Invoke(this, value);
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
