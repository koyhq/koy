using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Koy.App.Resources;
using Koy.Core.Clipboard;
using Koy.Core.Dock;
using Koy.Core.Search;
using Koy.Core.Settings;
using Koy.Core.Shell;
using Koy.Platform.Search;
using Koy.Platform.Shell;

namespace Koy.App.Features.Search;

/// <summary>Arama panelini ayarlara göre kurar; kapatılan kaynaklar hiç oluşturulmaz.</summary>
public static class SearchFactory
{
    private const int MaxRecentDocuments = 100;

    private static string T(string key) => Strings.Get(key);

    private static IReadOnlyList<string> Keywords(string key) => T(key).Split(';', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Ayarlar sayfasındaki anahtarlar için: kimlik ve ad.</summary>
    public static IReadOnlyList<(string Id, string Name)> SourceCatalog { get; } =
    [
        ("apps", "SearchCatApps"),
        ("windows", "SearchCatWindows"),
        ("calculator", "SearchCatCalculator"),
        ("session", "SearchCatSession"),
        ("shell", "SearchCatShell"),
        ("locations", "SearchCatLocations"),
        ("places", "SearchCatPlaces"),
        ("recent", "SearchCatRecent"),
        ("files", "SearchCatFiles"),
        ("web", "SearchCatWeb"),
        ("bookmarks", "SearchCatBookmarks"),
        ("kill", "SearchCatProcesses"),
        ("clipboard", "SearchCatClipboard"),
        ("datetime", "SearchCatDateTime"),
        ("help", "SearchCatHelp")
    ];

    public static AppSearchSource CreateAppSource(IShellActionService shellActions) => new(
        new InstalledAppsService(),
        app => shellActions.Launch(InstalledAppsService.ShellPath(app)),
        T,
        app => IconHelper.GetIconBytes(InstalledAppsService.ShellPath(app)))
    {
        Syntaxes = [new SearchSyntax(T("SearchExampleApp"), T("SearchSyntaxApps"))]
    };

    public static SearchViewModel Create(
        AppSettings settings,
        AppSearchSource apps,
        IWindowTracker windows,
        IClipboardHistoryService? clipboard,
        Dispatcher dispatcher,
        Action<bool> savePinned)
    {
        var shell = new SearchShell(CopyText);
        var sources = new List<ISearchSource>();
        WebShortcutSearchSource? web = null;

        void Add(ISearchSource source)
        {
            if (!settings.Search.DisabledSources.Contains(source.Id))
                sources.Add(source);
        }

        Add(apps);
        Add(new WindowSearchSource(windows.GetOpenWindows, windows.ActivateWindow, T)
        {
            Syntaxes = [new SearchSyntax(T("SearchExampleWindow"), T("SearchSyntaxWindows"))]
        });
        Add(new CalculatorSearchSource(shell, T)
        {
            Syntaxes =
            [
                new SearchSyntax("12*8", T("SearchSyntaxCalc")),
                new SearchSyntax("sqrt(16)+1", T("SearchSyntaxCalcFunctions")),
                new SearchSyntax("hex=255", T("SearchSyntaxCalcBase"))
            ]
        });
        Add(new SessionSearchSource(new SystemCommandService(), SessionEntries(), T)
        {
            Syntaxes = [new SearchSyntax(Keywords("SearchKwLock")[0], T("SearchSyntaxSession"))]
        });
        Add(new ShellCommandSearchSource(shell, T)
        {
            Syntaxes = [new SearchSyntax("ipconfig /all", T("SearchSyntaxShell"))]
        });
        Add(new LocationSearchSource(shell, T)
        {
            Syntaxes = [new SearchSyntax(@"C:\Windows", T("SearchSyntaxLocations")), new SearchSyntax("https://github.com", T("SearchSyntaxLocationsUrl"))]
        });
        Add(new PlacesSearchSource(() => SearchPlaces.Load(T), shell, T)
        {
            Syntaxes = [new SearchSyntax(T("SearchKwPlaces"), T("SearchSyntaxPlaces"))]
        });
        Add(new RecentDocumentsSearchSource(RecentDocuments, shell, T)
        {
            Syntaxes = [new SearchSyntax(T("SearchExampleDocument"), T("SearchSyntaxRecent"))]
        });
        Add(new FileSearchSource(new FileSearchService(), shell, T)
        {
            Syntaxes = [new SearchSyntax(T("SearchExampleDocument"), T("SearchSyntaxFiles"))]
        });

        web = new WebShortcutSearchSource(WebShortcutSearchSource.Defaults, shell, T, Koy.Platform.Search.DefaultBrowserLocator.Find)
        {
            Syntaxes = WebShortcutSearchSource.Defaults
                .Select(s => new SearchSyntax(s.Key + ":" + T("SearchExampleWeb"), string.Format(T("SearchSyntaxWeb"), s.Provider)))
                .Append(new SearchSyntax("!gg " + T("SearchExampleWeb"), T("SearchSyntaxWebBang")))
                .ToList()
        };
        Add(web);

        Add(new BookmarksSearchSource(SearchBookmarks.Load, shell, T)
        {
            Syntaxes = [new SearchSyntax(T("SearchKwBookmarks"), T("SearchSyntaxBookmarks"))]
        });
        Add(new KillSearchSource(SearchProcesses.List, p => SearchProcesses.Terminate(p, windows.TerminateProcess), SearchProcesses.RequestClose, T)
        {
            Syntaxes = [new SearchSyntax(T("SearchKwKill") + " notepad", T("SearchSyntaxKill"))]
        });
        if (clipboard is not null)
        {
            Add(new ClipboardSearchSource(clipboard, T)
            {
                Syntaxes = [new SearchSyntax(T("SearchExampleClipboard"), T("SearchSyntaxClipboard"))]
            });
        }

        Add(new DateTimeSearchSource(shell, T)
        {
            Syntaxes = [new SearchSyntax(Keywords("SearchKwDate")[0], T("SearchSyntaxDate")), new SearchSyntax(Keywords("SearchKwTime")[0], T("SearchSyntaxTime"))]
        });
        Add(new HelpSearchSource(() => sources, T));

        var history = new SearchHistory(JsonListFile<string>.InSettingsFolder("search-history.txt"));
        var fallback = sources.Contains(web) ? web.Fallback : (Func<SearchQuery, SearchResult?>?)null;
        var panel = new SearchPanelController(
            sources,
            action => dispatcher.InvokeAsync(action),
            history,
            fallback,
            T("SearchRecentQueries"),
            T("SearchActRemove"))
        {
            RetainPriorSearch = settings.Search.RetainPrior,
            HistoryMode = settings.Search.HistoryMode
        };

        return new SearchViewModel(panel, dispatcher, settings.Search.Pinned, savePinned);
    }

    /// <summary>Kayıtlı arama geçmişini siler (Ayarlar).</summary>
    public static void ClearHistory() => new SearchHistory(JsonListFile<string>.InSettingsFolder("search-history.txt")).Clear();

    private static IReadOnlyList<SystemCommandEntry> SessionEntries() =>
    [
        new(SystemCommand.Lock, T("SearchCmdLock"), Keywords("SearchKwLock")),
        new(SystemCommand.Sleep, T("SearchCmdSleep"), Keywords("SearchKwSleep")),
        new(SystemCommand.SignOut, T("SearchCmdSignOut"), Keywords("SearchKwSignOut")),
        new(SystemCommand.SwitchUser, T("SearchCmdSwitchUser"), Keywords("SearchKwSwitchUser")),
        new(SystemCommand.Restart, T("SearchCmdRestart"), Keywords("SearchKwRestart")),
        new(SystemCommand.Shutdown, T("SearchCmdShutdown"), Keywords("SearchKwShutdown"))
    ];

    private static IReadOnlyList<string> RecentDocuments() =>
        new RecentFilesService().GetRecent(MaxRecentDocuments).Select(f => f.OpenPath).ToList();

    private static void CopyText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException)
        {
            // Pano o anda başka bir uygulamada kilitli.
        }
    }
}

/// <summary>Satır simgesi yoksa kullanılan çizgi ikon.</summary>
public sealed class SearchGlyphConverter : IValueConverter
{
    private static readonly Dictionary<SearchGlyph, Geometry> Drawn = new()
    {
        [SearchGlyph.Calculator] = Parse("M6 2h12a2 2 0 0 1 2 2v16a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-16a2 2 0 0 1 2 -2zM8 6h8M16 14v4M16 10h.01M12 10h.01M8 10h.01M12 14h.01M8 14h.01M12 18h.01M8 18h.01"),
        [SearchGlyph.Terminal] = Parse("M4 17l6 -6l-6 -6M12 19h8"),
        [SearchGlyph.Folder] = Parse("M20 20a2 2 0 0 0 2 -2v-10a2 2 0 0 0 -2 -2h-8l-2 -2h-6a2 2 0 0 0 -2 2v12a2 2 0 0 0 2 2z"),
        [SearchGlyph.File] = Parse("M15 2h-9a2 2 0 0 0 -2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2 -2v-13zM14 2v4a2 2 0 0 0 2 2h4"),
        [SearchGlyph.Globe] = Parse("M22 12a10 10 0 1 1 -20 0a10 10 0 0 1 20 0zM2 12h20M12 2a15.3 15.3 0 0 1 4 10a15.3 15.3 0 0 1 -4 10a15.3 15.3 0 0 1 -4 -10a15.3 15.3 0 0 1 4 -10z"),
        [SearchGlyph.Bookmark] = Parse("M19 21l-7 -5l-7 5v-16a2 2 0 0 1 2 -2h10a2 2 0 0 1 2 2z"),
        [SearchGlyph.Clipboard] = Parse("M9 2h6a1 1 0 0 1 1 1v2a1 1 0 0 1 -1 1h-6a1 1 0 0 1 -1 -1v-2a1 1 0 0 1 1 -1zM16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2h2"),
        [SearchGlyph.Clock] = Parse("M22 12a10 10 0 1 1 -20 0a10 10 0 0 1 20 0zM12 6v6l4 2"),
        [SearchGlyph.Help] = Parse("M22 12a10 10 0 1 1 -20 0a10 10 0 0 1 20 0zM9.09 9a3 3 0 0 1 5.83 1c0 2 -3 3 -3 3M12 17h.01"),
        [SearchGlyph.History] = Parse("M3 12a9 9 0 1 0 9 -9a9.75 9.75 0 0 0 -6.74 2.74l-2.26 2.26M3 3v5h5M12 7v5l4 2")
    };

    private static Geometry Parse(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SearchGlyph glyph)
            return null;

        if (Drawn.TryGetValue(glyph, out var drawn))
            return drawn;

        var key = glyph switch
        {
            SearchGlyph.Power => "IconPower",
            SearchGlyph.Process => "IconGauge",
            SearchGlyph.Window => "IconLayers",
            _ => "IconAppWindow"
        };
        return Application.Current?.TryFindResource(key) as Geometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
