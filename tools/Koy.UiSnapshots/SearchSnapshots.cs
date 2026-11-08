using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Koy.App.Features.Search;
using Koy.Core.Search;

namespace Koy.UiSnapshots;

/// <summary>Arama panelini sahte sonuçlarla çentik boyutunda çizer. Gerçek kaynak ve Windows çağrısı yoktur.</summary>
internal static class SearchSnapshots
{
    private const double PanelWidth = 572;
    private const double PanelHeight = 330;

    private sealed class FakeSource(params SearchResult[] results) : SearchSource("fake", "Fake")
    {
        public override IEnumerable<SearchResult> Search(SearchQuery query) => query.Folded == "?" ? [] : results;
    }

    private static SearchResult App(string title, string category = "Uygulamalar", double relevance = 0.9) =>
        new("app:" + title, category, title, () => { }) { Glyph = SearchGlyph.App, Relevance = relevance };

    public static void Render(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> render)
    {
        void Shoot(string name, SearchViewModel vm, Action? afterLayout = null)
        {
            var panel = new SearchPanel { DataContext = vm, Width = PanelWidth, Height = PanelHeight };
            var host = new Border { Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0B, 0x0D)), Padding = new Thickness(24, 10, 24, 12), Child = panel };
            host.Measure(new Size(PanelWidth + 48, PanelHeight + 22));
            host.Arrange(new Rect(0, 0, PanelWidth + 48, PanelHeight + 22));
            host.UpdateLayout();
            Pump();
            afterLayout?.Invoke();
            host.UpdateLayout();
            Pump();
            host.UpdateLayout();
            render(host, PanelWidth + 48, PanelHeight + 22, Path.Combine(outDir, $"{prefix}-{theme}-arama-{name}.png"));
        }

        var mixed = new SearchResult[]
        {
            new("calc", "Hesap makinesi", "96", () => { })
            {
                Glyph = SearchGlyph.Calculator,
                CategoryRelevance = SearchRelevance.High,
                Actions = [new SearchAction("copy", "Kopyala", () => { })]
            },
            App("Not Defteri"),
            App("Notion", relevance: 0.8),
            new("path:a", "Belgeler", "not-listesi.docx", () => { })
            {
                Subtitle = @"C:\Users\kullanici\Belgeler",
                Glyph = SearchGlyph.File,
                CategoryRelevance = SearchRelevance.Low,
                Actions = [new SearchAction("folder", "Klasörü aç", () => { })]
            },
            new("session:shutdown", "Oturum", "Bilgisayarı kapat", () => { })
            {
                Glyph = SearchGlyph.Power,
                CategoryRelevance = SearchRelevance.Low,
                RequiresConfirmation = true
            }
        };

        // 1. Boş panel
        var empty = Create(new FakeSource());
        empty.Open(null);
        Shoot("bos", empty);

        // 2. Karışık sonuçlar, ilk satırın eylemi görünür
        var results = Create(new FakeSource(mixed));
        results.Open(null);
        results.QueryText = "not";
        Shoot("sonuclar", results);

        // 3. Tab ile eylem vurgulu
        var action = Create(new FakeSource(mixed));
        action.Open(null);
        action.QueryText = "not";
        action.CycleAction(1);
        Shoot("eylem", action);

        // 4. Kapat komutu onay bekliyor
        var confirm = Create(new FakeSource(mixed[^1]));
        confirm.Open(null);
        confirm.QueryText = "kapat";
        confirm.Activate(firstAction: false);
        Shoot("onay", confirm);

        // 5. Taşan liste: çentiğe sığmayan satırlara kaydırılır
        var many = Create(new FakeSource(Enumerable.Range(1, 15).Select(i => App("Uygulama " + i, relevance: 1 - i * 0.01)).ToArray()));
        many.Open(null);
        many.QueryText = "uygulama";
        Shoot("tasma", many, () =>
        {
            for (var i = 0; i < 9; i++)
                many.MoveSelection(1);
        });
    }

    private static SearchViewModel Create(ISearchSource source) =>
        new(new SearchPanelController([source]), Dispatcher.CurrentDispatcher, pinned: false, _ => { });

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Thread.Sleep(30);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }
    }
}
