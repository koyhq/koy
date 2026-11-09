using System.Net;
using System.Net.Http;
using Koy.Core.Lyrics;
using Koy.Platform.Net;

namespace Koy.Platform.Lyrics;

/// <summary>LRCLIB çevrim içi kaynağı. Önce tam eşleşme, bulunamazsa arama denenir.</summary>
public sealed class LrclibSource(HttpClient? httpClient = null) : ILyricsSource
{
    private readonly HttpClient _http = httpClient ?? AppHttp.Client;

    public async Task<LyricsLookup> FindAsync(LyricsQuery query, CancellationToken cancellationToken)
    {
        // Sanatçısız sorgu hizmette zorunlu alanı boş bırakır ve başka şarkının sözünü getirme riski taşır.
        if (query.Artist.Length == 0)
            return LyricsLookup.NotFound;

        try
        {
            using (var exact = await _http.GetAsync(LrclibApi.GetUrl(query), cancellationToken).ConfigureAwait(false))
            {
                if (exact.IsSuccessStatusCode)
                {
                    var json = await exact.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    if (LrclibApi.ParseSynced(json) is { } synced)
                        return LyricsLookup.Found(synced);
                }
                else if (exact.StatusCode != HttpStatusCode.NotFound)
                {
                    return LyricsLookup.Failed;
                }
            }

            using var search = await _http.GetAsync(LrclibApi.SearchUrl(query), cancellationToken).ConfigureAwait(false);
            if (!search.IsSuccessStatusCode)
                return LyricsLookup.Failed;

            var results = await search.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return LrclibApi.PickBest(results, query) is { } best ? LyricsLookup.Found(best) : LyricsLookup.NotFound;
        }
        catch (HttpRequestException)
        {
            return LyricsLookup.Failed;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // İstemci zaman aşımı; bizim iptalimiz değil.
            return LyricsLookup.Failed;
        }
    }
}
