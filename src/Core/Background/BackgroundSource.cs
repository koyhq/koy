namespace Koy.Core.Background;

/// <summary>
/// Arka plan türü. Yalnız veri: bu tür dosyayı açmaz, çözmez, oynatmaz.
/// Değerler kalıcıdır (ayar dosyasına yazılabilir): mevcut değerler değiştirilmez, yeni üye yalnızca sona eklenir.
/// </summary>
public enum BackgroundKind
{
    None = 0,
    StillImage = 1,
    AnimatedImage = 2,
    Video = 3
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
