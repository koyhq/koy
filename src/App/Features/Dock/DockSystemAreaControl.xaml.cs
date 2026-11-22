using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Koy.App.Features.Dock.Tray;
using Koy.Core.Shell;
using Koy.Core.Tray;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock;

/// <summary>
/// Tam genişlik dock'un sağ ucundaki sistem alanı (Windows 11 görev çubuğu gibi). Çizim XAML'de; burada yalnız fare olayları:
/// görünür tepsi simgelerinin tıklaması (uygulamaya Windows'un göndereceği mesajlar), ses tekerleği ve masaüstünü göster şeridi.
/// </summary>
public partial class DockSystemAreaControl : UserControl
{
    private bool _isDoubleClick;

    public DockSystemAreaControl()
    {
        InitializeComponent();
    }

    private DockViewModel? ViewModel => DataContext as DockViewModel;

    // Menü çıpası dock'un üzerine düşmesin diye dock penceresinin ekran dikdörtgeni (fiziksel piksel).
    private WindowRect GetDockRect()
    {
        var window = Window.GetWindow(this);
        return window is not null && PopupMenuProbe.TryGetWindowRect(new WindowInteropHelper(window).Handle, out var rect)
            ? rect
            : WindowRect.Empty;
    }

    private void OnTrayIconLeftDown(object sender, MouseButtonEventArgs e)
    {
        // Çift tıklama bırakmada gönderilir: Windows görev çubuğu da DBLCLK ardından LBUTTONUP yollar.
        _isDoubleClick = e.ClickCount >= 2;
        e.Handled = true;
    }

    private void OnTrayIconLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TrayIconViewModel icon })
            ViewModel?.SendTrayMouse(icon, _isDoubleClick ? TrayMouseAction.LeftDoubleClick : TrayMouseAction.LeftClick, GetDockRect());

        _isDoubleClick = false;
        e.Handled = true;
    }

    private void OnTrayIconRightUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TrayIconViewModel icon })
            ViewModel?.SendTrayMouse(icon, TrayMouseAction.RightClick, GetDockRect());

        e.Handled = true;
    }

    /// <summary>Ses simgesi üstünde tekerlek: Windows görev çubuğundaki gibi %2 adım.</summary>
    private void OnVolumeWheel(object sender, MouseWheelEventArgs e)
    {
        ViewModel?.Status?.AdjustVolume(e.Delta);
        e.Handled = true;
    }

    private void OnDesktopStripClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.Status?.ShowDesktopCommand.Execute(null);
        e.Handled = true;
    }
}
