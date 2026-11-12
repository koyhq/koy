using System.Globalization;
using Koy.Core.Background;

namespace Koy.Core.Notch;

/// <summary>
/// Canlı arka planın nereye uygulanacağı (issue #67). Değerler ayar dosyasına yazılır: mevcutlar değiştirilmez,
/// yeni üye yalnız sona eklenir. Varsayılan <see cref="None"/>: özellik kapalı, hiçbir şey çizilmez ve yüklenmez.
/// </summary>
public enum LiveBackgroundTarget
{
    None = 0,
    Notch = 1,
    MusicCard = 2,
    Both = 3
}

/// <summary>Kaynağın nereden geldiği. Değerler kalıcıdır (ayar dosyası), yeni üye yalnız sona eklenir.</summary>
public enum LiveBackgroundSourceKind
{
    /// <summary>Koy'un yerleşik, dosyasız ambiyans önayarı (renk geçişi, kod ile çizilir).</summary>
    BuiltInAmbient = 0,

    /// <summary>Kullanıcının seçtiği yerel GIF / video / görsel dosyası.</summary>
    LocalFile = 1
}

/// <summary>Arka planın çizileceği yüzey.</summary>
public enum LiveBackgroundSurface
{
    /// <summary>Açık çentiğin bütün zemini.</summary>
    Notch,

    /// <summary>Ana sayfadaki müzik kartının zemini.</summary>
    MusicCard
}

/// <summary>Oynatma kararı. Yalnız <see cref="Animate"/> kare üretir; diğerleri hareketsizdir.</summary>
public enum LiveBackgroundPlaybackMode
{
    /// <summary>Hiçbir şey çizilmez ve bellekte tutulmaz (çentik kapalı / gizli ya da özellik bu yüzeyde kapalı).</summary>
    Off,

    /// <summary>Yalnız sabit ilk kare gösterilir (hareketi azalt / pil tasarrufu).</summary>
    StaticFirstFrame,

    /// <summary>Animasyon ya da video oynar.</summary>
    Animate
}

/// <summary>
/// Yerleşik ambiyans önayarı. Renkler "#RRGGBB" metni: Core WPF'e bağımlı değildir, App fırçayı kendisi kurar.
/// Hepsi koyu tonlardır; ayrıca üstüne koyu perde biner (okunabilirlik).
/// </summary>
/// <param name="Id">Ayar dosyasında saklanan kalıcı kimlik; değiştirilmez.</param>
/// <param name="NameKey">Strings.resx anahtarı.</param>
/// <param name="Colors">Geçişte dönen renkler (en az 2).</param>
/// <param name="CycleSeconds">Bir tam geçiş döngüsünün süresi.</param>
public sealed record LiveBackgroundPreset(string Id, string NameKey, IReadOnlyList<string> Colors, int CycleSeconds);

/// <summary>Yerleşik ambiyans önayarlarının küçük kataloğu.</summary>
public static class LiveBackgroundPresets
{
    public const string DefaultId = "aurora";

    public static IReadOnlyList<LiveBackgroundPreset> All { get; } =
    [
        new("aurora", "LiveBgPresetAurora", ["#0F3A47", "#1E2A5E", "#3B1F5C"], 18),
        new("ember", "LiveBgPresetEmber", ["#3A1410", "#5C2A12", "#2B1033"], 20),
        new("ocean", "LiveBgPresetOcean", ["#0B2545", "#13496B", "#0E6B73"], 16),
        new("dusk", "LiveBgPresetDusk", ["#1F1B4B", "#4A1F4F", "#6B2A3A"], 22),
    ];

    /// <summary>Kimliğe göre önayar; bilinmeyen ya da boş kimlikte varsayılan döner (ayar dosyası elle bozulmuş olabilir).</summary>
    public static LiveBackgroundPreset Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}

/// <summary>Bellek ve çözümleme sınırları. Tuval 620×352 (AGENTS #13) büyütülmez, çözümleme ona kenetlenir.</summary>
public static class LiveBackgroundLimits
{
    /// <summary>Çözümleme genişliği (açık çentiğin genişliği); DecodePixelWidth bu değerdir.</summary>
    public const int DecodePixelWidth = 620;

    /// <summary>GIF için en çok tutulan kare sayısı.</summary>
    public const int MaxGifFrames = 120;

    /// <summary>Çözülmüş karelerin toplam bellek sınırı (bayt). 620×352×4 ≈ 0,87 MB/kare: yaklaşık 55 kare.</summary>
    public const long MaxDecodedBytes = 48L * 1024 * 1024;

    /// <summary>
    /// Kaynağın çözülmemiş (tam çözünürlük, kare başına 4 bayt) toplam boyutu için üst sınır. GIF kareleri düşük sıkıştırmalı
    /// dosyalarda bile çok daha büyük açılabilir; 64 MB'lık bir dosya bile gigabaytlarca kare üretebilir, bu yüzden kare sayısı ve
    /// çözünürlük birlikte sınırlanır.
    /// </summary>
    public const long MaxSourceDecodedBytes = 128L * 1024 * 1024;

    /// <summary>Kaynak çözülebilir mi: ölçüler geçerli ve ham kare belleği sınırın içinde (<see cref="MaxSourceDecodedBytes"/>).</summary>
    public static bool IsSourceWithinBudget(int sourceWidth, int sourceHeight, int frameCount)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || frameCount <= 0)
            return false;
        // double: uç değerlerde long taşması sınırı aşan girdiyi geçerli gösterirdi.
        return (double)sourceWidth * sourceHeight * 4 * frameCount <= MaxSourceDecodedBytes;
    }

    /// <summary>Yerel dosya boyutu sınırı (bayt).</summary>
    public const long MaxFileSizeBytes = 64L * 1024 * 1024;

    public const int MinScrimPercent = 30;
    public const int MaxScrimPercent = 90;
    public const int DefaultScrimPercent = 65;

    /// <summary>Koyu perde yüzdesini [30, 90] aralığına kenetler: perdesiz arka planda yazı okunmaz.</summary>
    public static int ClampScrimPercent(int percent) => Math.Clamp(percent, MinScrimPercent, MaxScrimPercent);

    /// <summary>Perde opaklığı 0–1.</summary>
    public static double ScrimOpacity(int percent) => ClampScrimPercent(percent) / 100.0;

    /// <summary>
    /// Kaynak boyutundan çözümleme boyutu: genişlik en çok 620 (küçük kaynak büyütülmez), oran korunur.
    /// Geçersiz (≤ 0) kaynak (0, 0) döner.
    /// </summary>
    public static (int Width, int Height) DecodeSize(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return (0, 0);

        var width = Math.Min(sourceWidth, DecodePixelWidth);
        var height = (int)Math.Max(1, Math.Round((double)sourceHeight * width / sourceWidth));
        return (width, height);
    }
}

/// <summary>
/// GIF karelerinin sınırlanması: kare sayısı ya da toplam bayt sınırı aşılırsa her N. kare tutulur, atlanan karelerin
/// süresi tutulan kareye eklenir (toplam süre ve hız bozulmaz).
/// </summary>
public static class GifFramePlan
{
    /// <summary>Tarayıcıların yaptığı gibi: 0–1 santisaniyelik gecikme 100 ms sayılır (aksi halde GIF çılgınca hızlı oynar).</summary>
    public static int NormalizeDelayMs(int delayCentiseconds) =>
        delayCentiseconds <= 1 ? 100 : delayCentiseconds * 10;

    /// <summary>Kaç karede bir kare tutulacak (1 = hepsi). Bayt sınırı kare başına 4 bayt (Pbgra32) üzerinden hesaplanır.</summary>
    public static int Stride(int frameCount, int pixelWidth, int pixelHeight)
    {
        if (frameCount <= 0 || pixelWidth <= 0 || pixelHeight <= 0)
            return 1;

        var bytesPerFrame = (long)pixelWidth * pixelHeight * 4;
        var byBytes = (int)Math.Max(1, Math.Min(int.MaxValue, LiveBackgroundLimits.MaxDecodedBytes / bytesPerFrame));
        var cap = Math.Min(LiveBackgroundLimits.MaxGifFrames, byBytes);
        return frameCount <= cap ? 1 : (int)Math.Ceiling((double)frameCount / cap);
    }

    /// <summary>Tutulacak kare sayısı.</summary>
    public static int KeptFrames(int frameCount, int pixelWidth, int pixelHeight)
    {
        if (frameCount <= 0)
            return 0;
        var stride = Stride(frameCount, pixelWidth, pixelHeight);
        return (int)Math.Ceiling((double)frameCount / stride);
    }

    /// <summary>Bu indeksteki kare bir tutulan grubun sonu mu (grubun toplam süresiyle birlikte yayımlanır).</summary>
    public static bool IsGroupEnd(int index, int frameCount, int stride) =>
        index == frameCount - 1 || (index + 1) % Math.Max(1, stride) == 0;
}

/// <summary>Oynatma kararı: ne zaman hareket eder, ne zaman yalnız ilk kare, ne zaman hiç.</summary>
public static class LiveBackgroundPlaybackPolicy
{
    /// <summary>Hedef bu yüzeyde arka plan istiyor mu.</summary>
    public static bool AppliesTo(LiveBackgroundTarget target, LiveBackgroundSurface surface) => (target, surface) switch
    {
        (LiveBackgroundTarget.Both, _) => true,
        (LiveBackgroundTarget.Notch, LiveBackgroundSurface.Notch) => true,
        (LiveBackgroundTarget.MusicCard, LiveBackgroundSurface.MusicCard) => true,
        _ => false
    };

    /// <summary>
    /// Hareket etmeli mi. Yalnız çentik açıkken (Expanded) ve pencere görünürken; hareketi azalt ya da pil tasarrufunda
    /// asla. Mevcut <see cref="BackgroundAnimationPolicy"/> kararına devreder (tek kural kaynağı): açık çentik "boşta
    /// değil", pil tasarrufu açıkken tercih verilmediği için hareket yok. Hedef <see cref="LiveBackgroundTarget.None"/> ise false.
    /// </summary>
    public static bool ShouldAnimate(bool isNotchExpanded, bool isWindowVisible, bool reduceMotion, bool isBatterySaver, LiveBackgroundTarget target)
    {
        if (!Enum.IsDefined(target) || target == LiveBackgroundTarget.None)
            return false;

        var state = new BackgroundPlaybackState(
            IsNotchIdle: !isNotchExpanded,
            BatterySaver: isBatterySaver,
            Fullscreen: false,
            IsNotchVisible: isWindowVisible,
            MotionScale: reduceMotion ? 0.0 : 1.0);

        // Tür fark etmez: ambiyans, GIF ve video aynı kurala bağlıdır (hareketli tür).
        return BackgroundAnimationPolicy.ShouldAnimate(BackgroundKind.AnimatedImage, state);
    }

    /// <summary>
    /// Yüzey için tam karar. Hedef bu yüzeyi içermiyorsa, çentik kapalıysa ya da pencere görünmüyorsa <see cref="LiveBackgroundPlaybackMode.Off"/>
    /// (kare ve çözülmüş bellek serbest bırakılır). Açık ve görünürken hareketi azalt / pil tasarrufu varsa
    /// <see cref="LiveBackgroundPlaybackMode.StaticFirstFrame"/>, yoksa <see cref="LiveBackgroundPlaybackMode.Animate"/>.
    /// </summary>
    public static LiveBackgroundPlaybackMode Decide(
        bool isNotchExpanded, bool isWindowVisible, bool reduceMotion, bool isBatterySaver,
        LiveBackgroundTarget target, LiveBackgroundSurface surface)
    {
        if (!AppliesTo(target, surface) || !isNotchExpanded || !isWindowVisible)
            return LiveBackgroundPlaybackMode.Off;

        return ShouldAnimate(isNotchExpanded, isWindowVisible, reduceMotion, isBatterySaver, target)
            ? LiveBackgroundPlaybackMode.Animate
            : LiveBackgroundPlaybackMode.StaticFirstFrame;
    }
}

/// <summary>Yerel dosya doğrulama sonucu.</summary>
public enum LiveBackgroundFileError
{
    None = 0,
    MissingPath = 1,
    UnsupportedExtension = 2,
    NotLocalPath = 3,
    InvalidPath = 4,
    Empty = 5,
    TooLarge = 6
}

/// <summary>
/// Yerel dosya doğrulayıcı. Dosya sistemine dokunmaz: boyutu çağıran verir. Yol denetimi mevcut
/// <see cref="BackgroundSourceValidator"/>'a devredilir (UNC, URL, göreli yol, aygıt adı, alternatif veri akışı reddi).
/// </summary>
public static class LiveBackgroundFileValidator
{
    /// <summary>Kabul edilen uzantılar (küçük harf, noktalı).</summary>
    public static IReadOnlyList<string> AllowedExtensions { get; } = [".gif", ".mp4", ".webm", ".webp"];

    /// <summary>OpenFileDialog süzgeci ("*.gif;*.mp4;*.webm;*.webp").</summary>
    public static string DialogPattern => string.Join(';', AllowedExtensions.Select(e => "*" + e));

    public const int MaxPathLength = 259;

    private static readonly string[] DeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³",
        "CONIN$", "CONOUT$"
    ];

    private static readonly System.Buffers.SearchValues<char> InvalidNameChars = System.Buffers.SearchValues.Create("<>\"|?*");

    /// <summary>Dosya yolunu ve boyutunu denetler; ilk hatayı döner.</summary>
    public static LiveBackgroundFileError Validate(string? path, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(path))
            return LiveBackgroundFileError.MissingPath;

        var extension = ExtensionOf(path);
        if (extension is null || !AllowedExtensions.Contains(extension))
            return LiveBackgroundFileError.UnsupportedExtension;

        if (path.Length > MaxPathLength)
            return LiveBackgroundFileError.InvalidPath;

        if (path.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format))
            return LiveBackgroundFileError.InvalidPath;

        if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith(@"\\", StringComparison.Ordinal) || !IsAbsoluteWindowsPath(path))
            return LiveBackgroundFileError.NotLocalPath;

        if (path.IndexOf(':', 2) >= 0)
            return LiveBackgroundFileError.InvalidPath;

        if (path.AsSpan().ContainsAny(InvalidNameChars))
            return LiveBackgroundFileError.InvalidPath;

        var segments = path[3..].Split('\\');
        if (segments.Any(IsNonCanonicalSegment) || segments.Any(IsReservedDeviceName))
            return LiveBackgroundFileError.InvalidPath;

        if (fileSizeBytes <= 0)
            return LiveBackgroundFileError.Empty;
        if (fileSizeBytes > LiveBackgroundLimits.MaxFileSizeBytes)
            return LiveBackgroundFileError.TooLarge;

        return LiveBackgroundFileError.None;
    }

    private static bool IsNonCanonicalSegment(string segment) =>
        segment.Length == 0 || segment is "." or ".." || segment[^1] is '.' or ' ';

    private static bool IsReservedDeviceName(string segment)
    {
        var stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return DeviceNames.Contains(stem);
    }

    private static bool IsAbsoluteWindowsPath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\' && path.IndexOf('/') < 0;

    /// <summary>Yolun uzantısı (küçük harf, noktalı); yoksa null.</summary>
    public static string? ExtensionOf(string path)
    {
        var lastDot = path.LastIndexOf('.');
        var lastSeparator = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
        if (lastDot < 0 || lastDot < lastSeparator || lastDot == path.Length - 1)
            return null;
        return path[lastDot..].ToLowerInvariant();
    }
}

/// <summary>Çizilecek içerik türü (ayardan çözülmüş).</summary>
public enum LiveBackgroundContentKind
{
    None = 0,
    Ambient = 1,
    AnimatedImage = 2,
    StillImage = 3,
    Video = 4
}

/// <summary>Çözülmüş seçim: App bunu olduğu gibi çizer, karar vermez.</summary>
/// <param name="Kind">İçerik türü; <see cref="LiveBackgroundContentKind.None"/> ise hiçbir şey çizilmez.</param>
/// <param name="Preset">Yalnız ambiyansta.</param>
/// <param name="FilePath">Yalnız yerel dosyada.</param>
/// <param name="Error">Yerel dosya geçersizse nedeni (arayüzde gösterilir).</param>
public sealed record LiveBackgroundSelection(
    LiveBackgroundContentKind Kind, LiveBackgroundPreset? Preset, string? FilePath, LiveBackgroundFileError Error)
{
    public static LiveBackgroundSelection None { get; } = new(LiveBackgroundContentKind.None, null, null, LiveBackgroundFileError.None);
}

/// <summary>Ayar değerlerinden içerik seçimini çözer (saf, dosya sistemine dokunmaz).</summary>
public static class LiveBackgroundResolver
{
    public static LiveBackgroundSelection Resolve(
        LiveBackgroundTarget target, LiveBackgroundSourceKind source, string? presetId, string? filePath, long fileSizeBytes)
    {
        if (!Enum.IsDefined(target) || target == LiveBackgroundTarget.None)
            return LiveBackgroundSelection.None;

        if (source == LiveBackgroundSourceKind.BuiltInAmbient)
            return new(LiveBackgroundContentKind.Ambient, LiveBackgroundPresets.Find(presetId), null, LiveBackgroundFileError.None);

        if (source != LiveBackgroundSourceKind.LocalFile)
            return LiveBackgroundSelection.None;

        var error = LiveBackgroundFileValidator.Validate(filePath, fileSizeBytes);
        if (error != LiveBackgroundFileError.None)
            return new(LiveBackgroundContentKind.None, null, null, error);

        var kind = LiveBackgroundFileValidator.ExtensionOf(filePath!) switch
        {
            ".gif" => LiveBackgroundContentKind.AnimatedImage,
            ".mp4" or ".webm" => LiveBackgroundContentKind.Video,
            _ => LiveBackgroundContentKind.StillImage
        };
        return new(kind, null, filePath, LiveBackgroundFileError.None);
    }
}
