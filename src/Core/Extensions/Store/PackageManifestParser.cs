using System.Text;
using System.Text.Json;

namespace Koy.Core.Extensions.Store;

/// <summary>
/// Paketin manifest.json'u: mevcut <see cref="ExtensionManifest"/> (kimlik, ad, sürüm, yazar, yetkiler) artı
/// mağazaya özgü <see cref="Kind"/> ve <see cref="License"/>. <see cref="ExtensionManifest"/>'e dokunulmaz:
/// o kayıt dışarıdan yalnız doğrulayıcıyla kurulur, bu sarmalayıcı onu içerir.
/// </summary>
public sealed record PackageManifest(ExtensionManifest Manifest, ExtensionKind Kind, string License);

/// <param name="ManifestError">Hata <see cref="ExtensionInstallError.InvalidManifest"/> iken doğrulayıcının ilk hatalı alanı.</param>
public sealed record PackageManifestResult(
    ExtensionInstallError Error, PackageManifest? Manifest, ExtensionManifestError ManifestError = ExtensionManifestError.None)
{
    public bool IsValid => Error == ExtensionInstallError.None && Manifest is not null;
}

/// <summary>
/// manifest.json okuma ve indeks öğesiyle eşleştirme. Veri paketleri yetki istemez: boş olmayan yetki listesi
/// <see cref="ExtensionInstallError.CapabilitiesNotAllowed"/> ile reddedilir. Asla fırlatmaz.
/// </summary>
public static class PackageManifestParser
{
    public static PackageManifestResult Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > PackageEntryPolicy.MaxManifestBytes)
        {
            return new(ExtensionInstallError.FileTooLarge, null);
        }

        if (utf8.Length >= 3 && utf8[0] == 0xEF && utf8[1] == 0xBB && utf8[2] == 0xBF)
        {
            utf8 = utf8[3..];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException)
        {
            return Invalid(ExtensionManifestError.None);
        }

        using (document)
        {
            return ParseRoot(document.RootElement);
        }
    }

    public static PackageManifestResult Parse(string? json) =>
        json is null ? Invalid(ExtensionManifestError.None) : Parse(Encoding.UTF8.GetBytes(json));

    private static PackageManifestResult ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Invalid(ExtensionManifestError.None);
        }

        // Yetki listesi yoksa boş sayılır (doğrulayıcının kuralı); varsa metin dizisi olmalı.
        List<string?>? capabilities = null;
        if (root.TryGetProperty("capabilities", out var capabilityElement) && capabilityElement.ValueKind != JsonValueKind.Null)
        {
            if (capabilityElement.ValueKind != JsonValueKind.Array)
            {
                return Invalid(ExtensionManifestError.UnknownCapability);
            }

            capabilities = [];
            foreach (var item in capabilityElement.EnumerateArray())
            {
                // Sınırı aşan liste doğrulayıcıda reddedilir; fazlası kopyalanmaz.
                if (capabilities.Count > ExtensionManifestValidator.MaxCapabilities)
                {
                    break;
                }

                capabilities.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
            }
        }

        // entryKind verilmezse "data": bu mağaza yalnız veri paketi alır.
        var entryKind = root.TryGetProperty("entryKind", out _) ? StringOf(root, "entryKind") : "data";

        if (!ExtensionManifestValidator.TryCreate(
                StringOf(root, "id"), StringOf(root, "name"), StringOf(root, "version"), StringOf(root, "author"),
                capabilities, entryKind, out var manifest, out var manifestError))
        {
            return Invalid(manifestError);
        }

        if (!ExtensionKinds.TryParse(StringOf(root, "kind"), out var kind) || !StoreFieldRules.IsValidLicense(StringOf(root, "license")))
        {
            return Invalid(ExtensionManifestError.None);
        }

        if (manifest!.Capabilities.Count > 0)
        {
            return new(ExtensionInstallError.CapabilitiesNotAllowed, null);
        }

        return new(ExtensionInstallError.None, new PackageManifest(manifest, kind, StringOf(root, "license")!));
    }

    /// <summary>
    /// Paketteki kimlik, sürüm ve tür indeksteki öğeyle birebir aynı mı. Ad, yazar ve lisans farkı hata değildir
    /// (indeks özeti elle yazılır), ama kimlik/sürüm/tür farkı başka bir paketin sahte etiketle gelmesidir.
    /// </summary>
    public static ExtensionInstallError Match(PackageManifest package, StoreItem item) =>
        package.Manifest.Id == item.Id && package.Manifest.Version == item.Version && package.Kind == item.Kind
            ? ExtensionInstallError.None
            : ExtensionInstallError.ManifestMismatch;

    private static string? StringOf(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static PackageManifestResult Invalid(ExtensionManifestError error) =>
        new(ExtensionInstallError.InvalidManifest, null, error);
}
