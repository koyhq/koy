using System.Windows;
using System.Windows.Media;
using Koy.Core.Dock;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock simgelerinin gösterge ve rozet görünümü (ayardan bir kez türetilir, öğeler paylaşır). Kararlar Core'dadır
/// (<see cref="DockPresentationMath"/>); burada yalnız WPF karşılıkları var. Fırça null ise XAML'deki tema vurgusu
/// (DynamicResource KoyAccent) kullanılır: böylece vurgu rengi tema değişince canlı güncellenir.
/// </summary>
public sealed class DockPresentationStyle
{
    // Nötr renk: bugünkü "çalışıyor" noktasıyla aynı gri.
    private static readonly Color NeutralColor = Color.FromRgb(0xA1, 0xA1, 0xAA);

    public static readonly DockPresentationStyle Default = From(new Koy.Core.Settings.DockSettings());

    public DockIndicatorStyle IndicatorStyle { get; private init; }
    public bool UseLine => IndicatorStyle == DockIndicatorStyle.Line;
    public bool IsHighlight => IndicatorStyle == DockIndicatorStyle.Highlight;
    public bool DrawsMark => DockPresentationMath.DrawsIndicatorMark(IndicatorStyle);
    public bool OnePerWindow { get; private init; }

    /// <summary>Gösterge fırçası; vurgu rengi seçiliyse null (tema vurgusu).</summary>
    public Brush? IndicatorBrush { get; private init; }

    /// <summary>Arka plan vurgusu fırçası (gösterge renginin şeffaf hâli); vurgu rengi seçiliyse null.</summary>
    public Brush? HighlightBrush { get; private init; }

    public bool HasIndicatorBrush => IndicatorBrush is not null;

    public bool ShowBadges { get; private init; }
    public DockBadgeStyle BadgeStyle { get; private init; }
    public bool BadgeDotOnly => BadgeStyle == DockBadgeStyle.DotOnly;
    public Brush? BadgeBrush { get; private init; }
    public bool HasBadgeBrush => BadgeBrush is not null;
    public HorizontalAlignment BadgeHorizontal { get; private init; }
    public VerticalAlignment BadgeVertical { get; private init; }
    public Thickness BadgeMargin { get; private init; }
    public DockLabelMode LabelMode { get; private init; }

    public static DockPresentationStyle From(Koy.Core.Settings.DockSettings dock)
    {
        var indicator = SolidOrNull(dock.IndicatorColor, dock.IndicatorCustomColor, alpha: 0xFF);
        var highlight = SolidOrNull(dock.IndicatorColor, dock.IndicatorCustomColor, alpha: 0x40);

        var right = dock.BadgeCorner is DockBadgeCorner.TopRight or DockBadgeCorner.BottomRight;
        var top = dock.BadgeCorner is DockBadgeCorner.TopRight or DockBadgeCorner.TopLeft;

        return new DockPresentationStyle
        {
            IndicatorStyle = dock.IndicatorStyle,
            OnePerWindow = dock.IndicatorOnePerWindow,
            IndicatorBrush = indicator,
            HighlightBrush = highlight,
            ShowBadges = dock.ShowBadges,
            BadgeStyle = dock.BadgeStyle,
            BadgeBrush = SolidOrNull(dock.BadgeColor, dock.BadgeCustomColor, alpha: 0xFF),
            BadgeHorizontal = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            BadgeVertical = top ? VerticalAlignment.Top : VerticalAlignment.Bottom,
            // Alt köşede gösterge noktasının üstüne binmesin diye biraz yukarıda durur.
            BadgeMargin = new Thickness(right ? 0 : 2, top ? 2 : 0, right ? 2 : 0, top ? 0 : 6),
            LabelMode = dock.LabelMode
        };
    }

    // Efektif kaynak Core'da çözülür (geçersiz özel renk → vurgu). Vurgu = null.
    private static SolidColorBrush? SolidOrNull(DockColorSource source, string? customHex, byte alpha)
    {
        var effective = DockPresentationMath.EffectiveColorSource(source, customHex);
        if (effective == DockColorSource.Accent)
            return null;

        var color = effective == DockColorSource.Neutral
            ? NeutralColor
            : (Color)ColorConverter.ConvertFromString(customHex!);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
