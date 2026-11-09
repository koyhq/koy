namespace Koy.Core.Dock;

/// <summary>Dock'taki Başlat düğmesinin simgesi: Koy'un kendi simgesi (varsayılan), Windows logosu ya da kullanıcının dosyası.</summary>
public enum DockStartIcon
{
    Koy,
    Windows,
    Custom
}

/// <summary>Çözülmüş simge kaynağı. <see cref="Path"/> yalnız <see cref="DockStartIcon.Custom"/> iken dolu.</summary>
public readonly record struct DockStartIconSource(DockStartIcon Kind, string? Path);

/// <summary>
/// Başlat düğmesinde hangi simgenin çizileceği kararı. Özel dosya seçilmiş ama yol boşsa, uzantı desteklenmiyorsa ya da
/// dosya yoksa Koy simgesine düşer: dock hiçbir zaman boş ya da bozuk bir düğmeyle açılmaz.
/// </summary>
public static class DockStartIconResolver
{
    /// <summary>Özel simge için kabul edilen uzantılar (WPF'in yerleşik çözücüleri).</summary>
    public static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".ico"];

    public static bool IsSupportedFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string ext;
        try
        {
            ext = System.IO.Path.GetExtension(path.Trim());
        }
        catch (ArgumentException)
        {
            return false;
        }

        return SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    /// <param name="fileExists">Sınama için dosya denetimi; boşsa gerçek dosya sistemi.</param>
    public static DockStartIconSource Resolve(DockStartIcon kind, string? customPath, Func<string, bool>? fileExists = null)
    {
        switch (kind)
        {
            case DockStartIcon.Windows:
                return new DockStartIconSource(DockStartIcon.Windows, null);
            case DockStartIcon.Custom when IsSupportedFile(customPath):
                var path = customPath!.Trim();
                // Bozuk yol dosya denetiminde atabilir; dock'u düşürmek yerine Koy simgesine dön.
                try
                {
                    if ((fileExists ?? System.IO.File.Exists)(path))
                        return new DockStartIconSource(DockStartIcon.Custom, path);
                }
                catch (Exception)
                {
                }

                return new DockStartIconSource(DockStartIcon.Koy, null);
            default:
                return new DockStartIconSource(DockStartIcon.Koy, null);
        }
    }

    public static DockStartIconSource Resolve(Koy.Core.Settings.AppSettings settings, Func<string, bool>? fileExists = null)
        => Resolve(settings.DockStartIcon, settings.DockStartIconPath, fileExists);
}
