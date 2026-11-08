using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// Aday pencere bilgisi (çoklu ekranda Z-sırasındaki açık pencereleri filtrelemek için).
/// </summary>
public readonly record struct DockCandidateWindow(
    nint Handle,
    WindowRect Bounds,
    nint MonitorHandle,
    bool IsMaximized = false,
    bool IsMinimized = false);

/// <summary>
/// Ön plandaki pencerenin hangi ekranın dock'unu etkileyeceğine karar verir.
/// Pencere yalnız bulunduğu ekranın (Windows'un MonitorFromWindow ile seçtiği, alanın çoğunu kaplayan) dock'unu etkiler.
/// Büyütülmüş pencere kenarlardan birkaç piksel taşar (ör. 0–1920 ekranda -8…1928); sınırlar bütün dock'lara verilince
/// bu taşma yan ekranın kenarına değiyor ve yan ekrandaki dock "pencere üstüme bindi" sanıp yapışık moda geçiyordu.
/// </summary>
public static class DockForegroundFilter
{
    /// <summary>
    /// Bu dock'a verilecek pencere sınırları: pencere başka ekrandaysa boş (çakışma yok).
    /// Pencerenin ekranı bilinmiyorsa (0) eski davranış korunur, sınırlar olduğu gibi verilir.
    /// </summary>
    public static WindowRect BoundsForDock(WindowRect windowBounds, nint windowMonitor, nint dockMonitor) =>
        AppliesToDock(windowMonitor, dockMonitor) ? windowBounds : WindowRect.Empty;

    /// <summary>Pencere bu dock'un ekranında mı (ekranı bilinmiyorsa evet sayılır).</summary>
    public static bool AppliesToDock(nint windowMonitor, nint dockMonitor) =>
        windowMonitor == 0 || windowMonitor == dockMonitor;

    /// <summary>
    /// Çoklu ekranda ön plan penceresi başka bir ekrandaysa, bu dock'un kendi ekranındaki en üst
    /// görünür ve küçültülmemiş penceresini seçer. Adaylar Z-sırasına (üstten alta) göre verilmelidir.
    /// Uygun pencere bulunamazsa boş sınır döner.
    /// </summary>
    public static (WindowRect Bounds, bool IsMaximized, nint Handle) ResolveMonitorWindow(
        nint dockMonitor,
        WindowInfo foreground,
        nint foregroundMonitor,
        bool foregroundMaximized,
        IEnumerable<DockCandidateWindow>? candidates)
    {
        if (AppliesToDock(foregroundMonitor, dockMonitor))
            return (foreground.Bounds, foregroundMaximized, foreground.Handle);

        var top = FindTopWindowForMonitor(dockMonitor, candidates);
        if (top is { } c)
            return (c.Bounds, c.IsMaximized, c.Handle);

        return (WindowRect.Empty, false, 0);
    }

    /// <summary>
    /// Verilen aday pencereler arasından bu monitördeki en üst görünür/küçültülmemiş pencereyi bulur.
    /// Adaylar Z-sırasına göre sıralı olmalıdır.
    /// </summary>
    public static DockCandidateWindow? FindTopWindowForMonitor(
        nint dockMonitor,
        IEnumerable<DockCandidateWindow>? candidates)
    {
        if (candidates is null)
            return null;

        foreach (var c in candidates)
        {
            if (!c.IsMinimized && c.MonitorHandle == dockMonitor)
                return c;
        }

        return null;
    }
}
