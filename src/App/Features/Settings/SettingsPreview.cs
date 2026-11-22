using System.Windows;
using System.Windows.Media;

namespace Koy.App.Features.Settings;

/// <summary>Ayar önizlemesinin çizdiği sahne.</summary>
public enum SettingsPreviewKind
{
    DockCapsule,
    DockFullWidth,
    ProxReserve,
    ProxDockDown,
    ProxDodge,
    ProxAlwaysHidden,
    ProxAlwaysFloating,
    NotchPinned,
    NotchFloating,
    CollapsedDefault,
    CollapsedAlbum,
    CollapsedAmbient
}

/// <summary>
/// Ayar sayfalarındaki küçük vektör önizlemeler (ekran çerçevesi, pencere, dock ya da çentik). Resim dosyası, gölge ya da efekt yok;
/// yalnız dolgu ve çizgi, <c>OnRender</c> ile görünürken bir kez çizilir (sayfa görünmezken hiçbir şey tutulmaz).
/// Renkler ayar sayfasının tema token'larıdır (<see cref="Stroke"/>, <see cref="Accent"/>).
/// </summary>
public sealed class SettingsPreview : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(SettingsPreviewKind), typeof(SettingsPreview),
        new FrameworkPropertyMetadata(SettingsPreviewKind.DockCapsule, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(SettingsPreview),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(SettingsPreview),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public SettingsPreviewKind Kind
    {
        get => (SettingsPreviewKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush Accent
    {
        get => (Brush)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(76, 48);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dc = drawingContext;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 20 || h < 16)
            return;

        var line = new Pen(Stroke, 1);
        var soft = new SolidColorBrush(Color.FromArgb(0x30, 0x88, 0x88, 0x88));
        var winFill = new SolidColorBrush(Color.FromArgb(0x38, 0x88, 0x88, 0x88));

        // Ekran çerçevesi
        dc.DrawRoundedRectangle(null, line, new Rect(0.5, 0.5, w - 1, h - 1), 4, 4);

        switch (Kind)
        {
            case SettingsPreviewKind.DockCapsule:
                dc.DrawRoundedRectangle(winFill, null, new Rect(6, 6, w - 12, h - 20), 2, 2);
                Capsule(dc, w * 0.5, h - 10);
                break;
            case SettingsPreviewKind.DockFullWidth:
                dc.DrawRoundedRectangle(winFill, null, new Rect(6, 6, w - 12, h - 18), 2, 2);
                dc.DrawRectangle(Accent, null, new Rect(1, h - 9, w - 2, 8));
                for (var i = 0; i < 5; i++)
                    dc.DrawEllipse(Brushes.White, null, new Point(w * 0.5 - 10 + i * 5, h - 5), 1.4, 1.4);
                break;
            case SettingsPreviewKind.ProxReserve:
                dc.DrawRoundedRectangle(winFill, null, new Rect(5, 5, w - 10, h - 21), 2, 2);   // pencere dock'un üstünde biter
                Capsule(dc, w * 0.5, h - 9);
                break;
            case SettingsPreviewKind.ProxDockDown:
                dc.DrawRoundedRectangle(winFill, null, new Rect(5, 5, w - 10, h - 14), 2, 2);   // pencere büyük, dock kenara yapışık
                dc.DrawRectangle(Accent, null, new Rect(w * 0.25, h - 8, w * 0.5, 7));
                break;
            case SettingsPreviewKind.ProxDodge:
                dc.DrawRoundedRectangle(winFill, null, new Rect(5, 5, w - 10, h - 10), 2, 2);
                dc.DrawRoundedRectangle(Accent, null, new Rect(w * 0.25, h - 4, w * 0.5, 6), 3, 3);   // dock yarı çekilmiş
                Arrow(dc, w * 0.5, h - 12);
                break;
            case SettingsPreviewKind.ProxAlwaysHidden:
                dc.DrawRoundedRectangle(winFill, null, new Rect(5, 5, w - 10, h - 10), 2, 2);
                dc.DrawRoundedRectangle(Accent, null, new Rect(w * 0.3, h - 4, w * 0.4, 2), 1, 1);   // yalnız ince kenar çizgisi
                break;
            case SettingsPreviewKind.ProxAlwaysFloating:
                dc.DrawRoundedRectangle(winFill, null, new Rect(5, 5, w - 10, h - 10), 2, 2);
                Capsule(dc, w * 0.5, h - 12);   // dock pencerenin üstünde havada
                break;
            case SettingsPreviewKind.NotchPinned:
                Notch(dc, w, pinned: true, SettingsPreviewKind.CollapsedDefault);
                break;
            case SettingsPreviewKind.NotchFloating:
                Notch(dc, w, pinned: false, SettingsPreviewKind.CollapsedDefault);
                break;
            case SettingsPreviewKind.CollapsedDefault:
            case SettingsPreviewKind.CollapsedAlbum:
            case SettingsPreviewKind.CollapsedAmbient:
                Notch(dc, w, pinned: true, Kind);
                break;
        }

        _ = soft;
    }

    private void Capsule(DrawingContext dc, double cx, double cy)
    {
        var width = 26.0;
        dc.DrawRoundedRectangle(Accent, null, new Rect(cx - width / 2, cy - 3.5, width, 7), 3.5, 3.5);
        for (var i = 0; i < 4; i++)
            dc.DrawEllipse(Brushes.White, null, new Point(cx - 7.5 + i * 5, cy), 1.3, 1.3);
    }

    private void Arrow(DrawingContext dc, double x, double y)
    {
        var pen = new Pen(Accent, 1.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(pen, new Point(x - 3, y + 1.5), new Point(x, y - 1.5));
        dc.DrawLine(pen, new Point(x, y - 1.5), new Point(x + 3, y + 1.5));
    }

    /// <summary>Kapalı çentik: kulaklı yapışık (Klasik) ya da yüzen ada; zemin türüne göre dolgu.</summary>
    private void Notch(DrawingContext dc, double w, bool pinned, SettingsPreviewKind background)
    {
        var nw = w * 0.46;
        var left = (w - nw) / 2;
        var top = pinned ? 0.5 : 6.0;
        var height = 13.0;

        Brush fill = background switch
        {
            SettingsPreviewKind.CollapsedAlbum => new LinearGradientBrush(Color.FromRgb(0xC0, 0x26, 0xD3), Color.FromRgb(0x25, 0x63, 0xEB), 0),
            SettingsPreviewKind.CollapsedAmbient => new LinearGradientBrush(Color.FromRgb(0x0E, 0x74, 0x90), Color.FromRgb(0x7C, 0x3A, 0xED), 45),
            _ => new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x20))
        };

        Geometry shape;
        if (pinned)
        {
            // Üst kenara yapışık: üst köşeler düz, alt köşeler yuvarlak.
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(left, top), true, true);
                ctx.LineTo(new Point(left + nw, top), true, false);
                ctx.LineTo(new Point(left + nw, top + height - 6), true, false);
                ctx.ArcTo(new Point(left + nw - 6, top + height), new Size(6, 6), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(left + 6, top + height), true, false);
                ctx.ArcTo(new Point(left, top + height - 6), new Size(6, 6), 0, false, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            shape = g;
        }
        else
        {
            shape = new RectangleGeometry(new Rect(left, top, nw, height), height / 2, height / 2);
        }

        dc.DrawGeometry(fill, new Pen(Stroke, 0.8), shape);

        if (background == SettingsPreviewKind.CollapsedAlbum)
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(left + 5, top + 3.5, 6, 6), 1.5, 1.5);
        else if (background == SettingsPreviewKind.CollapsedAmbient)
        {
            var band = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)), 2);
            dc.DrawLine(band, new Point(left + nw * 0.3, top + height), new Point(left + nw * 0.45, top));
            dc.DrawLine(band, new Point(left + nw * 0.6, top + height), new Point(left + nw * 0.75, top));
        }
        else
        {
            dc.DrawEllipse(Accent, null, new Point(left + 7, top + height / 2), 1.8, 1.8);
        }
    }
}
