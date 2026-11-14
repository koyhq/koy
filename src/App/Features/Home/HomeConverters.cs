using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Koy.App.Features.Home;

/// <summary>bool'u tersler; parametre "Visibility" ise sonucu Visible / Collapsed olarak verir.</summary>
public sealed class NotConverter : IValueConverter
{
    public static NotConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is not true;
        return parameter as string == "Visibility" || targetType == typeof(Visibility)
            ? (result ? Visibility.Visible : Visibility.Collapsed)
            : result;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Bütün girdiler true ise Visible, değilse Collapsed.</summary>
public sealed class AllTrueToVisibility : IMultiValueConverter
{
    public static AllTrueToVisibility Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.All(v => v is true) ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
