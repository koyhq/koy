namespace Koy.Core.Lyrics;

/// <summary>Çalma konumundan etkin satırı ve bir sonraki satır değişimine kalan süreyi hesaplar.</summary>
public static class LyricsTimeline
{
    /// <summary>Satır, sesten biraz önce vurgulanır: göz, geç gelen vurguyu erken gelenden daha çok fark eder.</summary>
    public static readonly TimeSpan Lead = TimeSpan.FromMilliseconds(150);

    /// <summary>Konumda etkin olan son satırın sırası; ilk satırdan önceyse -1.</summary>
    public static int IndexAt(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        var target = position + Lead;
        var low = 0;
        var high = lines.Count - 1;
        var found = -1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (lines[middle].Time <= target)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    /// <summary>Bir sonraki satıra kalan süre; son satırdaysa null. Zamanlayıcı yalnız satır değişiminde uyanır.</summary>
    public static TimeSpan? UntilNextChange(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        var next = IndexAt(lines, position) + 1;
        if (next >= lines.Count)
            return null;

        var delay = lines[next].Time - Lead - position;
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }
}
