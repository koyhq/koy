namespace Koy.Core.Voice;

public enum HarnessExecutableKind
{
    /// <summary>Doğrudan başlatılabilir (.exe / .com) dosya bulundu.</summary>
    Found,

    /// <summary>Yalnız .cmd / .bat betiği bulundu. Windows bunu kabuk olmadan başlatamaz.</summary>
    ScriptNotSupported,

    /// <summary>Hiçbir aday bulunamadı.</summary>
    NotFound
}

public readonly record struct HarnessExecutableResolution(HarnessExecutableKind Kind, string Path);

/// <summary>
/// Harness komutunu Windows PATH'inde çözer. npm kurulumları <c>codex.cmd</c> gibi betik shim'leri bırakır;
/// <c>Process.Start</c> (UseShellExecute=false) bunları çalıştıramaz ve hata "dosya bulunamadı" olarak döner.
/// Bu sınıf, bir <c>.exe</c> adayı varsa onu seçer; yalnız betik varsa bunu açıkça bildirir.
/// Dosya sistemi erişimi <paramref name="fileExists"/> ile dışarıdan verilir (test edilebilir).
/// </summary>
public static class HarnessExecutableResolver
{
    private static readonly string[] NativeExtensions = [".exe", ".com"];
    private static readonly string[] ScriptExtensions = [".cmd", ".bat"];

    public static HarnessExecutableResolution Resolve(
        string name,
        IEnumerable<string> pathDirectories,
        Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(pathDirectories);
        ArgumentNullException.ThrowIfNull(fileExists);

        // Yol bilgisi içeren ad (ör. C:\tools\x) doğrudan denenir; PATH'e bakılmaz.
        if (Path.IsPathRooted(name) || name.Contains('\\') || name.Contains('/'))
            return ResolveFile(name, fileExists);

        foreach (var dir in pathDirectories)
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            var result = ResolveFile(Path.Combine(dir, name), fileExists);
            if (result.Kind != HarnessExecutableKind.NotFound)
                return result;
        }

        return new HarnessExecutableResolution(HarnessExecutableKind.NotFound, name);
    }

    private static HarnessExecutableResolution ResolveFile(string path, Func<string, bool> fileExists)
    {
        var ext = Path.GetExtension(path);

        if (ext.Length > 0)
        {
            if (NativeExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return fileExists(path) ? Found(path) : NotFound(path);

            if (ScriptExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return fileExists(path)
                    ? new HarnessExecutableResolution(HarnessExecutableKind.ScriptNotSupported, path)
                    : NotFound(path);

            // Bilinmeyen uzantı: dosya varsa olduğu gibi denenir.
            return fileExists(path) ? Found(path) : NotFound(path);
        }

        // Uzantısız ad: önce yerel uzantılar, sonra betikler (Windows'taki sıraya yakın).
        foreach (var native in NativeExtensions)
        {
            if (fileExists(path + native))
                return Found(path + native);
        }

        foreach (var script in ScriptExtensions)
        {
            if (fileExists(path + script))
                return new HarnessExecutableResolution(HarnessExecutableKind.ScriptNotSupported, path + script);
        }

        return NotFound(path);
    }

    private static HarnessExecutableResolution Found(string path) =>
        new(HarnessExecutableKind.Found, path);

    private static HarnessExecutableResolution NotFound(string path) =>
        new(HarnessExecutableKind.NotFound, path);
}
