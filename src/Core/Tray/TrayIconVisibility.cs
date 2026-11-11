namespace Koy.Core.Tray;

// Bu dosyadaki enum adları settings.json'a yazılır: yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Tepsi simgesinin dock'taki yeri: doğrudan gösterilir, taşma menüsünde durur ya da hiç gösterilmez.</summary>
public enum TrayIconVisibility
{
    Shown,
    Overflow,
    Hidden
}
