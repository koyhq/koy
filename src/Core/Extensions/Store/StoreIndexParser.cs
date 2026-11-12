using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Koy.Core.Extensions.Store;

/// <summary>
/// <c>store/index.json</c> ayrıştırıcısı (docs/EXTENSION-STORE.md). Bütün indeks reddedilir: yanlış biçim adı,
/// büyük biçim sürümü, 1 MB üstü, 500 öğe üstü, bozuk JSON. Tek bir öğe bozuksa yalnız o atlanır ve sayılır;
/// aynı kimlik ikinci kez gelirse ikincisi atlanır. Bilinmeyen alanlar yok sayılır. Asla fırlatmaz.
/// </summary>
public static class StoreIndexParser
{
    public const string FormatName = "koy-store-index";
    public const int SupportedFormatVersion = 1;
    public const int MaxBytes = 1024 * 1024;
    public const int MaxItems = 500;

    public static StoreIndexResult Parse(string? json)
    {
        if (json is null)
        {
            return Fail(StoreIndexError.NotAStoreIndex);
        }

        // Bir karakter en çok 3 bayttır: dev metin kodlanmadan reddedilir.
        if (json.Length > MaxBytes || Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            return Fail(StoreIndexError.TooLarge);
        }

        return Parse(Encoding.UTF8.GetBytes(json));
    }

    public static StoreIndexResult Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > MaxBytes)
        {
            return Fail(StoreIndexError.TooLarge);
        }

        // UTF-8 BOM atlanır (Windows editörleri ekler); JsonDocument kabul etmez.
        if (utf8.Length >= 3 && utf8[0] == 0xEF && utf8[1] == 0xBB && utf8[2] == 0xBF)
        {
            utf8 = utf8[3..];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        }
        catch (JsonException)
        {
            return Fail(StoreIndexError.MalformedJson);
        }

        using (document)
        {
            return ParseRoot(document.RootElement);
        }
    }

    private static StoreIndexResult ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Fail(StoreIndexError.NotAStoreIndex);
        }

        if (!TryString(root, "format", out var format) || format != FormatName)
        {
            return Fail(StoreIndexError.NotAStoreIndex);
        }

        if (!root.TryGetProperty("formatVersion", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var formatVersion)
            || formatVersion < 1)
        {
            return Fail(StoreIndexError.NotAStoreIndex);
        }

        if (formatVersion > SupportedFormatVersion)
        {
            return Fail(StoreIndexError.UnsupportedFormatVersion);
        }

        if (!root.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return Fail(StoreIndexError.NotAStoreIndex);
        }

        if (itemsElement.GetArrayLength() > MaxItems)
        {
            return Fail(StoreIndexError.TooManyItems);
        }

        DateTimeOffset? generated = null;
        if (TryString(root, "generated", out var generatedText)
            && DateTimeOffset.TryParse(generatedText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var g))
        {
            generated = g;
        }

        var items = new List<StoreItem>(itemsElement.GetArrayLength());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var skipped = 0;
        foreach (var element in itemsElement.EnumerateArray())
        {
            if (!TryParseItem(element, out var item) || !seen.Add(item!.Id))
            {
                skipped++;
                continue;
            }

            items.Add(item);
        }

        return new StoreIndexResult(StoreIndexError.None, new StoreIndex(generated, items.AsReadOnly()), skipped);
    }

    private static bool TryParseItem(JsonElement element, out StoreItem? item)
    {
        item = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryString(element, "id", out var id) || !ExtensionManifestValidator.IsValidId(id)
            || !TryString(element, "name", out var name)
            || !ExtensionManifestValidator.IsValidText(name, ExtensionManifestValidator.MaxNameLength)
            || !TryString(element, "kind", out var kindText) || !ExtensionKinds.TryParse(kindText, out var kind)
            || !TryString(element, "version", out var version) || !ExtensionManifestValidator.IsValidVersion(version)
            || !TryString(element, "author", out var author)
            || !ExtensionManifestValidator.IsValidText(author, ExtensionManifestValidator.MaxAuthorLength)
            || !TryString(element, "license", out var license) || !StoreFieldRules.IsValidLicense(license)
            || !element.TryGetProperty("summary", out var summaryElement)
            || !TryLocalized(summaryElement, StoreFieldRules.MaxSummaryLength, allowNewlines: false, allowEmpty: false, out var summary)
            || !TryString(element, "updated", out var updatedText)
            || !DateOnly.TryParseExact(updatedText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var updated))
        {
            return false;
        }

        // Uzun açıklama isteğe bağlı; var ama bozuksa öğe bozuktur.
        var description = LocalizedText.Empty;
        if (element.TryGetProperty("description", out var descriptionElement))
        {
            if (!TryLocalized(descriptionElement, StoreFieldRules.MaxDescriptionLength, allowNewlines: true, allowEmpty: true, out var text))
            {
                return false;
            }

            description = text!;
        }

        // Dolu ama https olmayan adres öğeyi bozuk yapar (tıklanabilir bağlantı olacak).
        string? homepage = null;
        if (element.TryGetProperty("homepage", out var homepageElement) && homepageElement.ValueKind != JsonValueKind.Null)
        {
            if (!TryString(element, "homepage", out homepage) || !StoreFieldRules.IsHttpsUrl(homepage))
            {
                return false;
            }
        }

        long downloads = 0;
        if (element.TryGetProperty("downloads", out var downloadsElement) && downloadsElement.ValueKind != JsonValueKind.Null)
        {
            if (downloadsElement.ValueKind != JsonValueKind.Number || !downloadsElement.TryGetInt64(out downloads) || downloads < 0)
            {
                return false;
            }
        }

        if (!element.TryGetProperty("package", out var packageElement) || packageElement.ValueKind != JsonValueKind.Object
            || !TryString(packageElement, "url", out var packageUrl) || !StoreUrlPolicy.IsTrustedAssetUrl(packageUrl)
            || !TryString(packageElement, "sha256", out var packageHash) || !StoreFieldRules.IsSha256(packageHash)
            || !packageElement.TryGetProperty("size", out var sizeElement) || sizeElement.ValueKind != JsonValueKind.Number
            || !sizeElement.TryGetInt64(out var size) || size <= 0 || size > PackageEntryPolicy.MaxDownloadBytes)
        {
            return false;
        }

        // Önizleme isteğe bağlı: bozuksa öğe değil yalnız önizleme düşer (güvensiz adres hiç kullanılmaz).
        StorePreview? preview = null;
        if (element.TryGetProperty("preview", out var previewElement) && previewElement.ValueKind == JsonValueKind.Object
            && TryString(previewElement, "url", out var previewUrl) && StoreUrlPolicy.IsTrustedAssetUrl(previewUrl)
            && TryString(previewElement, "sha256", out var previewHash) && StoreFieldRules.IsSha256(previewHash))
        {
            preview = new StorePreview(previewUrl!, previewHash!.ToLowerInvariant());
        }

        item = new StoreItem(
            id!, name!, kind, version!, author!, license!, summary!, description, homepage, updated, downloads,
            new StorePackage(packageUrl!, packageHash!.ToLowerInvariant(), size), preview);
        return true;
    }

    /// <summary>En çok bu kadar dil (kötü niyetli dev nesne ayrıştırılmaz).</summary>
    private const int MaxLanguages = 32;

    // Düz metin ya da { "dil": "metin" } nesnesi. Nesne biçiminde "en" zorunlu; dil kodu iki küçük harf; her değer
    // alanın düz metin kuralından geçer. Yinelenen dil ya da boş nesne bozuktur.
    private static bool TryLocalized(JsonElement element, int maxLength, bool allowNewlines, bool allowEmpty, out LocalizedText? text)
    {
        text = null;
        if (element.ValueKind == JsonValueKind.String)
        {
            var plain = element.GetString();
            if (!StoreFieldRules.IsPlainText(plain, maxLength, allowNewlines, allowEmpty))
            {
                return false;
            }

            text = LocalizedText.Plain(plain!);
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var code = property.Name;
            if (values.Count >= MaxLanguages
                || code.Length != 2 || !char.IsAsciiLetterLower(code[0]) || !char.IsAsciiLetterLower(code[1])
                || property.Value.ValueKind != JsonValueKind.String
                || property.Value.GetString() is not { } value
                || !StoreFieldRules.IsPlainText(value, maxLength, allowNewlines, allowEmpty)
                || !values.TryAdd(code, value))
            {
                return false;
            }
        }

        if (!values.ContainsKey(LocalizedText.FallbackLanguage))
        {
            return false;
        }

        text = new LocalizedText(values);
        return true;
    }

    // Alan yoksa ya da metin değilse false (null/sayı bozuk sayılır, sessizce metne çevrilmez).
    private static bool TryString(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (!obj.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return value is not null;
    }

    private static StoreIndexResult Fail(StoreIndexError error) => new(error, null);
}
