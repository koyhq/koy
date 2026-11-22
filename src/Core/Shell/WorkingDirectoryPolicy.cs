namespace Koy.Core.Shell;

/// <summary>
/// Koy'un kendi çalışma dizini ve başlatılan alt uygulamalar için güvenli <c>WorkingDirectory</c> kararları.
/// Çalışma dizini Velopack <c>current\</c> klasöründe kalırsa başlatılan tüm uygulamalar bu klasörde tutamaç
/// tutar ve güncelleme sırasında <c>Update.exe</c> klasörü yedekleyemez (OS hatası 32).
/// </summary>
public static class WorkingDirectoryPolicy
{
    /// <summary>
    /// Koy sürecinin açılış çalışma dizinini seçer.
    /// Başlatma anahtarından gelen <c>System32</c> yerine kullanıcı profili seçilir; <c>AppContext.BaseDirectory</c>
    /// (<c>current\</c>) hiçbir zaman dönülmez.
    /// </summary>
    public static string? ResolveAppWorkingDirectory(string? userProfile, Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);

        return ExistingDirectory(userProfile, directoryExists);
    }

    /// <summary>
    /// Başlatılan bir uygulama ya da dosya için çalışma dizinini belirler:
    /// 1) Geçerliyse açıkça verilmiş dizin, 2) Köklü dosya yolunun üst klasörü, 3) Kullanıcı profili.
    /// </summary>
    public static string? ResolveLaunchWorkingDirectory(
        string? targetPath,
        string? explicitWorkingDirectory,
        string? userProfile,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);

        var explicitDir = ExistingDirectory(explicitWorkingDirectory, directoryExists);
        if (explicitDir is not null)
            return explicitDir;

        var targetParent = TryGetTargetParentDirectory(targetPath, directoryExists);
        if (targetParent is not null)
            return targetParent;

        return ExistingDirectory(userProfile, directoryExists);
    }

    private static string? TryGetTargetParentDirectory(string? targetPath, Func<string, bool> directoryExists)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return null;

        var trimmed = targetPath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        try
        {
            if (!Path.IsPathRooted(trimmed))
                return null;

            var parent = Path.GetDirectoryName(trimmed);
            return ExistingDirectory(parent, directoryExists);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? ExistingDirectory(string? path, Func<string, bool> directoryExists)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.Trim();
        return directoryExists(trimmed) ? trimmed : null;
    }
}
