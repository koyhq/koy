namespace Koy.Core.Shell;

/// <summary>
/// Windows başlangıç (HKCU\...\Run ve StartupApproved\Run) kayıtlarının hedef yol, biçimlendirme ve göç kararları.
/// Saf C# mantığı: Win32 ve kayıt defteri çağrıları Platform katmanındadır.
/// </summary>
public static class StartupPathPolicy
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string StartupApprovedRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    public const string AppValueName = "Koy";

    /// <summary>
    /// Otomatik başlatmaya yazılacak çalıştırılabilir dosya yolunu çözer.
    /// Velopack kurulumunda <c>current\Koy.exe</c> yerine kök klasördeki kalıcı saplamayı (<c>Koy.exe</c>) seçer;
    /// saplama yoksa ya da taşınabilir/geliştirme derlemesindeyse <paramref name="processPath"/> döner.
    /// </summary>
    public static string? ResolveTargetPath(
        bool isInstalled,
        bool isPortable,
        string? rootAppDir,
        string? processPath,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (string.IsNullOrWhiteSpace(processPath))
            return null;

        if (isInstalled && !isPortable && !string.IsNullOrWhiteSpace(rootAppDir))
        {
            var exeName = Path.GetFileName(processPath);
            if (!string.IsNullOrEmpty(exeName))
            {
                var stubPath = Path.Combine(rootAppDir, exeName);
                if (fileExists(stubPath))
                    return stubPath;
            }
        }

        return processPath;
    }

    /// <summary>Boşluk içeren yollar Windows Run anahtarında bozulmasın diye yolu tırnaklar.</summary>
    public static string FormatCommandLine(string exePath, string? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);

        var trimmedPath = exePath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmedPath))
            throw new ArgumentException("Çalıştırılabilir dosya yolu boş olamaz.", nameof(exePath));

        return string.IsNullOrWhiteSpace(arguments)
            ? $"\"{trimmedPath}\""
            : $"\"{trimmedPath}\" {arguments.Trim()}";
    }

    /// <summary>
    /// Kayıt defterindeki komut satırından çalıştırılabilir dosya yolunu ayıklar.
    /// Tırnaklı yolları, tırnaksız boşluklu <c>.exe</c> yollarını ve ek argümanlı satırları çözer.
    /// </summary>
    public static string? ExtractExecutablePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        var trimmed = commandLine.Trim();
        if (trimmed[0] == '"')
        {
            var close = trimmed.IndexOf('"', 1);
            if (close <= 1)
                return null;

            var quoted = trimmed[1..close].Trim();
            return quoted.Length == 0 ? null : quoted;
        }

        var exeEnd = FindExeBoundary(trimmed);
        if (exeEnd > 0)
            return trimmed[..exeEnd].Trim();

        var spaceIdx = trimmed.IndexOfAny([' ', '\t']);
        return spaceIdx > 0 ? trimmed[..spaceIdx] : trimmed;
    }

    private static int FindExeBoundary(string value)
    {
        var idx = 0;
        while (idx < value.Length)
        {
            var match = value.IndexOf(".exe", idx, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
                return -1;

            var end = match + 4;
            if (end == value.Length || char.IsWhiteSpace(value[end]))
                return end;

            idx = end;
        }

        return -1;
    }

    /// <summary>
    /// Windows Görev Yöneticisi (<c>StartupApproved\Run</c>) bu girdiyi devre dışı bırakmış mı?
    /// Kayıt yoksa ya da boşsa etkindir; ilk bayt tek ise (ör. 0x03, 0x07, 0x01) kullanıcı tarafından kapatılmıştır.
    /// </summary>
    public static bool IsStartupApprovedEnabled(byte[]? approvedBytes) =>
        approvedBytes is null || approvedBytes.Length == 0 || (approvedBytes[0] & 0x01) == 0;

    /// <summary>
    /// Kayıtlı başlangıç girdisinin gerçekten çalışır ve beklenen hedefe eşit olup olmadığını denetler.
    /// Silinmiş yol, eski geliştirme/taşınabilir yolu, <c>current\Koy.exe</c> veya Görev Yöneticisi engeli false döner.
    /// </summary>
    public static bool IsEntryValid(
        string? registryCommandLine,
        byte[]? approvedBytes,
        string? expectedExePath,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!IsStartupApprovedEnabled(approvedBytes))
            return false;

        var registeredPath = ExtractExecutablePath(registryCommandLine);
        if (string.IsNullOrWhiteSpace(registeredPath) || !fileExists(registeredPath))
            return false;

        if (!string.IsNullOrWhiteSpace(expectedExePath) && !PathsEqual(registeredPath, expectedExePath))
            return false;

        return true;
    }

    /// <summary>
    /// Velopack ile kurulu sürüm açılırken mevcut Run kaydı eski, geliştirme/taşınabilir ya da <c>current\Koy.exe</c>
    /// yoluna bakıyorsa kalıcı saplamaya geçirilmeli mi?
    /// Görev Yöneticisi'nde açıkça kapatılmışsa kullanıcının kararı korunur.
    /// </summary>
    public static bool ShouldMigrateInstalledEntry(
        string? registryCommandLine,
        byte[]? approvedBytes,
        string? expectedInstalledExePath,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (string.IsNullOrWhiteSpace(expectedInstalledExePath))
            return false;

        if (!IsStartupApprovedEnabled(approvedBytes))
            return false;

        var registeredPath = ExtractExecutablePath(registryCommandLine);
        if (string.IsNullOrWhiteSpace(registeredPath))
            return false;

        if (PathsEqual(registeredPath, expectedInstalledExePath))
            return false;

        var registeredName = Path.GetFileName(registeredPath);
        var expectedName = Path.GetFileName(expectedInstalledExePath);
        return string.Equals(registeredName, expectedName, StringComparison.OrdinalIgnoreCase)
               || !fileExists(registeredPath);
    }

    /// <summary>Dosya yollarını büyük/küçük harf duyarsız ve normalize edilmiş biçimde karşılaştırır.</summary>
    public static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            var fullLeft = Path.GetFullPath(left.Trim().Trim('"'))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRight = Path.GetFullPath(right.Trim().Trim('"'))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(fullLeft, fullRight, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
