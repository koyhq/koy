namespace Koy.Core.Extensions;

/// <summary>
/// Eklentinin kendini tanıttığı veri. Kod içermez; yalnız eklentinin ne olduğunu ve hangi yetkilere
/// ihtiyaç duyduğunu anlatır. Bu modelin oluşturulması hiçbir şeyi çalıştırmaz.
/// Dışarıdan tek geçerli yol <see cref="ExtensionManifestValidator.TryCreate"/>'tir: kurucu içeridedir ve
/// özellikler yalnız okunur, bu yüzden doğrulanmamış bir kimlik ya da kopya (<c>with</c>) oluşamaz.
/// </summary>
public sealed record ExtensionManifest
{
    internal ExtensionManifest(
        string id,
        string name,
        string version,
        string author,
        IReadOnlyList<ExtensionCapability> capabilities,
        ExtensionEntryKind entryKind)
    {
        Id = id;
        Name = name;
        Version = version;
        Author = author;

        // Kopya + salt okunur sarmalayıcı: çağıran sonradan değiştiremez, tüketici de diziye dönüştürüp yazamaz.
        // Sıralı tutulur: aynı yetki kümesi hangi sırayla yazılırsa yazılsın eşit sayılır.
        Capabilities = Array.AsReadOnly(capabilities.Order().ToArray());
        EntryKind = entryKind;
    }

    public string Id { get; }

    public string Name { get; }

    public string Version { get; }

    public string Author { get; }

    public IReadOnlyList<ExtensionCapability> Capabilities { get; }

    public ExtensionEntryKind EntryKind { get; }

    /// <summary>
    /// Yetki listesi dizi olarak tutulduğu için varsayılan eşitlik referansa bakar. Bu yüzden içerik
    /// karşılaştırması elle yazılır: aynı alanlara sahip iki manifest eşittir.
    /// </summary>
    public bool Equals(ExtensionManifest? other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id
            && Name == other.Name
            && Version == other.Version
            && Author == other.Author
            && EntryKind == other.EntryKind
            && Capabilities.SequenceEqual(other.Capabilities);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id, StringComparer.Ordinal);
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Version, StringComparer.Ordinal);
        hash.Add(Author, StringComparer.Ordinal);
        hash.Add(EntryKind);
        foreach (var capability in Capabilities)
        {
            hash.Add(capability);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Eklentinin istediği yetkiler. Küme kapalıdır: listede olmayan bir yetki manifestte reddedilir.
/// Yeni yetki eklemek, kullanıcının kuruluma onay vermesi gereken bir karardır.
/// </summary>
public enum ExtensionCapability
{
    Network,
    Files,
    Clipboard,
}

/// <summary>
/// Eklentinin türü. Şimdilik yalnız veri paketi (tema, simge paketi, dil) var. Dış süreçte çalışan
/// eklenti türü proje sahibinin kararını bekler (AGENTS #4), bu yüzden burada yoktur.
/// </summary>
public enum ExtensionEntryKind
{
    Data,
}
