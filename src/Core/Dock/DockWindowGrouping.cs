namespace Koy.Core.Dock;

// Pencere gruplama ve monitör süzgeci saf karar mantığıdır: Windows'tan gelen düz değerlerle çalışır, WinRT / Win32 tipi yoktur.
// Pencere sayımı (EnumWindows, monitör ve sanal masaüstü bilgisi) Platform tarafının işidir ve bu dosyanın dışındadır.

/// <summary>Bir pencerenin dock kararı için gereken gerçekler.</summary>
public readonly record struct DockWindowFact(string ProcessName, int MonitorId, nint Handle);

/// <summary>Tek dock simgesine karşılık gelen pencereler. Tutamaçlar girdi sırasındadır.</summary>
public sealed record DockWindowGroup(string ProcessName, IReadOnlyList<nint> Handles);

public static class DockWindowGrouping
{
    /// <summary>Monitör süzgeci. <see cref="DockWindowMonitorScope.AllMonitors"/> listeyi olduğu gibi döndürür.</summary>
    public static IReadOnlyList<DockWindowFact> FilterByMonitor(
        IReadOnlyList<DockWindowFact> windows,
        DockWindowMonitorScope scope,
        int currentMonitorId)
    {
        ArgumentNullException.ThrowIfNull(windows);
        if (scope == DockWindowMonitorScope.AllMonitors)
            return windows;

        // Girdi sırası korunur: aynı girdi her zaman aynı çıktıyı verir.
        return windows.Where(w => w.MonitorId == currentMonitorId).ToList();
    }

    /// <summary>
    /// Pencereleri dock simgelerine böler. Sıra kararlıdır: gruplar ilk göründükleri sırayla, bir gruptaki tutamaçlar
    /// girdi sırasıyla gelir. Süreç adı büyük/küçük harf duyarsız eşlenir.
    /// </summary>
    /// <param name="availableSlots">Dock'un bu pencereler için ayırabildiği simge sayısı (yalnız WhenSpaceRunsOut kullanır).</param>
    public static IReadOnlyList<DockWindowGroup> Group(IReadOnlyList<DockWindowFact> windows, DockGroupingMode mode, int availableSlots)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentOutOfRangeException.ThrowIfNegative(availableSlots);

        // Yer varken gruplamak pencereleri gereksiz yere saklar; WhenSpaceRunsOut bu yüzden yalnız sığmayınca birleştirir.
        var effective = mode == DockGroupingMode.WhenSpaceRunsOut
            ? (windows.Count <= availableSlots ? DockGroupingMode.Never : DockGroupingMode.Always)
            : mode;

        var groups = new List<DockWindowGroup>();
        if (effective == DockGroupingMode.Never)
        {
            foreach (var w in windows)
                groups.Add(new DockWindowGroup(w.ProcessName, [w.Handle]));
            return groups;
        }

        var handlesByName = new Dictionary<string, List<nint>>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var w in windows)
        {
            var key = w.ProcessName ?? "";
            if (!handlesByName.TryGetValue(key, out var handles))
            {
                handles = [];
                handlesByName[key] = handles;
                names.Add(key);
            }
            handles.Add(w.Handle);
        }

        foreach (var name in names)
            groups.Add(new DockWindowGroup(name, handlesByName[name]));
        return groups;
    }
}
