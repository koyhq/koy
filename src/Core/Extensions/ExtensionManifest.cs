namespace Koy.Core.Extensions;

/// <summary>
/// Eklentinin kendini tanıttığı veri. Kod içermez; yalnız eklentinin ne olduğunu ve hangi yetkilere
/// ihtiyaç duyduğunu anlatır. Bu modelin oluşturulması hiçbir şeyi çalıştırmaz.
/// Alanlar <see cref="ExtensionManifestValidator.TryCreate"/> ile doğrulanarak üretilir.
/// </summary>
public sealed record ExtensionManifest(
    string Id,
    string Name,
    string Version,
    string Author,
    IReadOnlyList<ExtensionCapability> Capabilities,
    ExtensionEntryKind EntryKind);

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
