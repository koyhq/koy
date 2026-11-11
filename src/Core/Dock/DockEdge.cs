namespace Koy.Core.Dock;

// Ad settings.json'a yazılır (UseStringEnumConverter): yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ, yalnız yeni değer eklenir.

/// <summary>Dock'un ekranın hangi kenarına oturduğu. Varsayılan alt kenardır (bugünkü davranış).</summary>
public enum DockEdge
{
    Bottom,
    Top,
    Left,
    Right
}
