namespace Koy.Core.Search;

/// <summary>Kategorinin önemi; listedeki grup sırasını belirler. Grup içi sıra <see cref="SearchResult.Relevance"/> ile.</summary>
public static class SearchRelevance
{
    public const int Lowest = 0;
    public const int Low = 30;
    public const int Moderate = 50;
    public const int High = 70;
    public const int Highest = 100;
}

/// <summary>Kategorinin listedeki yeri; önemden önce gelir: uygulamalar hep üstte, dosyalar hep altta.</summary>
public enum SearchCategoryPlacement
{
    Top,
    Normal,
    Bottom
}

public enum SearchGlyph
{
    App,
    Window,
    Calculator,
    Terminal,
    Power,
    Folder,
    File,
    Globe,
    Bookmark,
    Process,
    Clipboard,
    Clock,
    Help,
    History
}

/// <summary>Satırın ek eylemi (ör. "Kopyala", "Bulunduğu klasörü aç").</summary>
public sealed record SearchAction(string Id, string Label, Action Execute);

/// <summary>Listede bir satır.</summary>
public sealed record SearchResult(string Id, string Category, string Title, Action Execute)
{
    public string? Subtitle { get; init; }

    public SearchCategoryPlacement Placement { get; init; } = SearchCategoryPlacement.Normal;

    public int CategoryRelevance { get; init; } = SearchRelevance.Moderate;

    public double Relevance { get; init; } = 0.5;

    public SearchGlyph Glyph { get; init; } = SearchGlyph.App;

    public IReadOnlyList<SearchAction> Actions { get; init; } = [];

    /// <summary>İlk Enter yalnız sorar, ikinci Enter çalıştırır (kapat, yeniden başlat).</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Panel kapanınca odak önceki pencereye dönsün: kopyalanan sonucu oraya yapıştırmak için.</summary>
    public bool RestoresFocus { get; init; }

    /// <summary>Doluysa Enter panel kapatmaz, sorguyu bu metinle değiştirir (hesap sonucu, yardım).</summary>
    public string? ReplacesQuery { get; init; }

    /// <summary>Çalıştırılınca arama geçmişine yazılmasın.</summary>
    public bool SkipHistory { get; init; }

    /// <summary>Simge yalnız görünen satırlar için, arka planda çözülür.</summary>
    public Func<byte[]?>? LoadIcon { get; init; }
}

/// <summary>Yardımda gösterilen kullanım örneği.</summary>
public sealed record SearchSyntax(string Example, string Description);

/// <summary>Sonuç kaynağı. Boşta iş yapmaz: <see cref="Prepare"/> panel açılınca, <see cref="Release"/> kapanınca çağrılır.</summary>
public interface ISearchSource
{
    /// <summary>Ayar anahtarı; değişmez.</summary>
    string Id { get; }

    /// <summary>Kullanıcıya görünen ad.</summary>
    string Name { get; }

    IReadOnlyList<SearchSyntax> Syntaxes { get; }

    /// <summary>Sonuç arka planda aranıyor; biraz sonra <see cref="Changed"/> gelecek.</summary>
    bool IsBusy { get; }

    /// <summary>Sonuçlar değişti (ör. uygulama listesi hazır). Arka plan iş parçacığından gelebilir.</summary>
    event EventHandler? Changed;

    void Prepare();

    void Release();

    IEnumerable<SearchResult> Search(SearchQuery query);
}

public abstract class SearchSource(string id, string name) : ISearchSource
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public IReadOnlyList<SearchSyntax> Syntaxes { get; init; } = [];

    public virtual bool IsBusy => false;

    public event EventHandler? Changed;

    public virtual void Prepare()
    {
    }

    public virtual void Release()
    {
    }

    public abstract IEnumerable<SearchResult> Search(SearchQuery query);

    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

public enum SystemCommand
{
    Lock,
    Sleep,
    SignOut,
    SwitchUser,
    Restart,
    Shutdown
}

public interface ISystemCommandService
{
    void Execute(SystemCommand command);
}

/// <summary>Başlat menüsündeki bir uygulama. <see cref="Id"/>: <c>shell:AppsFolder</c> altındaki kimlik.</summary>
public sealed record InstalledApp(string Id, string Name);

public interface IInstalledAppsService
{
    /// <summary>Yavaş olabilir (yaklaşık 1 sn); arka planda çalışır, UI iş parçacığını bekletmez.</summary>
    Task<IReadOnlyList<InstalledApp>> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Windows Arama dizininden bir dosya ya da klasör.</summary>
public sealed record IndexedItem(string Path, bool IsFolder);

public interface IFileSearchService
{
    /// <summary>Dizin kapalıysa boş döner, istisna fırlatmaz.</summary>
    Task<IReadOnlyList<IndexedItem>> SearchAsync(string term, int limit, CancellationToken cancellationToken = default);
}

/// <summary>Bilinen klasör ya da sürücü.</summary>
public sealed record SearchPlace(string Name, string Path, string? Group = null);

public sealed record RunningProcess(int Id, string Name);

public sealed record BrowserBookmark(string Title, string Url);

/// <summary>Sonuçların dışarıya dokunan işleri; Core'daki kaynaklar Windows'a doğrudan bağlanmaz.</summary>
public interface ISearchShell
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    string? GetEnvironmentVariable(string name);

    /// <summary>Dosyayı, klasörü ya da adresi varsayılan uygulamayla açar.</summary>
    void Open(string target);

    void Run(string executable, string? arguments);

    void RunInTerminal(string commandLine);

    void OpenContainingFolder(string path);

    void CopyText(string text);
}
