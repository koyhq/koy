namespace Koy.Core.Dock;

// Bu dosyadaki enum adları settings.json'a yazılır: bir kez yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Çalışan uygulama göstergesi: nokta (varsayılan) ya da çizgi.</summary>
public enum DockIndicatorStyle
{
    Dot,
    Line,
    Highlight
}

/// <summary>Dock simgesine orta tık: uygulamanın yeni penceresini aç (varsayılan) ya da pencereyi kapat.</summary>
public enum MiddleClickAction
{
    NewWindow,
    Close
}
