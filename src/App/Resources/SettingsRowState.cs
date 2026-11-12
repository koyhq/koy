using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Koy.App.Resources;

/// <summary>
/// Ayar satırının arama ve "yalnız değişenler" durumu (issue #69). Ekli özellik olarak satır kenarlığına
/// (SettingRowStyle) ve bölüm başlığına (SectionHeaderStyle) konur; stil tetikleyicileri görünümü değiştirir.
/// Stil tetikleyicisi yerel değerin altında kaldığı için kendi Visibility bağı olan satırlar onu korur.
/// </summary>
public static class SettingsRowState
{
    /// <summary>Aramadan gelindi: satır kısa süre vurgulu kenarlıkla görünür.</summary>
    public static readonly DependencyProperty IsHighlightedProperty = DependencyProperty.RegisterAttached(
        "IsHighlighted", typeof(bool), typeof(SettingsRowState), new PropertyMetadata(false));

    public static bool GetIsHighlighted(DependencyObject o) => (bool)o.GetValue(IsHighlightedProperty);
    public static void SetIsHighlighted(DependencyObject o, bool v) => o.SetValue(IsHighlightedProperty, v);

    /// <summary>"Yalnız değişenleri göster" açık ve satır varsayılanda: gizlenir.</summary>
    public static readonly DependencyProperty IsFilteredOutProperty = DependencyProperty.RegisterAttached(
        "IsFilteredOut", typeof(bool), typeof(SettingsRowState), new PropertyMetadata(false));

    public static bool GetIsFilteredOut(DependencyObject o) => (bool)o.GetValue(IsFilteredOutProperty);
    public static void SetIsFilteredOut(DependencyObject o, bool v) => o.SetValue(IsFilteredOutProperty, v);

    /// <summary>Satırın ayarı varsayılandan farklı: solda küçük vurgu noktası (süsleyici).</summary>
    public static readonly DependencyProperty IsChangedProperty = DependencyProperty.RegisterAttached(
        "IsChanged", typeof(bool), typeof(SettingsRowState), new PropertyMetadata(false, OnIsChangedChanged));

    public static bool GetIsChanged(DependencyObject o) => (bool)o.GetValue(IsChangedProperty);
    public static void SetIsChanged(DependencyObject o, bool v) => o.SetValue(IsChangedProperty, v);

    private static void OnIsChangedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el)
            return;

        // Satır henüz çizilmemişse (gizli sayfa) süsleyici katmanı yoktur; yüklenince yeniden denenir.
        if (!TryApplyDot(el, (bool)e.NewValue) && el is FrameworkElement fe && (bool)e.NewValue)
        {
            fe.Loaded -= OnRowLoaded;
            fe.Loaded += OnRowLoaded;
        }
    }

    private static void OnRowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            fe.Loaded -= OnRowLoaded;
            TryApplyDot(fe, GetIsChanged(fe));
        }
    }

    private static bool TryApplyDot(UIElement el, bool show)
    {
        var layer = AdornerLayer.GetAdornerLayer(el);
        if (layer is null)
            return false;

        var existing = layer.GetAdorners(el)?.OfType<ChangedDotAdorner>().FirstOrDefault();
        if (show && existing is null)
            layer.Add(new ChangedDotAdorner(el));
        else if (!show && existing is not null)
            layer.Remove(existing);
        return true;
    }

    /// <summary>
    /// Satırın sol iç boşluğunda (14 DIP), dikey ortada 6 DIP vurgu noktası. Kaydırma alanının süsleyici katmanı
    /// satırın dışını kırptığı için nokta satırın içinde çizilir. İsabet testine katılmaz.
    /// </summary>
    private sealed class ChangedDotAdorner : Adorner
    {
        public ChangedDotAdorner(UIElement adorned) : base(adorned) => IsHitTestVisible = false;

        protected override void OnRender(DrawingContext dc)
        {
            var brush = Application.Current?.TryFindResource("KoyAccent") as Brush ?? Brushes.DodgerBlue;
            var height = AdornedElement.RenderSize.Height;
            dc.DrawEllipse(brush, null, new Point(6, height / 2), 3, 3);
        }
    }
}
