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

    private List<string>? _entries;

    public IReadOnlyList<string> Entries => Load();

    public void Add(string term)
    {
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
                ? File.ReadAllLines(filePath).Where(l => l.Length > 0).Take(MaxEntries).ToList()
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

            File.WriteAllLines(filePath, _entries ?? []);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Geçmiş yazılamasa da arama çalışır.
        }
    }
}
