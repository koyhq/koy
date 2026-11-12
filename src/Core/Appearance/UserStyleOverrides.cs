using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Koy.Core.Appearance;

/// <summary>Bir kullanıcı stil girdisinin değer türü. Her izinli token'ın türü sabittir; dosya türü seçemez.</summary>
public enum UserStyleKind
{
    /// <summary>SolidColorBrush kaynağı: "#RRGGBB" ya da "#AARRGGBB".</summary>
    Brush,

    /// <summary>Color kaynağı: "#RRGGBB" ya da "#AARRGGBB".</summary>
    Color,

    /// <summary>double kaynağı: sonlu, token'ın aralığında sayı.</summary>
    Number,

    /// <summary>CornerRadius kaynağı: "a" ya da "sol-üst,sağ-üst,sağ-alt,sol-alt".</summary>
    CornerRadius,

    /// <summary>Thickness kaynağı: "a" ya da "sol,üst,sağ,alt".</summary>
    Thickness,

    /// <summary>FontFamily kaynağı: düz yazı tipi adı (ya da virgülle ayrılmış yedek listesi).</summary>
    FontFamily
}

/// <summary>İzinli bir kaynak anahtarı. <paramref name="Min"/> ve <paramref name="Max"/> yalnız sayı, köşe ve kalınlık türlerinde anlamlıdır.</summary>
public sealed record UserStyleToken(string Key, UserStyleKind Kind, double Min = 0, double Max = 0);

/// <summary>ARGB renk (WPF'siz). "#RRGGBB" için A = 255.</summary>
public readonly record struct UserStyleColor(byte A, byte R, byte G, byte B);

/// <summary>
/// Dört kenarlı değer. Köşe yarıçapında sıra sol-üst, sağ-üst, sağ-alt, sol-alt; kalınlıkta sol, üst, sağ, alt
/// (WPF'in kendi sıraları). Tek sayı verilince dördü de aynıdır.
/// </summary>
public readonly record struct UserStyleQuad(double First, double Second, double Third, double Fourth)
{
    public static UserStyleQuad Uniform(double value) => new(value, value, value, value);
}

/// <summary>
/// Doğrulanmış kullanıcı stil değeri. Yalnız <see cref="UserStyleParser"/> üretir; türe göre ilgili alan doludur,
/// diğerleri varsayılandır. App bunu donmuş WPF nesnelerine çevirir (Core WPF bilmez).
/// </summary>
public sealed record UserStyleValue(
    UserStyleKind Kind,
    UserStyleColor Color = default,
    double Number = 0,
    UserStyleQuad Quad = default,
    string FontFamily = "")
{
    public static UserStyleValue ForBrush(UserStyleColor color) => new(UserStyleKind.Brush, Color: color);
    public static UserStyleValue ForColor(UserStyleColor color) => new(UserStyleKind.Color, Color: color);
    public static UserStyleValue ForNumber(double number) => new(UserStyleKind.Number, Number: number);
    public static UserStyleValue ForCornerRadius(UserStyleQuad quad) => new(UserStyleKind.CornerRadius, Quad: quad);
    public static UserStyleValue ForThickness(UserStyleQuad quad) => new(UserStyleKind.Thickness, Quad: quad);
    public static UserStyleValue ForFontFamily(string name) => new(UserStyleKind.FontFamily, FontFamily: name);
}

/// <summary>Bir girdinin neden yok sayıldığının makine okunur karşılığı (App bunu yerelleştirir).</summary>
public enum UserStyleDiagnosticCode
{
    InvalidJson,
    NotAnObject,
    FileTooLarge,
    FileUnreadable,
    TooManyEntries,
    UnknownToken,
    DuplicateToken,
    WrongValueType,
    ValueTooLong,
    BadColor,
    BadNumber,
    BadQuad,
    BadFontFamily,
    OutOfRange
}

/// <param name="Key">Anahtar adı (kırpılmış, denetim karakterleri temizlenmiş); dosya düzeyi sorunlarda "(file)".</param>
/// <param name="Reason">Kısa İngilizce açıklama (günlük ve tanı için). Arayüz <paramref name="Code"/>'dan yerelleştirir.</param>
public sealed record UserStyleDiagnostic(string Key, string Reason, UserStyleDiagnosticCode Code = UserStyleDiagnosticCode.InvalidJson);

/// <param name="OmittedDiagnostics">Rapor sınırı yüzünden listeye alınmayan tanı sayısı.</param>
public sealed record UserStyleParseResult(
    IReadOnlyDictionary<string, UserStyleValue> ValidEntries,
    IReadOnlyList<UserStyleDiagnostic> Diagnostics,
    int OmittedDiagnostics = 0)
{
    public static UserStyleParseResult Empty { get; } = new(
        new Dictionary<string, UserStyleValue>().ToFrozenDictionary(), []);
}

/// <summary>
/// Güvenlik sınırı: kullanıcı stil dosyasında yalnız bu listedeki anahtarlar geçersiz kılınabilir. Liste
/// Theme.Dark.xaml, Theme.Light.xaml ve Tokens.xaml'deki fırça, renk, sayı, köşe ve yazı tipi anahtarlarından
/// türer (tests/Core.Tests/Appearance/UserStyleOverridesTests.cs XAML dosyalarıyla eşitliği denetler).
/// Hiçbiri pencere ya da tuval boyutu değildir (AGENTS #13). XAML, yol, tip adı ya da kod burada yoktur.
/// </summary>
public static class AllowedTokens
{
    // Tema fırçaları (Koyu ve Açık aynı anahtarları taşır).
    private static readonly string[] ThemeBrushes =
    [
        "KoyBgWindow", "KoyBgShell", "KoyBgRow", "KoyBgControl", "KoyBgSegment", "KoyBgHover", "KoyBgActive",
        "KoyBgNavHover", "KoyBgNavActive", "KoyBorderHairline", "KoyBorderShell", "KoyBorderStrong", "KoyBorderControl",
        "KoyTextPrimary", "KoyTextSecondary", "KoyTextMuted", "KoyTextDisabled", "KoyAccent", "KoyAccentHover",
        "KoyAccentPressed", "KoyOnAccent", "KoyAccentText", "KoyFocusRing", "KoyAccentSubtle", "KoyBgInfo", "KoyFgInfo",
        "KoyDangerText", "KoyDangerBorder", "KoyDangerBg", "KoyScrim", "KoyStatusOk", "KoyStatusWarn",
        "KoyToggleTrackOff", "KoyToggleKnobOff", "KoyToggleKnobOn"
    ];

    // Tokens.xaml fırçaları: bağlam menüsü, kaydırma çubuğu ve çentik (temadan bağımsız koyu yüzey).
    private static readonly string[] TokenBrushes =
    [
        "KoyMenuBg", "KoyMenuBorder", "KoyMenuText", "KoyMenuTextMuted", "KoyMenuTextDisabled", "KoyMenuHover", "KoyMenuSeparator",
        "KoyScrollThumb", "KoyScrollThumbHover", "KoyScrollThumbDrag",
        "KoyNotchText", "KoyNotchTextSecondary", "KoyNotchTextOnLight", "KoyNotchHover", "KoyNotchPressed", "KoyNotchActive",
        "KoyNotchTextMuted", "KoyNotchDivider", "KoyNotchSurface", "KoyNotchCard", "KoyNotchChip", "KoyNotchMuted",
        "KoyNotchDim", "KoyNotchOk", "KoyNotchAccentHome", "KoyNotchAccentFocus", "KoyNotchAccentShelf",
        "KoyNotchAccentClipboard", "KoyNotchAccentSystem", "KoyNotchAccentControl", "KoyNotchMusicCard",
        "KoyNotchMusicCover", "KoyNotchMusicSub", "KoyNotchMusicSpectrum", "KoyNotchWeatherCard", "KoyNotchWeatherSoft",
        "KoyNotchWeatherIconBg", "KoyNotchFocusCard", "KoyNotchFocusTrack", "KoyNotchFocusButton", "KoyNotchFocusSoft",
        "KoyNotchShelfCard", "KoyNotchShelfChip", "KoyNotchShelfChipText", "KoyNotchShelfSoft", "KoyNotchShelfDash",
        "KoyNotchControlCard", "KoyNotchControlTrack", "KoyNotchControlSoft", "KoyNotchControlTile", "KoyNotchBrightness",
        "KoyNotchControlBattery", "KoyNotchControlIconBg", "KoyNotchRowHover", "KoyNotchRowError",
        "KoyNotchAccentCalendar", "KoyNotchCalendarCard", "KoyNotchCalendarChip", "KoyNotchCalendarSoft",
        "KoyNotchClipCard", "KoyNotchClipChip", "KoyNotchClipChipText", "KoyNotchClipSoft",
        "KoyNotchSystemCard", "KoyNotchSystemTrack", "KoyNotchSystemSoft", "KoyNotchNetCard", "KoyNotchNetSoft",
        "KoyNotchDash", "KoyNotchCheckBorder", "KoyNotchDoneText"
    ];

    private static readonly FrozenDictionary<string, UserStyleToken> ByKey = Build();

    /// <summary>Bütün izinli tokenlar (anahtar → tanım).</summary>
    public static IReadOnlyDictionary<string, UserStyleToken> All => ByKey;

    public static bool TryGet(string key, out UserStyleToken token)
    {
        if (key is not null && ByKey.TryGetValue(key, out var found))
        {
            token = found;
            return true;
        }

        token = null!;
        return false;
    }

    private static FrozenDictionary<string, UserStyleToken> Build()
    {
        var list = new List<UserStyleToken>();

        foreach (var key in ThemeBrushes)
            list.Add(new UserStyleToken(key, UserStyleKind.Brush));
        foreach (var key in TokenBrushes)
            list.Add(new UserStyleToken(key, UserStyleKind.Brush));

        list.Add(new UserStyleToken("KoyShadowColor", UserStyleKind.Color));

        list.Add(new UserStyleToken("KoyFont", UserStyleKind.FontFamily));
        list.Add(new UserStyleToken("KoyFontMono", UserStyleKind.FontFamily));
        list.Add(new UserStyleToken("KoyNotchFont", UserStyleKind.FontFamily));
        list.Add(new UserStyleToken("KoyClockFont", UserStyleKind.FontFamily));

        // Yazı boyutları (DIP): okunur kalsın diye dar aralık.
        foreach (var key in new[]
                 {
                     "KoyFontPageTitle", "KoyFontSubtitle", "KoyFontSection", "KoyFontRowTitle", "KoyFontBody",
                     "KoyFontControl", "KoyFontCaption", "KoyFontKey"
                 })
            list.Add(new UserStyleToken(key, UserStyleKind.Number, 8, 40));

        // Boşluk ölçeği (DIP).
        foreach (var key in new[] { "KoySpace1", "KoySpace2", "KoySpace3", "KoySpace4", "KoySpace5", "KoySpace6" })
            list.Add(new UserStyleToken(key, UserStyleKind.Number, 0, 64));

        // Köşe yarıçapları.
        foreach (var key in new[]
                 {
                     "KoyRadiusWindow", "KoyRadiusWindowTop", "KoyRadiusWindowBottomLeft", "KoyRadiusRow",
                     "KoyRadiusControl", "KoyRadiusSmall"
                 })
            list.Add(new UserStyleToken(key, UserStyleKind.CornerRadius, 0, 32));

        // Ölçüler (DIP).
        list.Add(new UserStyleToken("KoyControlHeight", UserStyleKind.Number, 20, 56));
        list.Add(new UserStyleToken("KoyRowMinHeight", UserStyleKind.Number, 32, 96));
        list.Add(new UserStyleToken("KoyNavItemHeight", UserStyleKind.Number, 24, 56));
        list.Add(new UserStyleToken("KoyIconSize", UserStyleKind.Number, 10, 32));
        list.Add(new UserStyleToken("KoyIconSizeLarge", UserStyleKind.Number, 12, 40));
        list.Add(new UserStyleToken("KoyIconStroke", UserStyleKind.Number, 0.5, 4));

        return list.ToFrozenDictionary(t => t.Key, StringComparer.Ordinal);
    }
}

/// <summary>
/// Kullanıcı stil dosyasının (user-theme.json) kısıtlı ayrıştırıcısı. Saf fonksiyon, asla fırlatmaz.
/// Yalnız System.Text.Json kullanır: XAML okunmaz, tip adı çözülmez, yansıma yoktur. Bilinmeyen anahtar, bozuk değer
/// ve bozuk JSON tanı olarak raporlanır; geçerli girdiler yine uygulanır.
/// </summary>
public static class UserStyleParser
{
    /// <summary>Dosya boyutu üst sınırı (UTF-8 bayt).</summary>
    public const int MaxBytes = 64 * 1024;

    /// <summary>Okunan en çok girdi sayısı (bilinmeyenler dahil); fazlası yok sayılır.</summary>
    public const int MaxEntries = 512;

    /// <summary>Raporlanan en çok tanı; fazlası yalnız sayılır.</summary>
    public const int MaxDiagnostics = 64;

    /// <summary>Tek bir değerin en çok uzunluğu (karakter).</summary>
    public const int MaxValueLength = 128;

    /// <summary>Yazı tipi adı üst sınırı (karakter).</summary>
    public const int MaxFontFamilyLength = 96;

    /// <summary>Dosya düzeyi tanılarda anahtar yerine yazılır.</summary>
    public const string FileKey = "(file)";

    private const int MaxEchoedKeyLength = 64;
    private const int MaxJsonDepth = 4;

    public static UserStyleParseResult Parse(string jsonContent) => Parse(jsonContent, AllowedTokens.All);

    /// <summary>İzinli token tablosunu dışarıdan alan aşırı yükleme (testler ve ileride başka kümeler için).</summary>
    public static UserStyleParseResult Parse(string? jsonContent, IReadOnlyDictionary<string, UserStyleToken> tokens)
    {
        var diagnostics = new List<UserStyleDiagnostic>();
        var omitted = 0;

        void Report(string key, UserStyleDiagnosticCode code, string reason)
        {
            // Dosya düzeyi tanı (en çok bir tane) sınırdan bağımsız yazılır: "çok girdi" uyarısı kaybolmasın.
            if (diagnostics.Count < MaxDiagnostics || key == FileKey)
                diagnostics.Add(new UserStyleDiagnostic(Echo(key), reason, code));
            else
                omitted++;
        }

        UserStyleParseResult Finish(Dictionary<string, UserStyleValue> valid) =>
            new(valid.ToFrozenDictionary(StringComparer.Ordinal), diagnostics, omitted);

        var entries = new Dictionary<string, UserStyleValue>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            Report(FileKey, UserStyleDiagnosticCode.InvalidJson, "File is empty or not valid JSON.");
            return Finish(entries);
        }

        // Boyut sınırı ayrıştırmadan önce: dev girdi için JsonDocument bile kurulmaz. Kaba üst sınır uzunluktan:
        // bir karakter en çok 3 UTF-8 bayttır (yedek çiftleri 2 karakter / 4 bayt).
        if (jsonContent.Length > MaxBytes || Encoding.UTF8.GetByteCount(jsonContent) > MaxBytes)
        {
            Report(FileKey, UserStyleDiagnosticCode.FileTooLarge, $"File is larger than {MaxBytes / 1024} KB.");
            return Finish(entries);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonContent, new JsonDocumentOptions
            {
                MaxDepth = MaxJsonDepth,
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            Report(FileKey, UserStyleDiagnosticCode.InvalidJson, "File is empty or not valid JSON.");
            return Finish(entries);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Report(FileKey, UserStyleDiagnosticCode.NotAnObject, "The top level must be a JSON object of key-value pairs.");
                return Finish(entries);
            }

            // Yalnız izinli (ve bu yüzden sınırlı sayıdaki) anahtarlar izlenir; bilinmeyenler için küme büyümez.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var count = 0;

            foreach (var property in root.EnumerateObject())
            {
                if (++count > MaxEntries)
                {
                    Report(FileKey, UserStyleDiagnosticCode.TooManyEntries, $"More than {MaxEntries} entries; the rest were ignored.");
                    break;
                }

                var key = property.Name;
                if (!tokens.TryGetValue(key, out var token))
                {
                    Report(key, UserStyleDiagnosticCode.UnknownToken, "Unknown key; only theme resource keys are allowed.");
                    continue;
                }

                if (!seen.Add(key))
                {
                    Report(key, UserStyleDiagnosticCode.DuplicateToken, "Duplicate key; the first value is used.");
                    continue;
                }

                UserStyleValue? value = null;
                var failure = TryReadText(property.Value, token, out var text) ?? TryParseValue(token, text, out value);
                if (failure is { } f)
                    Report(key, f.Code, f.Reason);
                else
                    entries[key] = value!;
            }
        }

        return Finish(entries);
    }

    /// <summary>
    /// Tek bir değer metnini token'ın türüne göre doğrular. Hata varsa kodu ve nedeni, yoksa null döner ve
    /// <paramref name="value"/> dolar. Dışarıdan çağrılabilir (testler her türü doğrudan dener).
    /// </summary>
    public static (UserStyleDiagnosticCode Code, string Reason)? TryParseValue(UserStyleToken token, string text, out UserStyleValue? value)
    {
        value = null;
        text = text.Trim();

        switch (token.Kind)
        {
            case UserStyleKind.Brush:
            case UserStyleKind.Color:
                if (!TryParseColor(text, out var color))
                    return (UserStyleDiagnosticCode.BadColor, "Expected a color like #RRGGBB or #AARRGGBB.");
                value = token.Kind == UserStyleKind.Brush ? UserStyleValue.ForBrush(color) : UserStyleValue.ForColor(color);
                return null;

            case UserStyleKind.Number:
                if (!TryParseNumber(text, out var number))
                    return (UserStyleDiagnosticCode.BadNumber, "Expected a finite number.");
                if (number < token.Min || number > token.Max)
                    return (UserStyleDiagnosticCode.OutOfRange, $"Value must be between {Format(token.Min)} and {Format(token.Max)}.");
                value = UserStyleValue.ForNumber(Normalize(number));
                return null;

            case UserStyleKind.CornerRadius:
            case UserStyleKind.Thickness:
                var quadFailure = TryParseQuad(token, text, out var quad);
                if (quadFailure is not null)
                    return quadFailure;
                value = token.Kind == UserStyleKind.CornerRadius ? UserStyleValue.ForCornerRadius(quad) : UserStyleValue.ForThickness(quad);
                return null;

            case UserStyleKind.FontFamily:
                if (!IsPlainFontFamily(text))
                    return (UserStyleDiagnosticCode.BadFontFamily, "Expected a plain font name (letters, digits, spaces, - _ . , ( )).");
                value = UserStyleValue.ForFontFamily(text);
                return null;

            default:
                return (UserStyleDiagnosticCode.WrongValueType, "Unsupported value kind.");
        }
    }

    // JSON değerini metne indirger. Sayı yalnız sayısal türlerde (Number, CornerRadius, Thickness) kabul edilir;
    // nesne, dizi, true/false/null hiçbir türde geçmez.
    private static (UserStyleDiagnosticCode Code, string Reason)? TryReadText(JsonElement element, UserStyleToken token, out string text)
    {
        text = string.Empty;
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                text = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number when token.Kind is UserStyleKind.Number or UserStyleKind.CornerRadius or UserStyleKind.Thickness:
                text = element.GetRawText();
                break;

            default:
                return (UserStyleDiagnosticCode.WrongValueType, "Wrong value type for this key.");
        }

        if (text.Length > MaxValueLength)
            return (UserStyleDiagnosticCode.ValueTooLong, $"Value is longer than {MaxValueLength} characters.");

        return null;
    }

    private static bool TryParseColor(string text, out UserStyleColor color)
    {
        color = default;
        if (text.Length is not (7 or 9) || text[0] != '#')
            return false;

        for (var i = 1; i < text.Length; i++)
        {
            if (!char.IsAsciiHexDigit(text[i]))
                return false;
        }

        var offset = 1;
        byte alpha = 255;
        if (text.Length == 9)
        {
            alpha = ReadByte(text, 1);
            offset = 3;
        }

        color = new UserStyleColor(alpha, ReadByte(text, offset), ReadByte(text, offset + 2), ReadByte(text, offset + 4));
        return true;
    }

    private static byte ReadByte(string text, int index) =>
        byte.Parse(text.AsSpan(index, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    // Kültürden bağımsız: "1,5" Türkçe ortamda da aynı sonucu verir (virgül burada ayraç, ondalık değil).
    private static bool TryParseNumber(string text, out double number) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    private static (UserStyleDiagnosticCode Code, string Reason)? TryParseQuad(UserStyleToken token, string text, out UserStyleQuad quad)
    {
        quad = default;
        var parts = text.Split(',');
        if (parts.Length is not (1 or 4))
            return (UserStyleDiagnosticCode.BadQuad, "Expected one number (\"a\") or four (\"a,b,c,d\").");

        Span<double> numbers = stackalloc double[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!TryParseNumber(parts[i].Trim(), out var n))
                return (UserStyleDiagnosticCode.BadQuad, "Expected one number (\"a\") or four (\"a,b,c,d\").");
            if (n < token.Min || n > token.Max)
                return (UserStyleDiagnosticCode.OutOfRange, $"Each value must be between {Format(token.Min)} and {Format(token.Max)}.");
            numbers[i] = Normalize(n);
        }

        quad = parts.Length == 1
            ? UserStyleQuad.Uniform(numbers[0])
            : new UserStyleQuad(numbers[0], numbers[1], numbers[2], numbers[3]);
        return null;
    }

    // Düz ad: yol, URI, "{...}" işaretlemesi, "<...>" etiketi ve denetim karakterleri geçmez. Virgülle ayrılmış
    // yedek listesine izin verilir (varsayılan KoyFont da böyle), her parça boş olmamalı.
    private static bool IsPlainFontFamily(string text)
    {
        if (text.Length == 0 || text.Length > MaxFontFamilyLength)
            return false;

        var segmentLength = 0;
        foreach (var c in text)
        {
            if (c == ',')
            {
                if (segmentLength == 0)
                    return false;
                segmentLength = 0;
                continue;
            }

            if (char.IsControl(c))
                return false;

            var allowed = char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' or '(' or ')';
            if (!allowed)
                return false;

            if (c != ' ')
                segmentLength++;
        }

        return segmentLength > 0;
    }

    // -0 aralığı geçer ama kanonik değerde "-0" kalmasın.
    private static double Normalize(double n) => n == 0 ? 0 : n;

    private static string Format(double n) => n.ToString("0.##", CultureInfo.InvariantCulture);

    // Rapora giden anahtar kırpılır ve denetim karakterleri temizlenir: tanı listesi dosyadaki içerikle şişirilemez
    // ve satır sonu / kaçış dizisi taşıyamaz.
    private static string Echo(string key)
    {
        if (key.Length == 0)
            return string.Empty;

        var length = Math.Min(key.Length, MaxEchoedKeyLength);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = char.IsControl(key[i]) ? '?' : key[i];
        return new string(chars);
    }
}
