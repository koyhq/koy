namespace Koy.Core.Lyrics;

public enum LyricsLoadStatus
{
    Found,
    NotFound,
    Failed
}

public sealed record LyricsLoadResult(LyricsLoadStatus Status, IReadOnlyList<LyricLine> Lines)
{
    public static LyricsLoadResult NotFound { get; } = new(LyricsLoadStatus.NotFound, []);
    public static LyricsLoadResult Failed { get; } = new(LyricsLoadStatus.Failed, []);
}

public interface ILyricsProvider
{
    Task<LyricsLoadResult> LoadAsync(LyricsQuery query, CancellationToken cancellationToken);
}

/// <summary>Kaynak sırası: yerel dosya, disk önbelleği, çevrim içi hizmet. Yerelde ya da önbellekte varsa ağa çıkılmaz.</summary>
public sealed class LyricsProvider(ILyricsSource local, LyricsCache cache, ILyricsSource online) : ILyricsProvider
{
    public async Task<LyricsLoadResult> LoadAsync(LyricsQuery query, CancellationToken cancellationToken)
    {
        var localResult = await local.FindAsync(query, cancellationToken).ConfigureAwait(false);
        if (localResult is { Kind: LyricsLookupKind.Found, Lrc: { } localLrc } && LrcParser.Parse(localLrc) is { Count: > 0 } localLines)
            return new LyricsLoadResult(LyricsLoadStatus.Found, localLines);

        var (status, cached) = cache.Read(query);
        if (status == LyricsCacheStatus.KnownMissing)
            return LyricsLoadResult.NotFound;

        if (status == LyricsCacheStatus.Hit && LrcParser.Parse(cached) is { Count: > 0 } cachedLines)
            return new LyricsLoadResult(LyricsLoadStatus.Found, cachedLines);

        var remote = await online.FindAsync(query, cancellationToken).ConfigureAwait(false);
        if (remote.Kind == LyricsLookupKind.Failed)
            return LyricsLoadResult.Failed;

        if (remote is { Kind: LyricsLookupKind.Found, Lrc: { } lrc } && LrcParser.Parse(lrc) is { Count: > 0 } lines)
        {
            cache.Write(query, lrc);
            return new LyricsLoadResult(LyricsLoadStatus.Found, lines);
        }

        cache.WriteMissing(query);
        return LyricsLoadResult.NotFound;
    }
}
