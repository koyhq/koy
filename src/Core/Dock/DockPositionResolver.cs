using Koy.Core.Display;
using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// Dock'un konum ve geometrik durum sonucu.
/// </summary>
public sealed record DockResolvedState(
    double LeftDip,
    double TopDip,
    double WidthDip,
    double HeightDip,
    double CornerRadiusTopLeft,
    double CornerRadiusTopRight,
    double CornerRadiusBottomRight,
    double CornerRadiusBottomLeft,
    double TranslationY,
    double Opacity,
    bool IsSnapped,
    bool IsHandleVisible,
    bool IsHidden,
    bool IsFullWidth,
    int AppBarReservedHeightDip)
{
    public bool HasUniformRadius =>
        CornerRadiusTopLeft.Equals(CornerRadiusTopRight) &&
        CornerRadiusTopRight.Equals(CornerRadiusBottomRight) &&
        CornerRadiusBottomRight.Equals(CornerRadiusBottomLeft);
}

/// <summary>
/// Dock'un ekran üzerindeki konumunu, boyutlarını, köşe yuvarlaklıklarını ve görünürlük durumunu
/// pencere yakınlığı (DockDown, ReserveSpace, Dodge, AlwaysHidden) ve dock stiline (FloatingCapsule, FullWidth)
/// göre hesaplayan saf mantık çözücüsü (Core).
/// </summary>
public static class DockPositionResolver
{
    public const double FloatingHeightDip = 60.0;
    public const double FloatingBottomMarginDip = 10.0;
    public const double FloatingCornerRadiusDip = 18.0;

    public const double SnappedHeightDip = 46.0;
    public const double SnappedCornerRadiusDip = 14.0;

    /// <summary>
    /// Tam genişlik çubuğun yüksekliği: Windows 11 görev çubuğu ölçüsü. Dock penceresi, AppBar payı, XAML ve gizlenme
    /// kayması hep bu sabitten okur; başka yerde 48 yazılmaz.
    /// </summary>
    public const double FullWidthHeightDip = 48.0;
    public const double DodgeHandleWidthDip = 120.0;
    public const double DodgeHandleHeightDip = 4.0;
    public const double DodgeHandleBottomMarginDip = 4.0;

    /// <summary>
    /// Pencerenin dock alanına yaklaştığını tespit etmek için kullanılan eşik mesafe (DIP).
    /// Dock'un alt kenardan yüksekliği 60 + 10 = 70 px olduğu için 80 px eşiği kullanılır.
    /// </summary>
    public const double ProximityThresholdDip = 80.0;

    public static DockResolvedState Resolve(
        DisplayMonitor? monitor,
        DockStyleMode dockStyle,
        DockProximityMode proximityMode,
        double contentWidthDip,
        WindowRect foregroundWindowBoundsDip,
        bool isWindowMaximized = false,
        bool isMouseAtBottomEdge = false,
        bool isMouseOverDock = false,
        bool isWindowFullscreen = false,
        bool hideInFullscreen = true,
        bool edgeRevealEnabled = true)
    {
        // Gerçek tam ekran (oyun, F11, tam ekran video): dock HİÇBİR koşulda çıkmaz, kenar tetiği de yoktur
        // (hideInFullscreen kapalıysa kullanıcı dock'u tam ekranda da istiyor demektir, o zaman normal akış sürer).
        // Büyütülmüş pencerede ise dock gizlenir ama fare alt kenara gelince (edgeRevealEnabled) geri çıkar.
        var monitorLeft = monitor?.LeftDip ?? 0.0;
        var monitorTop = monitor?.TopDip ?? 0.0;
        var monitorWidth = monitor?.WidthDip > 0 ? monitor.WidthDip : 1920.0;
        var monitorHeight = monitor?.HeightDip > 0 ? monitor.HeightDip : 1080.0;

        var monitorBottom = monitorTop + monitorHeight;
        var monitorRight = monitorLeft + monitorWidth;

        // Yakınlık / Çakışma denetimi
        var proximityZoneTop = monitorBottom - ProximityThresholdDip;
        var windowIntersects = isWindowMaximized ||
            (!foregroundWindowBoundsDip.IsEmpty &&
             foregroundWindowBoundsDip.Bottom >= proximityZoneTop &&
             foregroundWindowBoundsDip.Top < monitorBottom &&
             foregroundWindowBoundsDip.Right > monitorLeft &&
             foregroundWindowBoundsDip.Left < monitorRight);

        // Tam Genişlik Modu (M6.3 / DESIGN.md)
        if (dockStyle == DockStyleMode.FullWidth)
        {
            var top = monitorBottom - FullWidthHeightDip;
            var reservedHeight = proximityMode == DockProximityMode.ReserveSpace ? (int)FullWidthHeightDip : 0;

            if (isWindowFullscreen && hideInFullscreen)
            {
                return new DockResolvedState(
                    LeftDip: monitorLeft,
                    TopDip: top,
                    WidthDip: monitorWidth,
                    HeightDip: FullWidthHeightDip,
                    CornerRadiusTopLeft: 0,
                    CornerRadiusTopRight: 0,
                    CornerRadiusBottomRight: 0,
                    CornerRadiusBottomLeft: 0,
                    TranslationY: FullWidthHeightDip + 10.0,
                    Opacity: 0.0,
                    IsSnapped: true,
                    IsHandleVisible: false,
                    IsHidden: true,
                    IsFullWidth: true,
                    AppBarReservedHeightDip: reservedHeight);
            }

            if (isWindowMaximized && hideInFullscreen && proximityMode != DockProximityMode.ReserveSpace)
            {
                var isHovered = (edgeRevealEnabled && isMouseAtBottomEdge) || isMouseOverDock;
                return new DockResolvedState(
                    LeftDip: monitorLeft,
                    TopDip: top,
                    WidthDip: monitorWidth,
                    HeightDip: FullWidthHeightDip,
                    CornerRadiusTopLeft: 0,
                    CornerRadiusTopRight: 0,
                    CornerRadiusBottomRight: 0,
                    CornerRadiusBottomLeft: 0,
                    TranslationY: isHovered ? 0 : (FullWidthHeightDip + 10.0),
                    Opacity: isHovered ? 1.0 : 0.0,
                    IsSnapped: true,
                    IsHandleVisible: false,
                    IsHidden: !isHovered,
                    IsFullWidth: true,
                    AppBarReservedHeightDip: 0);
            }

            switch (proximityMode)
            {
                case DockProximityMode.Dodge:
                {
                    if (windowIntersects)
                    {
                        var isHovered = isMouseAtBottomEdge || isMouseOverDock;
                        return new DockResolvedState(
                            LeftDip: monitorLeft,
                            TopDip: top,
                            WidthDip: monitorWidth,
                            HeightDip: FullWidthHeightDip,
                            CornerRadiusTopLeft: 0,
                            CornerRadiusTopRight: 0,
                            CornerRadiusBottomRight: 0,
                            CornerRadiusBottomLeft: 0,
                            TranslationY: isHovered ? 0 : (FullWidthHeightDip + 10.0),
                            Opacity: isHovered ? 1.0 : 0.0,
                            IsSnapped: true,
                            IsHandleVisible: false,
                            IsHidden: !isHovered,
                            IsFullWidth: true,
                            AppBarReservedHeightDip: 0);
                    }
                    else
                    {
                        return new DockResolvedState(
                            LeftDip: monitorLeft,
                            TopDip: top,
                            WidthDip: monitorWidth,
                            HeightDip: FullWidthHeightDip,
                            CornerRadiusTopLeft: 0,
                            CornerRadiusTopRight: 0,
                            CornerRadiusBottomRight: 0,
                            CornerRadiusBottomLeft: 0,
                            TranslationY: 0,
                            Opacity: 1.0,
                            IsSnapped: true,
                            IsHandleVisible: false,
                            IsHidden: false,
                            IsFullWidth: true,
                            AppBarReservedHeightDip: 0);
                    }
                }

                case DockProximityMode.AlwaysHidden:
                {
                    var isHovered = isMouseAtBottomEdge || isMouseOverDock;
                    return new DockResolvedState(
                        LeftDip: monitorLeft,
                        TopDip: top,
                        WidthDip: monitorWidth,
                        HeightDip: FullWidthHeightDip,
                        CornerRadiusTopLeft: 0,
                        CornerRadiusTopRight: 0,
                        CornerRadiusBottomRight: 0,
                        CornerRadiusBottomLeft: 0,
                        TranslationY: isHovered ? 0 : (FullWidthHeightDip + 10.0),
                        Opacity: isHovered ? 1.0 : 0.0,
                        IsSnapped: true,
                        IsHandleVisible: false,
                        IsHidden: !isHovered,
                        IsFullWidth: true,
                        AppBarReservedHeightDip: 0);
                }

                default: // DockDown, ReserveSpace
                    return new DockResolvedState(
                        LeftDip: monitorLeft,
                        TopDip: top,
                        WidthDip: monitorWidth,
                        HeightDip: FullWidthHeightDip,
                        CornerRadiusTopLeft: 0,
                        CornerRadiusTopRight: 0,
                        CornerRadiusBottomRight: 0,
                        CornerRadiusBottomLeft: 0,
                        TranslationY: 0,
                        Opacity: 1.0,
                        IsSnapped: true,
                        IsHandleVisible: false,
                        IsHidden: false,
                        IsFullWidth: true,
                        AppBarReservedHeightDip: reservedHeight);
            }
        }

        // Yüzen Kapsül Modu (FloatingCapsule)
        var capsuleWidth = Math.Max(contentWidthDip > 0 ? contentWidthDip : 520.0, 160.0);
        var capsuleLeft = monitorLeft + Math.Max(0.0, (monitorWidth - capsuleWidth) / 2.0);

        if (isWindowFullscreen && hideInFullscreen)
        {
            var top = monitorBottom - FloatingHeightDip - FloatingBottomMarginDip;
            return new DockResolvedState(
                LeftDip: capsuleLeft,
                TopDip: top,
                WidthDip: capsuleWidth,
                HeightDip: FloatingHeightDip,
                CornerRadiusTopLeft: FloatingCornerRadiusDip,
                CornerRadiusTopRight: FloatingCornerRadiusDip,
                CornerRadiusBottomRight: FloatingCornerRadiusDip,
                CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                TranslationY: FloatingHeightDip + FloatingBottomMarginDip + 10.0,
                Opacity: 0.0,
                IsSnapped: false,
                IsHandleVisible: false,
                IsHidden: true,
                IsFullWidth: false,
                AppBarReservedHeightDip: 0);
        }

        // Aşağı yapış'ta büyütülmüş pencere dock'u gizlemez: dock alta yapışık kalır (aşağıdaki DockDown kolu), yer ayırmaz.
        // Yalnız gerçek tam ekran (yukarıdaki dal) gizler. Çekil ve Hep gizli büyütülmüş pencerede gizlemeyi sürdürür.
        if (isWindowMaximized && hideInFullscreen
            && proximityMode is not (DockProximityMode.ReserveSpace or DockProximityMode.DockDown))
        {
            var top = monitorBottom - FloatingHeightDip - FloatingBottomMarginDip;
            var isHovered = (edgeRevealEnabled && isMouseAtBottomEdge) || isMouseOverDock;
            return new DockResolvedState(
                LeftDip: capsuleLeft,
                TopDip: top,
                WidthDip: capsuleWidth,
                HeightDip: FloatingHeightDip,
                CornerRadiusTopLeft: FloatingCornerRadiusDip,
                CornerRadiusTopRight: FloatingCornerRadiusDip,
                CornerRadiusBottomRight: FloatingCornerRadiusDip,
                CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                TranslationY: isHovered ? 0 : (FloatingHeightDip + FloatingBottomMarginDip + 10.0),
                Opacity: isHovered ? 1.0 : 0.0,
                IsSnapped: false,
                IsHandleVisible: !isHovered,
                IsHidden: !isHovered,
                IsFullWidth: false,
                AppBarReservedHeightDip: 0);
        }

        switch (proximityMode)
        {
            case DockProximityMode.DockDown:
            {
                if (windowIntersects)
                {
                    // Aşağı yapış: Alt kenara oturur, 60 px -> 46 px incelir, yalnız üst köşeler 14 px yuvarlak
                    var top = monitorBottom - SnappedHeightDip;
                    return new DockResolvedState(
                        LeftDip: capsuleLeft,
                        TopDip: top,
                        WidthDip: capsuleWidth,
                        HeightDip: SnappedHeightDip,
                        CornerRadiusTopLeft: SnappedCornerRadiusDip,
                        CornerRadiusTopRight: SnappedCornerRadiusDip,
                        CornerRadiusBottomRight: 0,
                        CornerRadiusBottomLeft: 0,
                        TranslationY: 0,
                        Opacity: 1.0,
                        IsSnapped: true,
                        IsHandleVisible: false,
                        IsHidden: false,
                        IsFullWidth: false,
                        AppBarReservedHeightDip: 0);
                }
                else
                {
                    // Boşta / Pencere uzakta: Yüzen kapsül (60 px, alttan 10 px, 18 px yarıçap)
                    var top = monitorBottom - FloatingHeightDip - FloatingBottomMarginDip;
                    return new DockResolvedState(
                        LeftDip: capsuleLeft,
                        TopDip: top,
                        WidthDip: capsuleWidth,
                        HeightDip: FloatingHeightDip,
                        CornerRadiusTopLeft: FloatingCornerRadiusDip,
                        CornerRadiusTopRight: FloatingCornerRadiusDip,
                        CornerRadiusBottomRight: FloatingCornerRadiusDip,
                        CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                        TranslationY: 0,
                        Opacity: 1.0,
                        IsSnapped: false,
                        IsHandleVisible: false,
                        IsHidden: false,
                        IsFullWidth: false,
                        AppBarReservedHeightDip: 0);
                }
            }

            case DockProximityMode.ReserveSpace:
            {
                // Yer ayır: Masaüstü çalışma alanından (AppBar) 46 px ayırır, dock alta oturur
                var top = monitorBottom - SnappedHeightDip;
                return new DockResolvedState(
                    LeftDip: capsuleLeft,
                    TopDip: top,
                    WidthDip: capsuleWidth,
                    HeightDip: SnappedHeightDip,
                    CornerRadiusTopLeft: SnappedCornerRadiusDip,
                    CornerRadiusTopRight: SnappedCornerRadiusDip,
                    CornerRadiusBottomRight: 0,
                    CornerRadiusBottomLeft: 0,
                    TranslationY: 0,
                    Opacity: 1.0,
                    IsSnapped: true,
                    IsHandleVisible: false,
                    IsHidden: false,
                    IsFullWidth: false,
                    AppBarReservedHeightDip: (int)SnappedHeightDip);
            }

            case DockProximityMode.Dodge:
            {
                var top = monitorBottom - FloatingHeightDip - FloatingBottomMarginDip;

                if (windowIntersects)
                {
                    var isHovered = isMouseAtBottomEdge || isMouseOverDock;
                    if (isHovered)
                    {
                        // Fare alt kenara geldiğinde dock yukarı kayarak geri çıkar
                        return new DockResolvedState(
                            LeftDip: capsuleLeft,
                            TopDip: top,
                            WidthDip: capsuleWidth,
                            HeightDip: FloatingHeightDip,
                            CornerRadiusTopLeft: FloatingCornerRadiusDip,
                            CornerRadiusTopRight: FloatingCornerRadiusDip,
                            CornerRadiusBottomRight: FloatingCornerRadiusDip,
                            CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                            TranslationY: 0,
                            Opacity: 1.0,
                            IsSnapped: false,
                            IsHandleVisible: false,
                            IsHidden: false,
                            IsFullWidth: false,
                            AppBarReservedHeightDip: 0);
                    }
                    else
                    {
                        // Pencere üstüne binince dock çekilir, altta 120×4 tutamak kalır
                        return new DockResolvedState(
                            LeftDip: capsuleLeft,
                            TopDip: top,
                            WidthDip: capsuleWidth,
                            HeightDip: FloatingHeightDip,
                            CornerRadiusTopLeft: FloatingCornerRadiusDip,
                            CornerRadiusTopRight: FloatingCornerRadiusDip,
                            CornerRadiusBottomRight: FloatingCornerRadiusDip,
                            CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                            TranslationY: FloatingHeightDip + FloatingBottomMarginDip + 10.0,
                            Opacity: 0.0,
                            IsSnapped: false,
                            IsHandleVisible: true,
                            IsHidden: true,
                            IsFullWidth: false,
                            AppBarReservedHeightDip: 0);
                    }
                }
                else
                {
                    // Çakışan pencere yok, normal yüzer
                    return new DockResolvedState(
                        LeftDip: capsuleLeft,
                        TopDip: top,
                        WidthDip: capsuleWidth,
                        HeightDip: FloatingHeightDip,
                        CornerRadiusTopLeft: FloatingCornerRadiusDip,
                        CornerRadiusTopRight: FloatingCornerRadiusDip,
                        CornerRadiusBottomRight: FloatingCornerRadiusDip,
                        CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                        TranslationY: 0,
                        Opacity: 1.0,
                        IsSnapped: false,
                        IsHandleVisible: false,
                        IsHidden: false,
                        IsFullWidth: false,
                        AppBarReservedHeightDip: 0);
                }
            }

            case DockProximityMode.AlwaysHidden:
            {
                var top = monitorBottom - FloatingHeightDip - FloatingBottomMarginDip;
                var isHovered = isMouseAtBottomEdge || isMouseOverDock;

                return new DockResolvedState(
                    LeftDip: capsuleLeft,
                    TopDip: top,
                    WidthDip: capsuleWidth,
                    HeightDip: FloatingHeightDip,
                    CornerRadiusTopLeft: FloatingCornerRadiusDip,
                    CornerRadiusTopRight: FloatingCornerRadiusDip,
                    CornerRadiusBottomRight: FloatingCornerRadiusDip,
                    CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                    TranslationY: isHovered ? 0 : (FloatingHeightDip + FloatingBottomMarginDip + 10.0),
                    Opacity: isHovered ? 1.0 : 0.0,
                    IsSnapped: false,
                    IsHandleVisible: false,
                    IsHidden: !isHovered,
                    IsFullWidth: false,
                    AppBarReservedHeightDip: 0);
            }

            default:
                goto case DockProximityMode.DockDown;
        }
    }
}
