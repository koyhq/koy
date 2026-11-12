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

    private static readonly DependencyProperty ActiveAdornerProperty = DependencyProperty.RegisterAttached(
        "ActiveAdorner", typeof(ChangedDotAdorner), typeof(SettingsRowState));

    private static ChangedDotAdorner? GetActiveAdorner(UIElement el) =>
        (ChangedDotAdorner?)el.GetValue(ActiveAdornerProperty);

    private static void SetActiveAdorner(UIElement el, ChangedDotAdorner? v)
    {
        if (v is null)
            el.ClearValue(ActiveAdornerProperty);
        else
            el.SetValue(ActiveAdornerProperty, v);
    }

    private static void OnIsChangedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el)
            return;

        var show = (bool)e.NewValue;
        var fe = el as FrameworkElement;
        // Satır henüz çizilmemişse süsleyici katmanı henüz oluşmamış olabilir; yüklenince yeniden denenir.
        if (!TryApplyDot(el, show))
        {
            if (fe is not null)
            {
                fe.Loaded -= OnRowLoaded;
                if (show)
                    fe.Loaded += OnRowLoaded;
            }
        }
        else if (!show && fe is not null)
        {
            fe.Loaded -= OnRowLoaded;
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
        if (!show)
        {
            if (el is FrameworkElement fe)
                fe.Loaded -= OnRowLoaded;

            var active = GetActiveAdorner(el);
            if (active is not null)
            {
                active.Detach();
                SetActiveAdorner(el, null);
            }

            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer is not null)
            {
                var adorners = layer.GetAdorners(el);
                if (adorners is not null)
                {
                    foreach (var dot in adorners.OfType<ChangedDotAdorner>())
                    {
                        dot.Detach();
                        layer.Remove(dot);
                    }
                }
            }

            return true;
        }

        var current = GetActiveAdorner(el);
        if (current is not null)
        {
            current.UpdateState();
            return true;
        }

        var currentLayer = AdornerLayer.GetAdornerLayer(el);
        if (currentLayer is null)
            return false;

        var existing = currentLayer.GetAdorners(el)?.OfType<ChangedDotAdorner>().FirstOrDefault();
        if (existing is not null)
        {
            SetActiveAdorner(el, existing);
            existing.UpdateState();
            return true;
        }

        var newAdorner = new ChangedDotAdorner(el, currentLayer);
        SetActiveAdorner(el, newAdorner);
        newAdorner.UpdateState();
        return true;
    }

    /// <summary>
    /// Satırın sol iç boşluğunda (14 DIP), dikey ortada 6 DIP vurgu noktası. Kaydırma alanının süsleyici katmanı
    /// satırın dışını kırptığı için nokta satırın içinde çizilir. İsabet testine katılmaz.
    /// Sayfa değişimlerinde veya gizlendiğinde katmandan çıkarılır; askıda veya boşlukta çizilmez.
    /// </summary>
    private sealed class ChangedDotAdorner : Adorner
    {
        private AdornerLayer? _layer;

        public ChangedDotAdorner(UIElement adorned, AdornerLayer? layer = null) : base(adorned)
        {
            IsHitTestVisible = false;
            _layer = layer ?? AdornerLayer.GetAdornerLayer(adorned);

            AdornedElement.IsVisibleChanged += OnAdornedElementIsVisibleChanged;
            if (AdornedElement is FrameworkElement fe)
            {
                fe.Unloaded += OnAdornedElementUnloaded;
                fe.Loaded += OnAdornedElementLoaded;
            }
        }

        public void AttachToLayer(AdornerLayer? targetLayer = null)
        {
            var layer = targetLayer ?? AdornerLayer.GetAdornerLayer(AdornedElement) ?? _layer;
            if (layer is null)
                return;

            _layer = layer;
            var current = layer.GetAdorners(AdornedElement);
            if (current is null || Array.IndexOf(current, this) < 0)
            {
                layer.Add(this);
            }
        }

        public void RemoveFromLayer()
        {
            var layer = _layer ?? Parent as AdornerLayer ?? AdornerLayer.GetAdornerLayer(AdornedElement);
            if (layer is not null)
            {
                layer.Remove(this);
            }
        }

        public void UpdateState()
        {
            if (!GetIsChanged(AdornedElement))
            {
                Detach();
                SetActiveAdorner(AdornedElement, null);
                return;
            }

            if (AdornedElement.IsVisible)
            {
                AttachToLayer();
                Visibility = Visibility.Visible;
                InvalidateVisual();
            }
            else
            {
                Visibility = Visibility.Collapsed;
                RemoveFromLayer();
            }
        }

        private void OnAdornedElementIsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e) =>
            UpdateState();

        private void OnAdornedElementUnloaded(object? sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            RemoveFromLayer();
        }

        private void OnAdornedElementLoaded(object? sender, RoutedEventArgs e) =>
            UpdateState();

        public void Detach()
        {
            Visibility = Visibility.Collapsed;
            RemoveFromLayer();
            AdornedElement.IsVisibleChanged -= OnAdornedElementIsVisibleChanged;
            if (AdornedElement is FrameworkElement fe)
            {
                fe.Unloaded -= OnAdornedElementUnloaded;
                fe.Loaded -= OnAdornedElementLoaded;
            }
            _layer = null;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (!AdornedElement.IsVisible || AdornedElement.RenderSize.Height < 4)
                return;

            var brush = Application.Current?.TryFindResource("KoyAccent") as Brush ?? Brushes.DodgerBlue;
            var height = AdornedElement.RenderSize.Height;
            dc.DrawEllipse(brush, null, new Point(6, height / 2), 3, 3);
        }
    }
}
