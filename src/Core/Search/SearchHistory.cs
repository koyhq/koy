namespace Koy.Core.Search;

public enum SearchHistoryMode
{
    /// <summary>Geçmiş tutulmaz.</summary>
    Disabled,

    /// <summary>Yazarken geçmişten gri öneri görünür, sağ ok ya da Tab ile alınır.</summary>
    Suggestion,

    /// <summary>Geçmişten gelen öneri seçili metin olarak yazıya eklenir.</summary>
    ImmediateCompletion
}

/// <summary>Çalıştırılan aramalar; dosyada satır başına bir kayıt, en yenisi başta.</summary>
public sealed class SearchHistory(string filePath)
{
    public const int MaxEntries = 50;

    /// <summary>Listede gösterilen kayıt; fazlası yalnız öneride kullanılır.</summary>
    public const int VisibleEntries = 20;

    /// <summary>Bundan uzun arama geçmişe yazılmaz (yapıştırılmış metin olabilir).</summary>
    public const int MaxEntryLength = 256;

    private List<string>? _entries;

    public IReadOnlyList<string> Entries => Load();

    public void Add(string term)
    {
        // Satır sonu dosyayı bozar (satır başına bir kayıt); çok satırlı metin hiç yazılmaz.
        if (term.Length == 0 || term.Length > MaxEntryLength || SearchCommandSafety.HasControlCharacters(term))
            return;

        var entries = Load();
        if (entries.Count > 0 && entries[0] == term)
            return;

        entries.Remove(term);
        entries.Insert(0, term);
        if (entries.Count > MaxEntries)
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        Save();
    }

    public void RemoveAt(int index)
    {
        var entries = Load();
        if (index < 0 || index >= entries.Count)
            return;

        entries.RemoveAt(index);
        Save();
    }

    public void Clear()
    {
        Load().Clear();
        Save();
    }

    /// <summary>Yazılanla başlayan en yeni kayıt; yoksa null.</summary>
    public string? Suggest(string typed)
    {
        if (typed.Length == 0)
            return null;

        var folded = SearchText.Fold(typed);
        return Load().FirstOrDefault(e => e.Length >= typed.Length && SearchText.Fold(e).StartsWith(folded, StringComparison.Ordinal));
    }

    private List<string> Load()
    {
        if (_entries is not null)
            return _entries;

        try
        {
            _entries = File.Exists(filePath)
                ? File.ReadAllLines(filePath).Where(l => l.Length is > 0 and <= MaxEntryLength && !SearchCommandSafety.HasControlCharacters(l)).Take(MaxEntries).ToList()
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _entries = [];
        }

        return _entries;
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Geçici dosya + taşıma: yarım yazılmış geçmiş kalmaz.
            var temp = filePath + ".tmp";
            File.WriteAllLines(temp, _entries ?? []);
            File.Move(temp, filePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Geçmiş yazılamasa da arama çalışır.
        }
    }
}
