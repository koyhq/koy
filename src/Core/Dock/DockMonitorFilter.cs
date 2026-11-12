using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// "Yalnız bu monitörün pencereleri" süzgeci. Bir pencere, merkezi dock'un monitöründeyse o monitöre aittir.
/// Karar yalnız pencere sınırlarından verilir (yeni Win32 çağrısı yok); sınırlar zaten pencere taramasıyla gelir.
/// </summary>
public static class DockMonitorFilter
{
    // Küçültülmüş pencerenin GetWindowRect değeri (-32000, -32000). Hangi monitörde olduğu bilinemez; kaybolmasın diye tutulur.
    private const int MinimizedSentinel = -30000;

    /// <summary>Pencerenin merkezi monitörün içinde mi (sol/üst dahil, sağ/alt hariç).</summary>
    public static bool IsOnMonitor(WindowRect window, WindowRect monitor)
    {
        if (window.Left <= MinimizedSentinel && window.Top <= MinimizedSentinel)
            return true;

        var cx = window.Left + (window.Right - window.Left) / 2;
        var cy = window.Top + (window.Bottom - window.Top) / 2;
        return cx >= monitor.Left && cx < monitor.Right && cy >= monitor.Top && cy < monitor.Bottom;
    }

    /// <summary>
    /// Öğeleri monitöre göre süzer. Pencereleri hiç kalmayan sabitli öğe "çalışmıyor" olarak kalır; sabitlenmemiş olan düşer.
    /// Pencere bilgisi olmayan öğe (Windows listesi null) olduğu gibi geçer.
    /// </summary>
    /// <param name="foreground">Ön plan pencere (0 = yok): öğenin etkinliği süzülmüş pencerelerle yeniden hesaplanır.</param>
    public static IReadOnlyList<DockItem> Apply(IReadOnlyList<DockItem> items, WindowRect monitor, nint foreground)
    {
        ArgumentNullException.ThrowIfNull(items);

        var result = new List<DockItem>(items.Count);
        foreach (var item in items)
        {
            if (item.Windows is null || item.Windows.Count == 0)
            {
                result.Add(item);
                continue;
            }

            var kept = item.Windows.Where(w => IsOnMonitor(w.Bounds, monitor)).ToList();
            if (kept.Count == item.Windows.Count)
            {
                result.Add(item);
                continue;
            }

            if (kept.Count == 0)
            {
                if (!item.IsPinned)
                    continue;

                result.Add(item with
                {
                    IsRunning = false,
                    IsActive = false,
                    IsFlashing = false,
                    BadgeCount = 0,
                    WindowHandles = [],
                    Windows = [],
                    ProcessId = 0
                });
                continue;
            }

            var handles = kept.ConvertAll(w => w.Handle);
            result.Add(item with
            {
                IsActive = foreground != 0 && handles.Contains(foreground),
                WindowHandles = handles,
                Windows = kept
            });
        }

        return result;
    }
}
