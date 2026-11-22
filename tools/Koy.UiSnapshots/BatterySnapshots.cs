using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Koy.App.Features.Status;
using Koy.Core.Power;

namespace Koy.UiSnapshots;

/// <summary>
/// Pil durumları (şarj, prizde tam, prizde şarj olmuyor, pilde, düşük, kritik, pil tasarrufu): dock ve çentik kareleri
/// <c>--scene pil</c> ile, ayrıca simgenin koyu ve açık zeminde yan yana, ipucu metinleriyle döküldüğü bir levha.
/// </summary>
internal static class BatterySnapshots
{
    /// <summary>Durum adı (dosya adı) ve sahte güç verisi.</summary>
    public static readonly (string Name, FakePower Power)[] States =
    [
        ("sarj-oluyor", new FakePower(64, charging: true, time: TimeSpan.FromMinutes(45))),
        ("prizde-tam", new FakePower(100, ac: true)),
        ("prizde-sarj-olmuyor", new FakePower(80, ac: true)),
        ("pilde", new FakePower(54, time: new TimeSpan(3, 25, 0))),
        ("dusuk", new FakePower(15, time: TimeSpan.FromMinutes(38))),
        ("kritik", new FakePower(8, time: TimeSpan.FromMinutes(12))),
        ("pil-tasarrufu", new FakePower(60, saver: true, time: new TimeSpan(4, 10, 0))),
    ];

    /// <summary>Simgeyi bütün durumlar için koyu ve açık zeminde, ipucu metniyle birlikte yan yana çizer.</summary>
    public static void RenderSheet(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        var root = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent };
        foreach (var (back, fore, sub) in new[]
        {
            (Color.FromRgb(0x20, 0x20, 0x20), Colors.White, Color.FromRgb(0xC8, 0xC8, 0xC8)),
            (Color.FromRgb(0xF3, 0xF3, 0xF3), Color.FromRgb(0x1A, 0x1A, 0x1A), Color.FromRgb(0x50, 0x50, 0x50))
        })
        {
            var column = new StackPanel { Background = new SolidColorBrush(back) };
            foreach (var (name, power) in States)
            {
                var display = power.Display();
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 8, 14, 8) };
                row.Children.Add(new BatteryIcon { Display = display, OutlineBrush = new SolidColorBrush(fore), Width = 22, Height = 12, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = display.PercentText, Foreground = new SolidColorBrush(fore), FontSize = 12, FontWeight = FontWeights.SemiBold, Width = 44, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = BatteryText.Tooltip(display), Foreground = new SolidColorBrush(sub), FontSize = 12, Width = 360, VerticalAlignment = VerticalAlignment.Center });
                column.Children.Add(row);
            }
            root.Children.Add(column);
        }

        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        root.Arrange(new Rect(root.DesiredSize));
        root.UpdateLayout();
        renderElement(root, root.DesiredSize.Width, root.DesiredSize.Height, Path.Combine(outDir, $"{prefix}-{theme}-pil-levha.png"));
    }
}
