namespace Koy.Core.Background;

/// <summary>
/// Arka plan türü. Yalnız veri: bu tür dosyayı açmaz, çözmez, oynatmaz.
/// </summary>
public enum BackgroundKind
{
    None,
    StillImage,
    AnimatedImage,
    Video
}

/// <summary>
/// Kullanıcının seçtiği arka plan kaynağı. Yol yalnızca metin olarak saklanır; çalıştırılmaz,
/// okunmaz. Geçerliliği <see cref="BackgroundSourceValidator"/> denetler.
/// </summary>
public sealed record BackgroundSource(BackgroundKind Kind, string Path)
{
    /// <summary>Arka plan yok.</summary>
    public static BackgroundSource None { get; } = new(BackgroundKind.None, string.Empty);
}
