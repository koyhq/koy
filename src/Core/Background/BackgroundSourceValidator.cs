using System.Globalization;

namespace Koy.Core.Background;

/// <summary>
/// Doğrulama sonucu. Hata yoksa <see cref="Error"/> <see cref="BackgroundSourceError.None"/> olur.
/// Değerler kalıcıdır (ayar dosyasına yazılabilir): mevcut değerler değiştirilmez, yeni üye yalnızca sona eklenir.
/// </summary>
public enum BackgroundSourceError
{
    None = 0,
    MissingPath = 1,
    PathTooLong = 2,
    InvalidCharacters = 3,
    NotLocalPath = 4,
    UnknownExtension = 5,
    KindMismatch = 6,
    ReservedDeviceName = 7,
    NonCanonicalPath = 8
}

/// <summary>
/// Arka plan kaynağını doğrular. Dosya sistemine dokunmaz: yalnızca metin ve uzantı denetlenir.
/// Yalnız bilinen görsel/video uzantıları kabul edilir; yürütülebilir ya da kısayol türleri
/// (exe, bat, ps1, lnk, url ...) listede olmadığı için zaten reddedilir.
/// </summary>
public static class BackgroundSourceValidator
{
    /// <summary>En uzun kabul edilen yol: MAX_PATH (260) sonlandırıcı null'ı da sayar, metin en çok 259 karakter olur.</summary>
    public const int MaxPathLength = 259;

    private static readonly string[] StillExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    private static readonly string[] AnimatedExtensions = [".gif"];
    private static readonly string[] VideoExtensions = [".mp4", ".webm"];

    // Windows aygıt adları; uzantı ne olursa olsun dosya adı olarak aygıtı açar.
    private static readonly string[] DeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³",
        "CONIN$", "CONOUT$"
    ];

    // Dosya adında geçersiz (joker ve yönlendirme) karakterler; Core platformdan bağımsız kalsın diye elle yazıldı.
    private static readonly char[] InvalidNameChars = ['<', '>', '"', '|', '?', '*'];

    /// <summary>Kaynağı denetler ve ilk bulunan hatayı döner.</summary>
    public static BackgroundSourceError Validate(BackgroundSource source)
    {
        // Ayar dosyasından gelen tanımsız sayı da (örn. 99) geçerli tür sayılmaz.
        if (!Enum.IsDefined(source.Kind))
            return BackgroundSourceError.KindMismatch;

        if (source.Kind == BackgroundKind.None)
            return string.IsNullOrEmpty(source.Path) ? BackgroundSourceError.None : BackgroundSourceError.KindMismatch;

        var path = source.Path ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return BackgroundSourceError.MissingPath;

        // Kırpma ve gömme sonrası sınırı aşan yollar dosya API'lerinde beklenmedik davranır.
        if (path.Length > MaxPathLength)
            return BackgroundSourceError.PathTooLong;

        // Denetim karakterleri ve satır sonu, log ve ayar dosyasında yol enjeksiyonuna açıktır.
        // Biçim karakterleri (sağdan sola geçersiz kılma U+202E gibi) uzantıyı görünürde değiştirip kullanıcıyı yanıltır.
        if (path.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format))
            return BackgroundSourceError.InvalidCharacters;

        // Uzak kaynak (URL, UNC, \\?\ ve \\.\ önekleri) ve göreli yol bu özelliğin kapsamı dışında.
        // Yalnız "<harf>:\..." biçimi kabul edilir; eğik çizgi kanonik olmadığı için reddedilir.
        if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith(@"\\", StringComparison.Ordinal) || !IsAbsoluteWindowsPath(path))
            return BackgroundSourceError.NotLocalPath;

        // Alternatif veri akışı (dosya.png:gizli) sürücü harfinden sonra iki nokta üst üste ile gelir.
        // Yol metninde iki nokta yalnızca sürücü harfinden sonra olabilir, başka hiçbir yerde.
        if (path.IndexOf(':', 2) >= 0)
            return BackgroundSourceError.InvalidCharacters;

        // Joker ve yönlendirme karakterleri (* ? < > " |) dosya adı olamaz; arama gibi davranabilir.
        if (path.IndexOfAny(InvalidNameChars) >= 0)
            return BackgroundSourceError.InvalidCharacters;

        // "." ve ".." parçaları, çift ters eğik çizgi, sondaki nokta/boşluk: Windows bunları sessizce
        // normalleştirir, yani kullanıcının gördüğü yol ile açılan yol ayrışır.
        var segments = path[3..].Split('\\');
        if (segments.Any(IsNonCanonicalSegment))
            return BackgroundSourceError.NonCanonicalPath;

        if (segments.Any(IsReservedDeviceName))
            return BackgroundSourceError.ReservedDeviceName;

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
        var lastSeparator = path.LastIndexOf('\\');
        if (lastDot < 0 || lastDot < lastSeparator || lastDot == path.Length - 1)
            return null;
        return path[lastDot..];
    }

    private static bool IsNonCanonicalSegment(string segment) =>
        segment.Length == 0 || segment is "." or ".." || segment[^1] is '.' or ' ';

    /// <summary>
    /// Yol parçasının ilk noktaya kadarki kısmı aygıt adıysa (NUL.png, com1.gif) true. Klasör adları da sayılır:
    /// Windows aygıt adlı klasörlü yolları (C:\nul\a.png) aygıt olarak çözer. Sondaki boşluklar yok sayılır;
    /// "CONsole.png" aygıt değildir.
    /// </summary>
    private static bool IsReservedDeviceName(string segment)
    {
        var stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return DeviceNames.Contains(stem);
    }

    /// <summary>
    /// Sürücü harfli ve kanonik (yalnız ters eğik çizgili) yerel yol mu. Platformdan bağımsız, saf metin denetimi.
    /// </summary>
    private static bool IsAbsoluteWindowsPath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\' && path.IndexOf('/') < 0;
}
