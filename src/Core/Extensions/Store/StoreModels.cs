namespace Koy.Core.Extensions.Store;

/// <summary>İndeksteki paket dosyası: https adresi, SHA256 (küçük harf hex) ve bildirilen boyut.</summary>
public sealed record StorePackage(string Url, string Sha256, long Size);

/// <summary>İsteğe bağlı önizleme görseli.</summary>
public sealed record StorePreview(string Url, string Sha256);

/// <summary>İndeksteki bir eklenti. Alanlar <see cref="StoreIndexParser"/> tarafından doğrulanmıştır.</summary>
public sealed record StoreItem(
    string Id,
    string Name,
    ExtensionKind Kind,
    string Version,
    string Author,
    string License,
    LocalizedText Summary,
    LocalizedText Description,
    string? Homepage,
    DateOnly Updated,
    long Downloads,
    StorePackage Package,
    StorePreview? Preview);

public sealed record StoreIndex(DateTimeOffset? Generated, IReadOnlyList<StoreItem> Items)
{
    public static StoreIndex Empty { get; } = new(null, []);
}

/// <summary>İndeks alma/ayrıştırma hatası. Hata yoksa <see cref="None"/>.</summary>
public enum StoreIndexError
{
    None,
    /// <summary>Ağ hatası ya da zaman aşımı (yalnız istemci üretir).</summary>
    Network,
    TooLarge,
    MalformedJson,
    NotAStoreIndex,
    UnsupportedFormatVersion,
    TooManyItems,
    Cancelled,
}

/// <param name="SkippedItems">Bozuk ya da tekrar eden olduğu için atlanan öğe sayısı.</param>
public sealed record StoreIndexResult(StoreIndexError Error, StoreIndex? Index, int SkippedItems = 0)
{
    public bool IsSuccess => Error == StoreIndexError.None && Index is not null;
}
