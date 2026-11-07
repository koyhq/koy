namespace Koy.Core.Dock;

/// <summary>
/// Dock simgesinin üzerine sürükleme eylemi: Sol %25 = Before, Orta ~%50 = Merge, Sağ %25 = After.
/// </summary>
public enum DockDropZone
{
    Before,
    Merge,
    After
}

/// <summary>
/// Geriye dönük uyumluluk veya alternatif adlandırma için eylem takma adı.
/// </summary>
public enum DockDropAction
{
    Before,
    Merge,
    After
}

/// <summary>
/// Dock öğeleri üzerinde sürükleme bölgelerini hesaplayan saf geometri motoru.
/// </summary>
public static class DockDropZones
{
    /// <summary>
    /// Birleştirme (Merge) eyleminin silahlanması (armed) için gereken kesintisiz bekleme süresi: 400 ms.
    /// </summary>
    public static readonly TimeSpan MergeHoverDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Verilen genişlik ve bağıl X konumuna göre sürükleme bölgesini tespit eder.
    /// Varsayılan mergeRatio 0.5 (%50 orta bölge, %25 sol, %25 sağ).
    /// </summary>
    public static DockDropZone DetectZone(double itemWidth, double relativeX, double mergeRatio = 0.5)
    {
        if (itemWidth <= 0)
            return DockDropZone.Merge;

        if (mergeRatio <= 0)
            return relativeX < itemWidth * 0.5 ? DockDropZone.Before : DockDropZone.After;

        if (mergeRatio >= 1.0)
            return DockDropZone.Merge;

        var sideRatio = (1.0 - mergeRatio) / 2.0;
        var leftThreshold = itemWidth * sideRatio;
        var rightThreshold = itemWidth * (1.0 - sideRatio);

        if (relativeX < leftThreshold)
            return DockDropZone.Before;

        if (relativeX > rightThreshold)
            return DockDropZone.After;

        return DockDropZone.Merge;
    }

    /// <summary>
    /// İmlecin mutlak X koordinatı, yuvanın sol kenarı ve genişliğine göre bölgeyi hesaplar.
    /// </summary>
    public static DockDropZone ResolveZone(double cursorX, double slotLeft, double slotWidth, double mergeRatio = 0.5) =>
        DetectZone(slotWidth, cursorX - slotLeft, mergeRatio);
}

/// <summary>
/// Bölge tespiti için ek statik sarmalayıcı sınıf.
/// </summary>
public static class DockDropZoneDetector
{
    public static DockDropZone DetectZone(double itemWidth, double relativeX, double mergeRatio = 0.5) =>
        DockDropZones.DetectZone(itemWidth, relativeX, mergeRatio);

    public static DockDropZone ResolveZone(double cursorX, double slotLeft, double slotWidth, double mergeRatio = 0.5) =>
        DockDropZones.ResolveZone(cursorX, slotLeft, slotWidth, mergeRatio);
}

/// <summary>
/// Birleştirme (Merge) bölgesinde 400 ms kesintisiz duraklamayı takip eden saf durum yöneticisi.
/// Platform zamanlayıcılarına bağımlı değildir; dışarıdan geçirilen zaman damgasıyla test edilebilir.
/// </summary>
public sealed class DockDropZoneTracker
{
    public static readonly TimeSpan MergeHoverDelay = DockDropZones.MergeHoverDelay;

    public static DockDropZone DetectZone(double itemWidth, double relativeX, double mergeRatio = 0.5) =>
        DockDropZones.DetectZone(itemWidth, relativeX, mergeRatio);

    public static DockDropZone ResolveZone(double cursorX, double slotLeft, double slotWidth, double mergeRatio = 0.5) =>
        DockDropZones.ResolveZone(cursorX, slotLeft, slotWidth, mergeRatio);

    private int _targetSlot = -1;
    private string? _targetKey;
    private TimeSpan? _hoverStartTime;
    private readonly TimeProvider? _timeProvider;

    public bool IsMergeArmed { get; private set; }
    public int TargetSlot => _targetSlot;
    public string? TargetKey => _targetKey;
    public TimeSpan? HoverStartTime => _hoverStartTime;

    public DockDropZoneTracker(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// İndeks tabanlı yuva ve zaman damgasıyla durumu günceller.
    /// İmleç 400 ms boyunca Merge bölgesinde kalırsa IsMergeArmed true döner.
    /// </summary>
    public bool Update(int targetSlot, DockDropZone zone, TimeSpan now)
    {
        if (zone != DockDropZone.Merge || targetSlot < 0)
        {
            Reset();
            return false;
        }

        if (_targetSlot != targetSlot)
        {
            _targetSlot = targetSlot;
            _targetKey = null;
            _hoverStartTime = now;
            IsMergeArmed = false;
            return false;
        }

        _hoverStartTime ??= now;
        if (now - _hoverStartTime.Value >= MergeHoverDelay)
        {
            IsMergeArmed = true;
        }

        return IsMergeArmed;
    }

    /// <summary>
    /// Anahtar/kimlik tabanlı yuva ve zaman damgasıyla durumu günceller.
    /// </summary>
    public bool Update(string targetKey, DockDropZone zone, TimeSpan now)
    {
        if (zone != DockDropZone.Merge || string.IsNullOrEmpty(targetKey))
        {
            Reset();
            return false;
        }

        if (!string.Equals(_targetKey, targetKey, StringComparison.OrdinalIgnoreCase))
        {
            _targetKey = targetKey;
            _targetSlot = -1;
            _hoverStartTime = now;
            IsMergeArmed = false;
            return false;
        }

        _hoverStartTime ??= now;
        if (now - _hoverStartTime.Value >= MergeHoverDelay)
        {
            IsMergeArmed = true;
        }

        return IsMergeArmed;
    }

    /// <summary>
    /// TimeProvider ile zaman damgasını hesaplayarak durumu günceller.
    /// </summary>
    public bool Update(int targetSlot, DockDropZone zone, TimeProvider timeProvider)
    {
        return Update(targetSlot, zone, TimeSpan.FromTicks(timeProvider.GetTimestamp()));
    }

    /// <summary>
    /// Yapıcıda TimeProvider verilmişse parametresiz zaman güncellemesi yapar.
    /// </summary>
    public bool Update(int targetSlot, DockDropZone zone)
    {
        if (_timeProvider is null)
            throw new InvalidOperationException("TimeProvider yapıcıda sağlanmadı. Update(targetSlot, zone, TimeSpan now) kullanın.");

        return Update(targetSlot, zone, TimeSpan.FromTicks(_timeProvider.GetTimestamp()));
    }

    /// <summary>
    /// Durumu sıfırlar. İmleç Merge dışına çıktığında veya sürükleme bittiğinde çağrılır.
    /// </summary>
    public void Reset()
    {
        _targetSlot = -1;
        _targetKey = null;
        _hoverStartTime = null;
        IsMergeArmed = false;
    }
}
