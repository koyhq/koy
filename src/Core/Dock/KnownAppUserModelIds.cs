namespace Koy.Core.Dock;

/// <summary>
/// Pencerelerinde açık (pencere başına) AUMID bulunmayan, Windows'un süreçten türettiği iyi bilinen sistem uygulamaları.
/// Gezgin pencereleri (CabinetWClass) AUMID özelliği taşımaz ama görev çubuğunda "Microsoft.Windows.Explorer" altında
/// gruplanır; sabitli File Explorer kısayolunun AUMID'i de budur.
/// </summary>
public static class KnownAppUserModelIds
{
    public const string Explorer = "Microsoft.Windows.Explorer";

    /// <summary>Süreç yolundan bilinen AUMID; tanınmıyorsa null.</summary>
    public static string? ForProcessPath(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            return null;

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrEmpty(systemRoot) &&
            PinnedAppMatcher.SamePath(processPath, Path.Combine(systemRoot, "explorer.exe")))
            return Explorer;

        return null;
    }
}
