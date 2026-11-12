using System.Diagnostics;
using Koy.Core.Tray;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock.Tray;

/// <summary>
/// Tepsi simgesinin sahibi uygulamanın exe adı (issue #77) ve bu oturumda görülen adlar. Yalnız gösterim katmanı:
/// tepsi barındırma servisine (TrayHostService) dokunmaz; simgenin sahip penceresinden süreç adı okunur.
/// Görülen adlar ayarlar sayfasındaki listeyi besler; tepsi penceresi açıldıkça dolar (bellekte, en çok birkaç düzine ad).
/// </summary>
public static class TrayAppNames
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> SeenNames = new(StringComparer.Ordinal);

    /// <summary>Bu oturumda tepside görülen exe adları (anahtar biçiminde).</summary>
    public static IReadOnlyList<string> Seen
    {
        get
        {
            lock (Gate)
                return SeenNames.ToList();
        }
    }

    /// <summary>Sahip pencereden exe adı ("discord.exe"); bulunamazsa boş metin. Bulunan ad görülenlere eklenir.</summary>
    public static string ForOwner(nint ownerHwnd)
    {
        var pid = WindowStyles.GetWindowProcessId(ownerHwnd);
        if (pid == 0)
            return "";

        string key;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            key = TrayIconVisibilityPolicy.AppKey(process.ProcessName + ".exe");
        }
        catch (ArgumentException)
        {
            return ""; // süreç bu arada kapandı
        }
        catch (InvalidOperationException)
        {
            return "";
        }

        lock (Gate)
            SeenNames.Add(key);
        return key;
    }
}
