using System.Buffers;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Koy.Core.Search;

namespace Koy.Platform.Search;

/// <summary>
/// Hafif çevrimiçi sözlük servisi (İngilizce için <c>dictionaryapi.dev</c>, Türkçe için TDK <c>sozluk.gov.tr</c>).
/// Yalnız kullanıcı sorgu yazdığında tekil istek atar; boşta sıfır ağ ve CPU kullanır.
/// </summary>
public sealed class HttpDictionaryService : IDictionaryService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private static readonly SearchValues<char> TurkishSpecificChars = SearchValues.Create("çğıİöşüÇĞÖŞÜ");

    public async Task<DictionaryEntry?> LookupAsync(string word, CancellationToken cancellationToken = default)
    {
        var trimmed = word.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 64)
            return null;

        var isTurkishUi = string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "tr", StringComparison.OrdinalIgnoreCase);
        var hasTurkishChars = trimmed.AsSpan().IndexOfAny(TurkishSpecificChars) >= 0;

        if (hasTurkishChars || isTurkishUi)
        {
            var tdk = await LookupTdkAsync(trimmed, cancellationToken).ConfigureAwait(false);
            if (tdk is not null || hasTurkishChars)
                return tdk;
        }

        return await LookupEnglishAsync(trimmed, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DictionaryEntry?> LookupEnglishAsync(string word, CancellationToken cancellationToken)
    {
        var escaped = Uri.EscapeDataString(word);
        var url = $"https://api.dictionaryapi.dev/api/v2/entries/en/{escaped}";
        var json = await TryGetStringAsync(url, cancellationToken).ConfigureAwait(false);
        if (json is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var first = doc.RootElement[0];
            var entryWord = first.TryGetProperty("word", out var w) ? w.GetString() ?? word : word;
            var phonetic = first.TryGetProperty("phonetic", out var ph) ? ph.GetString() : null;
            var meanings = new List<DictionaryMeaning>();

            if (first.TryGetProperty("meanings", out var meaningsEl) && meaningsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in meaningsEl.EnumerateArray())
                {
                    var pos = m.TryGetProperty("partOfSpeech", out var posEl) ? posEl.GetString() ?? "" : "";
                    if (!m.TryGetProperty("definitions", out var defsEl) || defsEl.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var d in defsEl.EnumerateArray())
                    {
                        var def = d.TryGetProperty("definition", out var defEl) ? defEl.GetString() : null;
                        if (string.IsNullOrWhiteSpace(def))
                            continue;

                        var example = d.TryGetProperty("example", out var exEl) ? exEl.GetString() : null;
                        meanings.Add(new DictionaryMeaning(pos, def, example));
                        if (meanings.Count >= 6)
                            break;
                    }

                    if (meanings.Count >= 6)
                        break;
                }
            }

            return meanings.Count > 0
                ? new DictionaryEntry(entryWord, phonetic, meanings, $"https://www.merriam-webster.com/dictionary/{escaped}")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<DictionaryEntry?> LookupTdkAsync(string word, CancellationToken cancellationToken)
    {
        var escaped = Uri.EscapeDataString(word);
        var url = $"https://sozluk.gov.tr/gts?ara={escaped}";
        var json = await TryGetStringAsync(url, cancellationToken).ConfigureAwait(false);
        if (json is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var first = doc.RootElement[0];
            if (!first.TryGetProperty("anlamlarListe", out var listEl) || listEl.ValueKind != JsonValueKind.Array)
                return null;

            var entryWord = first.TryGetProperty("madde", out var mEl) ? mEl.GetString() ?? word : word;
            var meanings = new List<DictionaryMeaning>();
            foreach (var item in listEl.EnumerateArray())
            {
                var def = item.TryGetProperty("anlam", out var aEl) ? aEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(def))
                    continue;

                var pos = "";
                if (item.TryGetProperty("ozelliklerListe", out var ozEl) && ozEl.ValueKind == JsonValueKind.Array && ozEl.GetArrayLength() > 0)
                    pos = ozEl[0].TryGetProperty("tam_adi", out var tamEl) ? tamEl.GetString() ?? "" : "";

                string? example = null;
                if (item.TryGetProperty("orneklerListe", out var orEl) && orEl.ValueKind == JsonValueKind.Array && orEl.GetArrayLength() > 0)
                    example = orEl[0].TryGetProperty("ornek", out var exEl) ? exEl.GetString() : null;

                meanings.Add(new DictionaryMeaning(pos, def, example));
                if (meanings.Count >= 6)
                    break;
            }

            return meanings.Count > 0
                ? new DictionaryEntry(entryWord, null, meanings, $"https://sozluk.gov.tr/?ara={escaped}")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string?> TryGetStringAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
