namespace Koy.Core.Dock;

/// <summary>
/// Pencerelerinde açık (pencere başına) AUMID bulunmayan, Windows'un süreçten türettiği iyi bilinen sistem uygulamaları.
/// Gezgin pencereleri (CabinetWClass) AUMID özelliği taşımaz ama görev çubuğunda "Microsoft.Windows.Explorer" altında
/// gruplanır; sabitli File Explorer kısayolunun AUMID'i de budur.
/// </summary>
public static class KnownAppUserModelIds
{
    public const string Explorer = "Microsoft.Windows.Explorer";

    /// <summary>Gezgin'in klasör pencereleri; kabuğun öteki pencereleri (masaüstü, görev çubuğu, açılır pencereler) bu AUMID'e girmez.</summary>
    public static bool IsExplorerFolderWindowClass(string? className) =>
        string.Equals(className, "CabinetWClass", StringComparison.Ordinal) ||
        string.Equals(className, "ExploreWClass", StringComparison.Ordinal);

    /// <summary>
    /// Süreç yolu ve pencere sınıfından bilinen AUMID; tanınmıyorsa null. Sınıf bilinmiyorsa (null) eşleme yapılmaz.
    /// </summary>
    public static string? ForWindow(string? processPath, string? className)
    {
        if (string.IsNullOrWhiteSpace(processPath) || !IsExplorerFolderWindowClass(className))
            return null;

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrEmpty(systemRoot) &&
            PinnedAppMatcher.SamePath(processPath, Path.Combine(systemRoot, "explorer.exe")))
            return Explorer;

        return null;
    }
}
