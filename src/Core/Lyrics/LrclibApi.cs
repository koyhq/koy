using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Koy.Core.Lyrics;

/// <summary>LRCLIB adresleri ve yanıt çözümleme. Ağ çağrısı Platform'dadır, burası saf ve testlidir.</summary>
public static class LrclibApi
{
    private const string BaseUrl = "https://lrclib.net/api";

    // Oturumun bildirdiği süre saniyeye yuvarlanmış gelebilir; farklı kayıtların (radyo sürümü, canlı) ayrımı için dar tutulur.
    private const double DurationToleranceSeconds = 3;

    public static string GetUrl(LyricsQuery query)
    {
        var url = new StringBuilder(BaseUrl)
            .Append("/get?track_name=").Append(Uri.EscapeDataString(query.Title))
            .Append("&artist_name=").Append(Uri.EscapeDataString(query.Artist));

        // Hizmet 1-3600 saniye dışındaki süreleri kabul etmez.
        if (query.Duration is { TotalSeconds: >= 1 and <= 3600 } duration)
            url.Append("&duration=").Append(((int)Math.Round(duration.TotalSeconds)).ToString(CultureInfo.InvariantCulture));

        return url.ToString();
    }

    public static string SearchUrl(LyricsQuery query) =>
        $"{BaseUrl}/search?track_name={Uri.EscapeDataString(query.Title)}&artist_name={Uri.EscapeDataString(query.PrimaryArtist)}";

    /// <summary>Tek kayıt yanıtındaki eşzamanlı söz; yoksa ya da yanıt bozuksa null.</summary>
    public static string? ParseSynced(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? ReadSynced(document.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Arama sonuçlarından parçaya en yakın eşzamanlı sözü seçer. Süre biliniyorsa toleransı aşan kayıt elenir:
    /// aynı adlı başka bir sürümün sözleri yanlış yerde akardı.
    /// </summary>
    public static string? PickBest(string json, LyricsQuery query)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            string? best = null;
            var bestDifference = double.MaxValue;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || ReadSynced(item) is not { } synced || !TitleMatches(item, query.Title))
                    continue;

                if (query.Duration is not { } duration)
                    return synced;

                if (!item.TryGetProperty("duration", out var value) || value.ValueKind != JsonValueKind.Number)
                    continue;

                var difference = Math.Abs(value.GetDouble() - duration.TotalSeconds);
                if (difference <= DurationToleranceSeconds && difference < bestDifference)
                {
                    best = synced;
                    bestDifference = difference;
                }
            }

            return best;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadSynced(JsonElement element) =>
        element.TryGetProperty("syncedLyrics", out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    // Arama gevşektir; başlığı tutmayan kayıt başka bir şarkıdır.
    private static bool TitleMatches(JsonElement item, string title)
    {
        if (!item.TryGetProperty("trackName", out var value) || value.ValueKind != JsonValueKind.String)
            return false;

        var candidate = Comparable(value.GetString());
        var wanted = Comparable(title);
        return candidate.Length > 0 && wanted.Length > 0
            && (candidate.Contains(wanted, StringComparison.Ordinal) || wanted.Contains(candidate, StringComparison.Ordinal));
    }

    private static string Comparable(string? text) =>
        string.Concat((text ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
