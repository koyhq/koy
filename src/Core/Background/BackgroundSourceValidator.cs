namespace Koy.Core.Background;

/// <summary>
/// Doğrulama sonucu. Hata yoksa <see cref="Error"/> <see cref="BackgroundSourceError.None"/> olur.
/// </summary>
public enum BackgroundSourceError
{
    None,
    MissingPath,
    PathTooLong,
    InvalidCharacters,
    NotLocalPath,
    UnknownExtension,
    KindMismatch
}

/// <summary>
/// Arka plan kaynağını doğrular. Dosya sistemine dokunmaz: yalnızca metin ve uzantı denetlenir.
/// Yalnız bilinen görsel/video uzantıları kabul edilir; yürütülebilir ya da kısayol türleri
/// (exe, bat, ps1, lnk, url ...) listede olmadığı için zaten reddedilir.
/// </summary>
public static class BackgroundSourceValidator
{
    /// <summary>En uzun kabul edilen yol (Windows MAX_PATH).</summary>
    public const int MaxPathLength = 260;

    private static readonly string[] StillExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    private static readonly string[] AnimatedExtensions = [".gif"];
    private static readonly string[] VideoExtensions = [".mp4", ".webm"];

    /// <summary>Kaynağı denetler ve ilk bulunan hatayı döner.</summary>
    public static BackgroundSourceError Validate(BackgroundSource source)
    {
        if (source.Kind == BackgroundKind.None)
            return string.IsNullOrEmpty(source.Path) ? BackgroundSourceError.None : BackgroundSourceError.KindMismatch;

        var path = source.Path ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return BackgroundSourceError.MissingPath;

        // Kırpma ve gömme sonrası sınırı aşan yollar dosya API'lerinde beklenmedik davranır.
        if (path.Length > MaxPathLength)
            return BackgroundSourceError.PathTooLong;

        // Denetim karakterleri ve satır sonu, log ve ayar dosyasında yol enjeksiyonuna açıktır.
        if (path.Any(char.IsControl))
            return BackgroundSourceError.InvalidCharacters;

        // Uzak kaynak (URL, UNC) ve göreli yol çözümlemesi bu özelliğin kapsamı dışında.
        if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith(@"\\", StringComparison.Ordinal) || !IsAbsoluteWindowsPath(path))
            return BackgroundSourceError.NotLocalPath;

        var extension = Extension(path);
        if (extension is null)
            return BackgroundSourceError.UnknownExtension;

        var expected = KindOf(extension);
        if (expected is null)
            return BackgroundSourceError.UnknownExtension;

        return expected == source.Kind ? BackgroundSourceError.None : BackgroundSourceError.KindMismatch;
    }

    /// <summary>Uzantıya göre tür; bilinmeyen uzantıda null.</summary>
    public static BackgroundKind? KindOf(string extension)
    {
        var lower = extension.ToLowerInvariant();
        if (StillExtensions.Contains(lower)) return BackgroundKind.StillImage;
        if (AnimatedExtensions.Contains(lower)) return BackgroundKind.AnimatedImage;
        if (VideoExtensions.Contains(lower)) return BackgroundKind.Video;
        return null;
    }

    /// <summary>Son noktadan sonraki uzantı (nokta dahil); yoksa null.</summary>
    private static string? Extension(string path)
    {
        var lastDot = path.LastIndexOf('.');
        var lastSeparator = path.LastIndexOfAny(['\\', '/']);
        if (lastDot < 0 || lastDot < lastSeparator || lastDot == path.Length - 1)
            return null;
        return path[lastDot..];
    }

    /// <summary>Sürücü harfli (C:\...) ya da çözümlenmiş yerel yol mu. Platformdan bağımsız, saf metin denetimi.</summary>
    private static bool IsAbsoluteWindowsPath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
}
