using System.Windows;
using System.Windows.Media;
using Koy.Core.Appearance;

namespace Koy.App.Features.Dock;

/// <summary>
/// Tam genişlik dock'un sistem temasına uyması: bir kaynak sözlüğünde <c>KoyDock*</c> adlarını tema dosyalarındaki <c>KoyTaskbar*</c>
/// fırçalarıyla gölgeler (içerideki DynamicResource kullanımları metin, simge ve ayırıcıyı açık temada koyu renge çevirir).
/// Kapsülde (<paramref name="active"/> yanlış) gölge kaldırılır ve <c>KoyDock*</c> her temada koyu kalır.
/// </summary>
public static class TaskbarThemeResources
{
    public static void Apply(ResourceDictionary resources, bool active)
    {
        foreach (var (dockKey, _) in TaskbarTheme.Pairs)
            resources.Remove(dockKey);

        if (!active)
            return;

        foreach (var (dockKey, taskbarKey) in TaskbarTheme.Pairs)
        {
            if (Application.Current?.TryFindResource(taskbarKey) is SolidColorBrush brush)
            {
                var clone = brush.Clone();
                clone.Freeze();
                resources[dockKey] = clone;
            }
        }
    }
}
