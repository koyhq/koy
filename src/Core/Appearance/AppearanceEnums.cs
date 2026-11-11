namespace Koy.Core.Appearance;

// Bu dosyadaki enum adları settings.json'a yazılır: bir kez yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Tema seçimi. "System" Windows uygulama temasını izler.</summary>
public enum AppTheme
{
    System,
    Dark,
    Light
}

/// <summary>Malzeme seviyesi: Katı (opak), Buzlu (%86) ya da Cam (%72).</summary>
public enum MaterialLevel
{
    Solid,
    Frosted,
    Glass
}

/// <summary>Animasyon hızı. <see cref="Off"/> geçişleri anında yapar.</summary>
public enum AnimationSpeed
{
    Calm,
    Normal,
    Fast,
    Off
}
