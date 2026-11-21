using System.Windows;
using System.Windows.Media;
using Koy.Core.Appearance;

namespace Koy.App.Resources;

/// <summary>
/// Yüksek Karşıtlık (SystemParameters.HighContrast): çentik token'ları sistem renklerine bağlanır
/// (<see cref="NotchHighContrastMap"/>). Çentik normalde sabit koyu paletlidir; bu kipte yüzey Window, yazı ve kenar
/// WindowText olur. Yalnız kip açıkken override sözlüğüne yazılır, kapalıyken hiçbir şey eklenmez.
/// </summary>
internal static class NotchHighContrast
{
    public static bool IsActive => SystemParameters.HighContrast;

    public static void Apply(ResourceDictionary overrides)
    {
        if (!IsActive)
            return;

        foreach (var (key, role) in NotchHighContrastMap.Entries)
            overrides[key] = Frozen(ColorFor(role));
    }

    private static Color ColorFor(HighContrastRole role) => role switch
    {
        HighContrastRole.Background => SystemColors.WindowColor,
        HighContrastRole.Highlight => SystemColors.HighlightColor,
        HighContrastRole.HighlightText => SystemColors.HighlightTextColor,
        HighContrastRole.Track => SystemColors.GrayTextColor,
        _ => SystemColors.WindowTextColor
    };

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
