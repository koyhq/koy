using System.Globalization;
using System.Windows.Data;
using Koy.Core.SystemStats;

namespace Koy.App.Resources;

/// <summary>Sayıyı uygulama diline uygun yüzde metnine çevirir ("%42", "42%").</summary>
public sealed class PercentTextConverter : IValueConverter
{
    public static readonly PercentTextConverter Instance = new();

    // Bağlamanın verdiği kültür XAML dilidir (en-US); uygulamanın dili CurrentCulture'dadır.
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        SystemFormat.Percent(System.Convert.ToDouble(value, CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
