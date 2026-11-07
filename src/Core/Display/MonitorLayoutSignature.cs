namespace Koy.Core.Display;

/// <summary>
/// Ekran düzeninin imzası: kimlik, sınırlar, DPI ve ana ekran. ÇALIŞMA ALANI bilerek yoktur: dock kendi AppBar'ıyla
/// çalışma alanını değiştirir, Gezgin de görev çubuğunu gösterip gizleyince oynatır; imzaya girseydi her çalışma alanı
/// oynaması "ekran düzeni değişti" sayılır, dock görev çubuğunu yeniden gizler (ABM_SETSTATE) ve bu Gezgin'e çalışma
/// alanını yeniden hesaplatıp döngüye girerdi (büyütülmüş pencereler sürekli yeniden boyutlanıyordu).
/// </summary>
public static class MonitorLayoutSignature
{
    public static string Compute(IEnumerable<DisplayMonitor> monitors) =>
        string.Join("|", monitors
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Select(m => $"{m.Id};{m.Bounds};{m.DpiScaleX:F2};{m.IsPrimary}"));
}
