namespace Koy.Core.Extensions;

/// <summary>Kurulu bir eklenti ve kullanıcının açma/kapama tercihi. Kapalı eklenti başlatılmaz (AGENTS #5).</summary>
public sealed record InstalledExtension(ExtensionManifest Manifest, bool Enabled);

/// <summary>Kurulu eklenti listesi işleminin hata nedeni. Hata yoksa <see cref="None"/>.</summary>
public enum InstalledExtensionListError
{
    None,
    TooManyExtensions,
    DuplicateId,
    UnknownId,
}

/// <summary>
/// Kurulu eklenti listesinin saf kuralları: sıralama, tekil kimlik, üst sınır ve açma/kapama.
/// Dosya okumaz, yazmaz ve hiçbir eklentiyi başlatmaz.
/// </summary>
public static class InstalledExtensionList
{
    /// <summary>Üst sınır. Her eklenti ayrı bir değerlendirme ve bellek maliyeti getirir.</summary>
    public const int MaxInstalled = 64;

    /// <summary>
    /// Girdiyi kimliğe göre (ordinal) sıralı bir listeye çevirir. Aynı kimlik iki kez gelirse ya da sınır
    /// aşılırsa reddeder. Sıralama girdinin sırasından bağımsızdır, bu yüzden sonuç her zaman aynıdır.
    /// </summary>
    public static bool TryBuild(
        IEnumerable<InstalledExtension> entries,
        out IReadOnlyList<InstalledExtension> list,
        out InstalledExtensionListError error)
    {
        list = [];

        // En çok MaxInstalled + 1 öğe okunur: sınır aşılınca numaralandırma durur, fazlası kopyalanmaz.
        var items = new List<InstalledExtension>();
        foreach (var entry in entries)
        {
            if (items.Count == MaxInstalled)
            {
                error = InstalledExtensionListError.TooManyExtensions;
                return false;
            }

            items.Add(entry);
        }

        // Kimlikler doğrulamadan geçtiği için büyük/küçük harf farkı yoktur; ordinal karşılaştırma yeterli.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!seen.Add(item.Manifest.Id))
            {
                error = InstalledExtensionListError.DuplicateId;
                return false;
            }
        }

        list = items.OrderBy(item => item.Manifest.Id, StringComparer.Ordinal).ToList().AsReadOnly();
        error = InstalledExtensionListError.None;
        return true;
    }

    /// <summary>
    /// Yalnız verilen kimliğin açık/kapalı durumunu değiştirir ve yeni bir liste döndürür. Girdi liste
    /// değişmez. Kimlik yoksa <see cref="InstalledExtensionListError.UnknownId"/> döner.
    /// </summary>
    public static bool TrySetEnabled(
        IReadOnlyList<InstalledExtension> list,
        string id,
        bool enabled,
        out IReadOnlyList<InstalledExtension> result,
        out InstalledExtensionListError error)
    {
        var index = -1;
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Manifest.Id, id, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            result = list;
            error = InstalledExtensionListError.UnknownId;
            return false;
        }

        // Kimlik değişmediği için sıralama korunur.
        var copy = list.ToList();
        copy[index] = list[index] with { Enabled = enabled };
        result = copy;
        error = InstalledExtensionListError.None;
        return true;
    }
}
