using System.Windows;
using System.Windows.Controls;

namespace Koy.App.Features.Dock;

/// <summary>Tam genişlik dock'un pencere öğeleri (hava durumu) düğmesi. Çizim XAML'de; <see cref="IsCompact"/> yalnız sıcaklığı bırakır.</summary>
public partial class DockWeatherWidget : UserControl
{
    /// <summary>Sol hizada uygulamaların sağındaki biçim: kısa açıklama gizlenir (simge ve sıcaklık).</summary>
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(DockWeatherWidget),
        new PropertyMetadata(false, (d, e) => ((DockWeatherWidget)d).DescriptionText.Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible));

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public DockWeatherWidget()
    {
        InitializeComponent();
    }
}
