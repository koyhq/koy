using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// Açık bir pencerenin hangi sabitli uygulamaya ait olduğu. Windows görev çubuğu düğmeleri AppUserModelID (AUMID) ile
/// gruplar; Koy da önce onu kullanır. AUMID yoksa tam yol, en sonda dosya adı ve başlık gelir.
/// File Explorer kısayolunun hedef yolu boştur (kabuk öğesi) ve Gezgin pencereleri pencere başına AUMID taşımaz; bu yüzden
/// pencerede AUMID yoksa bilinen sistem uygulamaları tablosuna (explorer.exe → "Microsoft.Windows.Explorer") bakılır.
/// Tablo olmasaydı eşleme başlığa düşüp Gezgin penceresi için ikinci bir simge çıkarırdı.
/// </summary>
public static class PinnedAppMatcher
{
    public static bool IsMatch(PinnedApp pin, OpenWindowInfo win)
    {
        // 1. Kimlik: ikisinde de AUMID varsa karar onundur. Aynı exe'nin farklı AUMID'li pencereleri (Chrome profilleri,
        //    PWA'lar) Windows'ta ayrı düğmedir, burada da ayrı kalır.
        var winAumid = string.IsNullOrEmpty(win.AppUserModelId) ? KnownAppUserModelIds.ForWindow(win.ProcessPath, win.ClassName) : win.AppUserModelId;
        if (!string.IsNullOrEmpty(pin.AppUserModelId) && !string.IsNullOrEmpty(winAumid))
            return string.Equals(pin.AppUserModelId, winAumid, StringComparison.OrdinalIgnoreCase);

        // 2. Hedef yürütülebilir dosya: normalleştirilmiş tam yol, sonra dosya adı.
        if (!string.IsNullOrEmpty(pin.TargetPath) && !string.IsNullOrEmpty(win.ProcessPath))
        {
            if (SamePath(pin.TargetPath, win.ProcessPath))
                return true;

            var pinExe = Path.GetFileName(pin.TargetPath);
            var winExe = Path.GetFileName(win.ProcessPath);
            if (!string.IsNullOrEmpty(pinExe) && string.Equals(pinExe, winExe, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 3. Başlık / kısayol adı (son çare)
        var shortcutName = Path.GetFileNameWithoutExtension(pin.ShortcutPath);
        if (!string.IsNullOrEmpty(shortcutName) && !string.IsNullOrEmpty(win.Title) &&
            win.Title.Contains(shortcutName, StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrEmpty(pin.Title) && !string.IsNullOrEmpty(win.Title) &&
               win.Title.Contains(pin.Title, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Yolları karşılaştırır: ortam değişkenleri açılır, büyük/küçük harf ve System32/SysWOW64/Sysnative farkı yok sayılır.</summary>
    public static bool SamePath(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string path)
    {
        var p = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        try
        {
            p = Path.GetFullPath(p);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Geçersiz yol olduğu gibi karşılaştırılır
        }

        return p.Replace(@"\SysWOW64\", @"\System32\", StringComparison.OrdinalIgnoreCase)
                .Replace(@"\Sysnative\", @"\System32\", StringComparison.OrdinalIgnoreCase);
    }
}
