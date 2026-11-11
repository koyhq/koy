namespace Koy.Core.Settings;

// Enum adları settings.json'a yazılır: bir kez yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Güncelleme kanalı. Eski dosyalarda "Kararlı" / "Önizleme" metniydi (dile bağlı); göçte ada çevrilir.</summary>
public enum UpdateChannel
{
    Stable,
    Preview
}
