namespace Koy.Core.Lyrics;

public enum LyricsLookupKind
{
    Found,

    /// <summary>Kaynak yanıt verdi, bu parça için eşzamanlı söz yok.</summary>
    NotFound,

    /// <summary>Geçici sorun (ağ, 429, sunucu): sonuç önbelleğe yazılmaz, sonra yeniden denenir.</summary>
    Failed
}

public readonly record struct LyricsLookup(LyricsLookupKind Kind, string? Lrc = null)
{
    public static LyricsLookup NotFound { get; } = new(LyricsLookupKind.NotFound);
    public static LyricsLookup Failed { get; } = new(LyricsLookupKind.Failed);
    public static LyricsLookup Found(string lrc) => new(LyricsLookupKind.Found, lrc);
}

/// <summary>Bir söz kaynağı (yerel klasör, çevrim içi hizmet). Ham LRC metni döndürür.</summary>
public interface ILyricsSource
{
    Task<LyricsLookup> FindAsync(LyricsQuery query, CancellationToken cancellationToken);
}
