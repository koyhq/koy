using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Koy.Core.Display;
using Koy.Core.Notch;
using Koy.Platform.Shell;

namespace Koy.App.Features.Notch;

/// <summary>
/// Ekranın üst kenarı boyunca ince çizgi (renk ve kalınlık ayardan) (<see cref="NotchEdgeLine"/>). Tıklama geçirir, odak çalmaz;
/// çentik penceresinin hemen altında durur, böylece çentik ortasını örter ve gizlenince çizginin içine çekilir.
/// Animasyonu ve zamanlayıcısı yoktur: yalnız yerleşim ya da tema değişince bir kez çizilir.
/// </summary>
public sealed class NotchLineWindow : Window
{
    private readonly int _thickness;
    private DisplayMonitor _monitor;
    private nint _hwnd;

    public NotchLineWindow(DisplayMonitor monitor, NotchEdgeLineColor color, int thicknessDip)
    {
        _monitor = monitor;
        _thickness = NotchEdgeLine.ClampThickness(thicknessDip);
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        Title = "Koy";
        Left = monitor.LeftDip;
        Top = monitor.TopDip;
        Width = Math.Max(1, monitor.WidthDip);
        Height = _thickness;

        var line = new Border();
        // Çentik zemini ya da vurgu rengi; DynamicResource tema, vurgu ya da stil dosyası değişince çizgiyi kendisi günceller.
        line.SetResourceReference(Border.BackgroundProperty, NotchEdgeLine.ResourceKey(color));
        Content = line;
    }

    public nint Handle => _hwnd;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.ApplyToolWindowAndNoActivate(_hwnd);
        WindowStyles.SetClickThrough(_hwnd, true);
        Place(_monitor);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Place(_monitor);
    }

    /// <summary>Çizgiyi hedef monitörün üst kenarına piksel olarak yerleştirir (karışık DPI'da DIP yanıltır).</summary>
    public void Place(DisplayMonitor monitor)
    {
        _monitor = monitor;
        if (_hwnd == 0)
            return;

        // Pencere başka DPI'lı ekrandaysa önce yalnız taşınır; boyut DpiChanged'den sonra verilir (NotchWindow ile aynı).
        var sameDpi = Math.Abs(VisualTreeHelper.GetDpi(this).DpiScaleY - monitor.DpiScaleY) < 0.01;
        WindowStyles.SetBounds(_hwnd, NotchEdgeLine.Bounds(monitor, _thickness), resize: sameDpi);
    }

    /// <summary>Çizgiyi yeniden en üst katmana alır; ardından çentik kendi üstüne çıkınca çizgi onun altında kalır.</summary>
    public void ReassertTopmost()
    {
        if (_hwnd != 0)
            WindowStyles.ReassertTopmost(_hwnd);
    }

    public void SetLineVisible(bool visible)
    {
        if (visible && !IsVisible)
            Show();
        else if (!visible && IsVisible)
            Hide();
    }
}
