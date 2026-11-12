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
    int AppBarReservedHeightDip,
    double TranslationX = 0.0)
{
    public bool HasUniformRadius =>
        CornerRadiusTopLeft.Equals(CornerRadiusTopRight) &&
        CornerRadiusTopRight.Equals(CornerRadiusBottomRight) &&
        CornerRadiusBottomRight.Equals(CornerRadiusBottomLeft);
}

/// <summary>
/// Dock'un ekran üzerindeki konumunu, boyutlarını, köşe yuvarlaklıklarını ve görünürlük durumunu
/// pencere yakınlığı (DockDown, ReserveSpace, Dodge, AlwaysHidden, AlwaysFloating) ve dock stiline (FloatingCapsule, FullWidth)
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
        bool edgeRevealEnabled = true,
        DockEdge edge = DockEdge.Bottom,
        int lengthPercent = DockLayoutLimits.DefaultLengthPercent,
        double edgeOffsetDip = DockLayoutLimits.DefaultEdgeOffsetDip,
        double innerPaddingDip = DockLayoutLimits.DefaultInnerPaddingDip)
    {
        if (!Enum.IsDefined(edge)) edge = DockEdge.Bottom;
        lengthPercent = DockLayoutLimits.ClampLengthPercent(lengthPercent);
        edgeOffsetDip = DockLayoutLimits.ClampEdgeOffsetDip(edgeOffsetDip);
        innerPaddingDip = DockLayoutLimits.ClampInnerPaddingDip(innerPaddingDip);

        // Gerçek tam ekran (oyun, F11, tam ekran video): dock HİÇBİR koşulda çıkmaz, kenar tetiği de yoktur
        // (hideInFullscreen kapalıysa kullanıcı dock'u tam ekranda da istiyor demektir, o zaman normal akış sürer).
        // Büyütülmüş pencerede ise dock gizlenir ama fare alt kenara gelince (edgeRevealEnabled) geri çıkar.
        var monitorLeft = monitor?.LeftDip ?? 0.0;
        var monitorTop = monitor?.TopDip ?? 0.0;
        var monitorWidth = monitor?.WidthDip > 0 ? monitor.WidthDip : 1920.0;
        var monitorHeight = monitor?.HeightDip > 0 ? monitor.HeightDip : 1080.0;

        var monitorBottom = monitorTop + monitorHeight;
        var monitorRight = monitorLeft + monitorWidth;

        // Yakınlık / Çakışma denetimi (seçili kenara göre ProximityThresholdDip şeridi)
        bool edgeIntersects = edge switch
        {
            DockEdge.Top => foregroundWindowBoundsDip.Top <= monitorTop + ProximityThresholdDip &&
                            foregroundWindowBoundsDip.Bottom > monitorTop &&
                            foregroundWindowBoundsDip.Right > monitorLeft &&
                            foregroundWindowBoundsDip.Left < monitorRight,
            DockEdge.Left => foregroundWindowBoundsDip.Left <= monitorLeft + ProximityThresholdDip &&
                             foregroundWindowBoundsDip.Right > monitorLeft &&
                             foregroundWindowBoundsDip.Bottom > monitorTop &&
                             foregroundWindowBoundsDip.Top < monitorBottom,
            DockEdge.Right => foregroundWindowBoundsDip.Right >= monitorRight - ProximityThresholdDip &&
                              foregroundWindowBoundsDip.Left < monitorRight &&
                              foregroundWindowBoundsDip.Bottom > monitorTop &&
                              foregroundWindowBoundsDip.Top < monitorBottom,
            _ => foregroundWindowBoundsDip.Bottom >= monitorBottom - ProximityThresholdDip &&
                 foregroundWindowBoundsDip.Top < monitorBottom &&
                 foregroundWindowBoundsDip.Right > monitorLeft &&
                 foregroundWindowBoundsDip.Left < monitorRight
        };

        var windowIntersects = isWindowMaximized || (!foregroundWindowBoundsDip.IsEmpty && edgeIntersects);

        (double tl, double tr, double br, double bl) SnappedCorners() => edge switch
        {
            DockEdge.Top => (0.0, 0.0, SnappedCornerRadiusDip, SnappedCornerRadiusDip),
            DockEdge.Left => (0.0, SnappedCornerRadiusDip, SnappedCornerRadiusDip, 0.0),
            DockEdge.Right => (SnappedCornerRadiusDip, 0.0, 0.0, SnappedCornerRadiusDip),
            _ => (SnappedCornerRadiusDip, SnappedCornerRadiusDip, 0.0, 0.0)
        };

        (double tx, double ty) Translation(bool isHidden, double thickness, double offset)
        {
            if (!isHidden) return (0.0, 0.0);
            var shift = thickness + offset + 10.0;
            return edge switch
            {
                DockEdge.Top => (0.0, -shift),
                DockEdge.Left => (-shift, 0.0),
                DockEdge.Right => (shift, 0.0),
                _ => (0.0, shift)
            };
        }

        // Tam Genişlik Modu (M6.3 / DESIGN.md)
        if (dockStyle == DockStyleMode.FullWidth)
        {
            var monitorWorkArea = new DockRectDip(monitorLeft, monitorTop, monitorWidth, monitorHeight);
            var layout = DockLayoutMath.Compute(edge, lengthPercent, edgeOffsetDip, innerPaddingDip, FullWidthHeightDip, monitorWorkArea);
            var outer = layout.Outer;
            var reservedHeight = proximityMode == DockProximityMode.ReserveSpace ? (int)FullWidthHeightDip : 0;

            if (isWindowFullscreen && hideInFullscreen)
            {
                var (tx, ty) = Translation(true, FullWidthHeightDip, edgeOffsetDip);
                return new DockResolvedState(
                    LeftDip: outer.Left,
                    TopDip: outer.Top,
                    WidthDip: outer.Width,
                    HeightDip: outer.Height,
                    CornerRadiusTopLeft: 0,
                    CornerRadiusTopRight: 0,
                    CornerRadiusBottomRight: 0,
                    CornerRadiusBottomLeft: 0,
                    TranslationY: ty,
                    Opacity: 0.0,
                    IsSnapped: true,
                    IsHandleVisible: false,
                    IsHidden: true,
                    IsFullWidth: true,
                    AppBarReservedHeightDip: reservedHeight,
                    TranslationX: tx);
            }

            if (isWindowMaximized && hideInFullscreen && proximityMode != DockProximityMode.ReserveSpace)
            {
                var isHovered = (edgeRevealEnabled && isMouseAtBottomEdge) || isMouseOverDock;
                var (tx, ty) = Translation(!isHovered, FullWidthHeightDip, edgeOffsetDip);
                return new DockResolvedState(
                    LeftDip: outer.Left,
                    TopDip: outer.Top,
                    WidthDip: outer.Width,
                    HeightDip: outer.Height,
                    CornerRadiusTopLeft: 0,
                    CornerRadiusTopRight: 0,
                    CornerRadiusBottomRight: 0,
                    CornerRadiusBottomLeft: 0,
                    TranslationY: ty,
                    Opacity: isHovered ? 1.0 : 0.0,
                    IsSnapped: true,
                    IsHandleVisible: false,
                    IsHidden: !isHovered,
                    IsFullWidth: true,
                    AppBarReservedHeightDip: 0,
                    TranslationX: tx);
            }

            switch (proximityMode)
            {
                case DockProximityMode.Dodge:
                {
                    if (windowIntersects)
                    {
                        var isHovered = isMouseAtBottomEdge || isMouseOverDock;
                        var (tx, ty) = Translation(!isHovered, FullWidthHeightDip, edgeOffsetDip);
                        return new DockResolvedState(
                            LeftDip: outer.Left,
                            TopDip: outer.Top,
                            WidthDip: outer.Width,
                            HeightDip: outer.Height,
                            CornerRadiusTopLeft: 0,
                            CornerRadiusTopRight: 0,
                            CornerRadiusBottomRight: 0,
                            CornerRadiusBottomLeft: 0,
                            TranslationY: ty,
                            Opacity: isHovered ? 1.0 : 0.0,
                            IsSnapped: true,
                            IsHandleVisible: false,
                            IsHidden: !isHovered,
                            IsFullWidth: true,
                            AppBarReservedHeightDip: 0,
                            TranslationX: tx);
                    }
                    else
                    {
                        return new DockResolvedState(
                            LeftDip: outer.Left,
                            TopDip: outer.Top,
                            WidthDip: outer.Width,
                            HeightDip: outer.Height,
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
                            AppBarReservedHeightDip: 0,
                            TranslationX: 0);
                    }
                }

                case DockProximityMode.AlwaysHidden:
                {
                    var isHovered = isMouseAtBottomEdge || isMouseOverDock;
                    var (tx, ty) = Translation(!isHovered, FullWidthHeightDip, edgeOffsetDip);
                    return new DockResolvedState(
                        LeftDip: outer.Left,
                        TopDip: outer.Top,
                        WidthDip: outer.Width,
                        HeightDip: outer.Height,
                        CornerRadiusTopLeft: 0,
                        CornerRadiusTopRight: 0,
                        CornerRadiusBottomRight: 0,
                        CornerRadiusBottomLeft: 0,
                        TranslationY: ty,
                        Opacity: isHovered ? 1.0 : 0.0,
                        IsSnapped: true,
                        IsHandleVisible: false,
                        IsHidden: !isHovered,
                        IsFullWidth: true,
                        AppBarReservedHeightDip: 0,
                        TranslationX: tx);
                }

                default: // DockDown, ReserveSpace
                    return new DockResolvedState(
                        LeftDip: outer.Left,
                        TopDip: outer.Top,
                        WidthDip: outer.Width,
                        HeightDip: outer.Height,
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
                        AppBarReservedHeightDip: reservedHeight,
                        TranslationX: 0);
            }
        }

        // Yüzen Kapsül Modu (FloatingCapsule)
        DockRectDip ComputeCapsuleRect(bool isSnapped)
        {
            var thickness = isSnapped ? SnappedHeightDip : FloatingHeightDip;
            var offset = isSnapped ? edgeOffsetDip : (FloatingBottomMarginDip + edgeOffsetDip);
            var capsuleLength = Math.Max(contentWidthDip > 0 ? contentWidthDip : 520.0, 160.0);

            if (edge is DockEdge.Bottom or DockEdge.Top)
            {
                var width = Math.Min(capsuleLength, monitorWidth);
                var left = monitorLeft + Math.Max(0.0, (monitorWidth - width) / 2.0);
                var top = edge == DockEdge.Bottom
                    ? monitorBottom - offset - thickness
                    : monitorTop + offset;
                return new DockRectDip(left, top, width, thickness);
            }
            else
            {
                var height = Math.Min(capsuleLength, monitorHeight);
                var top = monitorTop + Math.Max(0.0, (monitorHeight - height) / 2.0);
                var left = edge == DockEdge.Left
                    ? monitorLeft + offset
                    : monitorRight - offset - thickness;
                return new DockRectDip(left, top, thickness, height);
            }
        }

        if (isWindowFullscreen && hideInFullscreen)
        {
            var outer = ComputeCapsuleRect(isSnapped: false);
            var (tx, ty) = Translation(true, FloatingHeightDip, FloatingBottomMarginDip + edgeOffsetDip);
            return new DockResolvedState(
                LeftDip: outer.Left,
                TopDip: outer.Top,
                WidthDip: outer.Width,
                HeightDip: outer.Height,
                CornerRadiusTopLeft: FloatingCornerRadiusDip,
                CornerRadiusTopRight: FloatingCornerRadiusDip,
                CornerRadiusBottomRight: FloatingCornerRadiusDip,
                CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                TranslationY: ty,
                Opacity: 0.0,
                IsSnapped: false,
                IsHandleVisible: false,
                IsHidden: true,
                IsFullWidth: false,
                AppBarReservedHeightDip: 0,
                TranslationX: tx);
        }

        // Aşağı yapış'ta büyütülmüş pencere dock'u gizlemez: dock alta yapışık kalır (aşağıdaki DockDown kolu), yer ayırmaz.
        // Yalnız gerçek tam ekran (yukarıdaki dal) gizler. Çekil ve Hep gizli büyütülmüş pencerede gizlemeyi sürdürür.
        // Hep havada da gizlemez: pencere ne kadar yaklaşırsa yaklaşsın kapsül yüzer kalır.
        if (isWindowMaximized && hideInFullscreen
            && proximityMode is not (DockProximityMode.ReserveSpace or DockProximityMode.DockDown or DockProximityMode.AlwaysFloating))
        {
            var outer = ComputeCapsuleRect(isSnapped: false);
            var isHovered = (edgeRevealEnabled && isMouseAtBottomEdge) || isMouseOverDock;
            var (tx, ty) = Translation(!isHovered, FloatingHeightDip, FloatingBottomMarginDip + edgeOffsetDip);
            return new DockResolvedState(
                LeftDip: outer.Left,
                TopDip: outer.Top,
                WidthDip: outer.Width,
                HeightDip: outer.Height,
                CornerRadiusTopLeft: FloatingCornerRadiusDip,
                CornerRadiusTopRight: FloatingCornerRadiusDip,
                CornerRadiusBottomRight: FloatingCornerRadiusDip,
                CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                TranslationY: ty,
                Opacity: isHovered ? 1.0 : 0.0,
                IsSnapped: false,
                IsHandleVisible: !isHovered,
                IsHidden: !isHovered,
                IsFullWidth: false,
                AppBarReservedHeightDip: 0,
                TranslationX: tx);
        }

        switch (proximityMode)
        {
            case DockProximityMode.DockDown:
            {
                if (windowIntersects)
                {
                    // Aşağı yapış: Kenara oturur, 60 px -> 46 px incelir, içe bakan iki köşe 14 px yuvarlak
                    var outer = ComputeCapsuleRect(isSnapped: true);
                    var (tl, tr, br, bl) = SnappedCorners();
                    return new DockResolvedState(
                        LeftDip: outer.Left,
                        TopDip: outer.Top,
                        WidthDip: outer.Width,
                        HeightDip: outer.Height,
                        CornerRadiusTopLeft: tl,
                        CornerRadiusTopRight: tr,
                        CornerRadiusBottomRight: br,
                        CornerRadiusBottomLeft: bl,
                        TranslationY: 0,
                        Opacity: 1.0,
                        IsSnapped: true,
                        IsHandleVisible: false,
                        IsHidden: false,
                        IsFullWidth: false,
                        AppBarReservedHeightDip: 0,
                        TranslationX: 0);
                }
                else
                {
                    // Boşta / Pencere uzakta: Yüzen kapsül (60 px, kenardan 10 px + ofset, 18 px yarıçap)
                    var outer = ComputeCapsuleRect(isSnapped: false);
                    return new DockResolvedState(
                        LeftDip: outer.Left,
                        TopDip: outer.Top,
                        WidthDip: outer.Width,
                        HeightDip: outer.Height,
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
                        AppBarReservedHeightDip: 0,
                        TranslationX: 0);
                }
            }

            case DockProximityMode.ReserveSpace:
            {
                // Yer ayır: Masaüstü çalışma alanından (AppBar) 46 px ayırır, dock kenara oturur
                var outer = ComputeCapsuleRect(isSnapped: true);
                var (tl, tr, br, bl) = SnappedCorners();
                return new DockResolvedState(
                    LeftDip: outer.Left,
                    TopDip: outer.Top,
                    WidthDip: outer.Width,
                    HeightDip: outer.Height,
                    CornerRadiusTopLeft: tl,
                    CornerRadiusTopRight: tr,
                    CornerRadiusBottomRight: br,
                    CornerRadiusBottomLeft: bl,
                    TranslationY: 0,
                    Opacity: 1.0,
                    IsSnapped: true,
                    IsHandleVisible: false,
                    IsHidden: false,
                    IsFullWidth: false,
                    AppBarReservedHeightDip: (int)SnappedHeightDip,
                    TranslationX: 0);
            }

            case DockProximityMode.Dodge:
            {
                var outer = ComputeCapsuleRect(isSnapped: false);

                if (windowIntersects)
                {
                    var isHovered = isMouseAtBottomEdge || isMouseOverDock;
                    if (isHovered)
                    {
                        // Fare kenara geldiğinde dock kayarak geri çıkar
                        return new DockResolvedState(
                            LeftDip: outer.Left,
                            TopDip: outer.Top,
                            WidthDip: outer.Width,
                            HeightDip: outer.Height,
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
                            AppBarReservedHeightDip: 0,
                            TranslationX: 0);
                    }
                    else
                    {
                        // Pencere üstüne binince dock çekilir, altta 120×4 tutamak kalır
                        var (tx, ty) = Translation(true, FloatingHeightDip, FloatingBottomMarginDip + edgeOffsetDip);
                        return new DockResolvedState(
                            LeftDip: outer.Left,
                            TopDip: outer.Top,
                            WidthDip: outer.Width,
                            HeightDip: outer.Height,
                            CornerRadiusTopLeft: FloatingCornerRadiusDip,
                            CornerRadiusTopRight: FloatingCornerRadiusDip,
                            CornerRadiusBottomRight: FloatingCornerRadiusDip,
                            CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                            TranslationY: ty,
                            Opacity: 0.0,
                            IsSnapped: false,
                            IsHandleVisible: true,
                            IsHidden: true,
                            IsFullWidth: false,
                            AppBarReservedHeightDip: 0,
                            TranslationX: tx);
                    }
                }
                else
                {
                    // Çakışan pencere yok, normal yüzer
                    return new DockResolvedState(
                        LeftDip: outer.Left,
                        TopDip: outer.Top,
                        WidthDip: outer.Width,
                        HeightDip: outer.Height,
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
                        AppBarReservedHeightDip: 0,
                        TranslationX: 0);
                }
            }

            case DockProximityMode.AlwaysHidden:
            {
                var outer = ComputeCapsuleRect(isSnapped: false);
                var isHovered = isMouseAtBottomEdge || isMouseOverDock;
                var (tx, ty) = Translation(!isHovered, FloatingHeightDip, FloatingBottomMarginDip + edgeOffsetDip);

                return new DockResolvedState(
                    LeftDip: outer.Left,
                    TopDip: outer.Top,
                    WidthDip: outer.Width,
                    HeightDip: outer.Height,
                    CornerRadiusTopLeft: FloatingCornerRadiusDip,
                    CornerRadiusTopRight: FloatingCornerRadiusDip,
                    CornerRadiusBottomRight: FloatingCornerRadiusDip,
                    CornerRadiusBottomLeft: FloatingCornerRadiusDip,
                    TranslationY: ty,
                    Opacity: isHovered ? 1.0 : 0.0,
                    IsSnapped: false,
                    IsHandleVisible: false,
                    IsHidden: !isHovered,
                    IsFullWidth: false,
                    AppBarReservedHeightDip: 0,
                    TranslationX: tx);
            }

            case DockProximityMode.AlwaysFloating:
            {
                // Hep havada: pencere yakınlığına (windowIntersects) ve büyütülmüşlüğe bakılmaz; hep yüzen kapsül.
                var outer = ComputeCapsuleRect(isSnapped: false);
                return new DockResolvedState(
                    LeftDip: outer.Left,
                    TopDip: outer.Top,
                    WidthDip: outer.Width,
                    HeightDip: outer.Height,
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
                    AppBarReservedHeightDip: 0,
                    TranslationX: 0);
            }

            default:
                goto case DockProximityMode.DockDown;
        }
    }
}
