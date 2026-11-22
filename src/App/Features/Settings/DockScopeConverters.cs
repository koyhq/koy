using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Koy.Core.Dock;

namespace Koy.App.Features.Settings;

/// <summary>
/// Dock ayar satırının seçili stilde görünmesi: bağlama <c>DockIsFullWidth</c> (bool) verilir, parametre <see cref="Core.Settings.DockSettings"/>
/// özellik adıdır. Karar Core'daki <see cref="DockSettingsMatrix"/>'tedir; geçersiz satır gizlenir (gri bırakılmaz).
/// </summary>
public sealed class DockScopeVisibilityConverter : IValueConverter
{
    public static readonly DockScopeVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var style = value is true ? DockStyleMode.FullWidth : DockStyleMode.FloatingCapsule;
        if (parameter is not string setting)
            return Visibility.Collapsed;

        // "@FullWidth" / "@Capsule": ayar değil, doğrudan stil kapısı (Windows karşılığı olan türetilmiş anahtarlar, ör. otomatik gizle).
        if (setting.StartsWith('@'))
            return (setting == "@FullWidth") == (style == DockStyleMode.FullWidth) ? Visibility.Visible : Visibility.Collapsed;

        return DockSettingsMatrix.IsApplicable(setting, style) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
