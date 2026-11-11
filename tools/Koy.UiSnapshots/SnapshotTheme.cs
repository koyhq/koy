using System.Windows;
using Koy.App.Resources;
using Koy.Core.Settings;

namespace Koy.UiSnapshots;

/// <summary>Uygulamanın kendi tema yöneticisini kullanır; böylece görüntüler gerçek kaynaklarla çizilir.</summary>
internal static class SnapshotTheme
{
    public static void Install(Application app)
    {
        ThemeManager.Install(app);
        app.Resources["NotchBackground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xF0, 0, 0, 0));
        app.Resources["NotchForeground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
    }

    public static void Apply(Application app, string theme)
    {
        ThemeManager.Apply(app, new AppSettings { Appearance = new AppearanceSettings { Theme = theme == "light" ? Koy.Core.Appearance.AppTheme.Light : Koy.Core.Appearance.AppTheme.Dark, Material = Program.Material } }, systemUsesLightOverride: false);
    }
}
