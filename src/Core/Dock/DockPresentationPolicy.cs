namespace Koy.Core.Dock;

/// <summary>Dört kenar boşluğu (DIP): sol, üst, sağ, alt.</summary>
public readonly record struct DockInsets(double Left, double Top, double Right, double Bottom);

/// <summary>Dört köşe yarıçapı (DIP): sol üst, sağ üst, sağ alt, sol alt.</summary>
public readonly record struct DockCorners(double TopLeft, double TopRight, double BottomRight, double BottomLeft)
{
    public static DockCorners Uniform(double radius) => new(radius, radius, radius, radius);
}

/// <summary>
/// <see cref="DockPresentationPolicy.Decide"/> girdisi: çözücünün sonucu ve konumu etkileyen anlık durum.
/// <paramref name="ControllerVisible"/> görünürlük denetleyicisinin kararı; denetleyici yoksa null.
/// <paramref name="Proximity"/> etkin yakınlık kipi (<c>EffectiveProximity</c>).
/// </summary>
public readonly record struct DockPresentationInput(
    DockResolvedState Resolved,
    DockEdge Edge,
    DockProximityMode Proximity,
    bool HideInFullscreen,
    bool HiddenEdgeLineEnabled,
    double EdgeOffsetDip,
    bool IsForegroundFullscreen,
    bool IsMouseAtEdge,
    bool? ControllerVisible);

/// <summary>Dock'un o anki çizim kararı: kenar boşlukları, köşeler, görünürlük, kayma hedefi ve fare izleme ihtiyacı.</summary>
public readonly record struct DockPresentation(
    DockInsets Margin,
    DockCorners Corners,
    bool ShouldShow,
    bool IsEdgeLineVisible,
    bool NeedsEdgeWatch,
    double TargetX,
    double TargetY,
    double TargetOpacity);

/// <summary>
/// DockViewModel.EvaluatePosition içindeki kararlar (saf): hangi boşluk ve köşe, görünsün mü, gizliyken ne kadar kayar,
/// ince kenar çizgisi ve fare izleme. ViewModel yalnız sonucu özelliklere ve yaylara yazar.
/// </summary>
public static class DockPresentationPolicy
{
    // Yapışık ya da yüzen kapsülün ekran kenarına dönük olmayan taraflardaki boşluğu.
    private const double SideGap = 12;
    private const double FloatingEdgeGap = 10;
    private const double FloatingCornerRadius = 18;
    private const double HideSlideExtra = 10;

    public static DockPresentation Decide(DockPresentationInput i)
    {
        var resolved = i.Resolved;

        var (margin, corners) = Shape(resolved, i.Edge);

        // Tam ekran bu monitör için hesaplanır (yan ekrandaki oyun bu ekrandaki dock'u gizlemez; denetleyicinin
        // sistem geneli bayrağı bilerek kullanılmaz).
        var isBlockedByFullscreen = i.HideInFullscreen && i.IsForegroundFullscreen;
        var shouldShow = !resolved.IsHidden && !isBlockedByFullscreen;
        if (i.ControllerVisible is { } controllerVisible && i.Proximity != DockProximityMode.ReserveSpace)
            shouldShow = shouldShow && controllerVisible;

        // Çekil tutamağı zaten gizli dock'u işaret eder; ikisi üst üste çizilmez.
        var isEdgeLineVisible = !resolved.IsHandleVisible && DockPresentationMath.ShowsHiddenEdgeLine(
            i.HiddenEdgeLineEnabled, !shouldShow, isBlockedByFullscreen || i.IsForegroundFullscreen);

        // Fare izleme yalnız dock gizliyken (ya da kenardan açıldıysa, kapanışını görmek için) ve tam ekranda değilken
        // gerekir; tam ekranda kenardan açılma yok.
        var needsEdgeWatch = (!shouldShow || i.IsMouseAtEdge) && !isBlockedByFullscreen && !i.IsForegroundFullscreen;

        var (targetX, targetY) = Target(resolved, i.Edge, i.EdgeOffsetDip, shouldShow);

        return new DockPresentation(margin, corners, shouldShow, isEdgeLineVisible, needsEdgeWatch,
            targetX, targetY, shouldShow ? resolved.Opacity : 0.0);
    }

    private static (DockInsets Margin, DockCorners Corners) Shape(DockResolvedState resolved, DockEdge edge)
    {
        if (resolved.IsFullWidth)
            return (new DockInsets(0, 0, 0, 0), DockCorners.Uniform(0));

        if (resolved.IsSnapped)
        {
            var snapped = edge switch
            {
                DockEdge.Top => new DockInsets(SideGap, 0, SideGap, SideGap),
                DockEdge.Left => new DockInsets(0, SideGap, SideGap, SideGap),
                DockEdge.Right => new DockInsets(SideGap, SideGap, 0, SideGap),
                _ => new DockInsets(SideGap, SideGap, SideGap, 0)
            };
            return (snapped, new DockCorners(resolved.CornerRadiusTopLeft, resolved.CornerRadiusTopRight,
                resolved.CornerRadiusBottomRight, resolved.CornerRadiusBottomLeft));
        }

        var floating = edge switch
        {
            DockEdge.Top => new DockInsets(SideGap, FloatingEdgeGap, SideGap, SideGap),
            DockEdge.Left => new DockInsets(FloatingEdgeGap, SideGap, SideGap, SideGap),
            DockEdge.Right => new DockInsets(SideGap, SideGap, FloatingEdgeGap, SideGap),
            _ => new DockInsets(SideGap, SideGap, SideGap, FloatingEdgeGap)
        };
        return (floating, DockCorners.Uniform(FloatingCornerRadius));
    }

    // Görünürken ya da çözücü kendi gizleme kaymasını verdiğinde onu kullanır; aksi halde (denetleyici ya da tam ekran
    // gizledi) dock kendi kalınlığı + kenar uzaklığı kadar ekran kenarından dışarı kayar.
    private static (double X, double Y) Target(DockResolvedState resolved, DockEdge edge, double edgeOffsetDip, bool shouldShow)
    {
        if (shouldShow || resolved.IsHidden)
            return (resolved.TranslationX, resolved.TranslationY);

        var thickness = resolved.IsFullWidth ? DockPositionResolver.FullWidthHeightDip
            : resolved.IsSnapped ? DockPositionResolver.SnappedHeightDip : DockPositionResolver.FloatingHeightDip;
        var offset = resolved.IsFullWidth || resolved.IsSnapped
            ? edgeOffsetDip
            : DockPositionResolver.FloatingBottomMarginDip + edgeOffsetDip;
        var shift = thickness + offset + HideSlideExtra;
        return edge switch
        {
            DockEdge.Top => (0.0, -shift),
            DockEdge.Left => (-shift, 0.0),
            DockEdge.Right => (shift, 0.0),
            _ => (0.0, shift)
        };
    }
}
