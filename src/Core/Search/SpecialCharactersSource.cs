using System.Globalization;

namespace Koy.Core.Search;

/// <summary>Özel karakter ve sembol kataloğu; Enter panoya kopyalar, panel dışına sürüklenebilir.</summary>
public sealed class SpecialCharactersSource(ISearchShell shell, Func<string, string> t, CultureInfo? culture = null)
    : SearchSource("chars", t("SearchCatSpecialChars"))
{
    private const int MaxDirectResults = 12;
    private const int MaxKeywordResults = 24;

    private readonly bool _turkish = string.Equals((culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName, "tr", StringComparison.OrdinalIgnoreCase);

    public sealed record SpecialCharEntry(string Symbol, string EnglishName, string TurkishName, string Keywords);

    public static IReadOnlyList<SpecialCharEntry> Catalog { get; } =
    [
        // Oklar / Arrows
        new("→", "Right Arrow", "Sağ Ok", "arrow right ok sag next ileri"),
        new("←", "Left Arrow", "Sol Ok", "arrow left ok sol back geri"),
        new("↑", "Up Arrow", "Yukarı Ok", "arrow up ok yukari"),
        new("↓", "Down Arrow", "Aşağı Ok", "arrow down ok asagi"),
        new("↔", "Left Right Arrow", "Çift Yönlü Ok", "arrow both left right ok cift yonlu"),
        new("↕", "Up Down Arrow", "Dikey Ok", "arrow up down ok dikey"),
        new("⇒", "Right Double Arrow", "Sağ Çift Ok", "arrow double right implies ok cift sag"),
        new("⇐", "Left Double Arrow", "Sol Çift Ok", "arrow double left ok cift sol"),
        new("⇔", "Left Right Double Arrow", "Çift Ok Denk", "arrow double iff ok cift denk"),
        new("↗", "North East Arrow", "Sağ Yukarı Ok", "arrow north east external ok sag yukari"),
        new("↘", "South East Arrow", "Sağ Aşağı Ok", "arrow south east ok sag asagi"),
        new("↩", "Return Arrow", "Geri Dönüş Oku", "arrow return reply enter ok donus"),
        new("↪", "Forward Arrow", "İleri Yön Oku", "arrow forward ok ileri"),

        // Para birimleri / Currencies
        new("₺", "Turkish Lira", "Türk Lirası", "currency money lira tl try para birimi turk lirasi"),
        new("€", "Euro", "Avro", "currency money euro eur avro para birimi"),
        new("$", "Dollar", "Dolar", "currency money dollar usd dolar para birimi"),
        new("£", "Pound Sterling", "Sterlin", "currency money pound gbp sterlin para birimi"),
        new("¥", "Yen / Yuan", "Yen / Yuan", "currency money yen yuan jpy cny para birimi"),
        new("₹", "Indian Rupee", "Hint Rupisi", "currency money rupee inr rupi para birimi"),
        new("₩", "Korean Won", "Kore Wonu", "currency money won krw para birimi"),
        new("₽", "Russian Ruble", "Rus Rublesi", "currency money ruble rub ruble para birimi"),
        new("₿", "Bitcoin", "Bitcoin", "currency crypto bitcoin btc kripto"),
        new("¢", "Cent", "Sent", "currency cent sent para birimi"),

        // Matematik / Math
        new("±", "Plus-Minus", "Artı Eksi", "math plus minus arti eksi"),
        new("×", "Multiplication", "Çarpma", "math multiply times carpma x"),
        new("÷", "Division", "Bölme", "math divide division bolme"),
        new("≠", "Not Equal", "Eşit Değil", "math not equal esit degil"),
        new("≈", "Approximately Equal", "Yaklaşık Eşit", "math approx approximately yaklasik esit"),
        new("≤", "Less Than or Equal", "Küçük Eşit", "math less equal leq kucuk esit"),
        new("≥", "Greater Than or Equal", "Büyük Eşit", "math greater equal geq buyuk esit"),
        new("∞", "Infinity", "Sonsuzluk", "math infinity sonsuz sonsuzluk"),
        new("√", "Square Root", "Karekök", "math sqrt square root karekok"),
        new("∑", "Summation", "Toplam", "math sum summation sigma toplam"),
        new("∏", "Product", "Çarpım", "math product pi carpim"),
        new("∫", "Integral", "İntegral", "math integral"),
        new("∂", "Partial Differential", "Kısmi Türev", "math partial derivative turev"),
        new("∆", "Increment / Delta", "Fark / Delta", "math delta increment fark"),
        new("°", "Degree", "Derece", "math degree angle temperature derece"),
        new("‰", "Per Mille", "Binde", "math permille binde"),
        new("∴", "Therefore", "Dolayısıyla", "math therefore logic dolayisiyla"),
        new("∵", "Because", "Çünkü", "math because logic cunku"),
        new("∈", "Element Of", "Elemanıdır", "math set element in eleman"),
        new("∉", "Not an Element Of", "Elemanı Değildir", "math set not element eleman degil"),
        new("∅", "Empty Set", "Boş Küme", "math empty set null bos kume"),
        new("∀", "For All", "Her Biri İçin", "math forall logic her"),
        new("∃", "There Exists", "En Az Bir", "math exists logic var"),
        new("²", "Superscript Two", "Üst Simge İki (Kare)", "math squared power two kare ust"),
        new("³", "Superscript Three", "Üst Simge Üç (Küp)", "math cubed power three kup ust"),
        new("½", "One Half", "Yarım", "math fraction half yarim bucuk"),
        new("¼", "One Quarter", "Çeyrek", "math fraction quarter ceyrek"),
        new("¾", "Three Quarters", "Üç Çeyrek", "math fraction three quarters uc ceyrek"),

        // Yunan harfleri / Greek
        new("α", "Alpha", "Alfa", "greek alpha alfa"),
        new("β", "Beta", "Beta", "greek beta"),
        new("γ", "Gamma", "Gama", "greek gamma gama"),
        new("δ", "Delta", "Delta", "greek delta"),
        new("ε", "Epsilon", "Epsilon", "greek epsilon"),
        new("θ", "Theta", "Teta", "greek theta teta"),
        new("λ", "Lambda", "Lambda", "greek lambda"),
        new("μ", "Mu / Micro", "Mü / Mikro", "greek mu micro mikron"),
        new("π", "Pi", "Pi", "greek pi math"),
        new("σ", "Sigma", "Sigma", "greek sigma"),
        new("φ", "Phi", "Fi", "greek phi fi"),
        new("ψ", "Psi", "Psi", "greek psi"),
        new("ω", "Omega", "Omega", "greek omega"),
        new("Ω", "Ohm / Capital Omega", "Ohm / Büyük Omega", "greek omega ohm resistance direnc"),

        // Noktalama ve Tipografi / Punctuation & Typography
        new("—", "Em Dash", "Uzun Tire", "punctuation dash em uzun tire"),
        new("–", "En Dash", "Orta Tire", "punctuation dash en orta tire"),
        new("…", "Ellipsis", "Üç Nokta", "punctuation ellipsis dots uc nokta"),
        new("•", "Bullet", "Madde İşareti", "punctuation bullet dot madde isareti"),
        new("·", "Middle Dot", "Orta Nokta", "punctuation middle dot orta nokta"),
        new("§", "Section Sign", "Bölüm İşareti", "punctuation section bolum"),
        new("¶", "Pilcrow / Paragraph", "Paragraf İşareti", "punctuation paragraph pilcrow paragraf"),
        new("©", "Copyright", "Telif Hakkı", "symbol copyright telif"),
        new("®", "Registered Trademark", "Tescilli Marka", "symbol registered tescilli"),
        new("™", "Trademark", "Ticari Marka", "symbol trademark marka"),
        new("№", "Numero Sign", "Numara İşareti", "symbol numero no numara"),
        new("«", "Left Guillemet", "Sol Çift Tırnak", "punctuation quote guillemet tirnak"),
        new("»", "Right Guillemet", "Sağ Çift Tırnak", "punctuation quote guillemet tirnak"),
        new("“", "Left Double Quote", "Sol Tırnak", "punctuation quote tirnak"),
        new("”", "Right Double Quote", "Sağ Tırnak", "punctuation quote tirnak"),

        // İşaretler ve Arayüz / Marks & UI
        new("✓", "Check Mark", "Onay İşareti", "symbol check tick onay tik"),
        new("✔", "Heavy Check Mark", "Kalın Onay İşareti", "symbol check tick onay tik"),
        new("✗", "Ballot X", "Çarpı İşareti", "symbol cross x carpi"),
        new("★", "Black Star", "Dolu Yıldız", "symbol star yildiz"),
        new("☆", "White Star", "Boş Yıldız", "symbol star outline yildiz"),
        new("♥", "Heart", "Kalp", "symbol heart love kalp"),
        new("♪", "Eighth Note", "Müzik Notası", "symbol music note muzik nota"),
        new("⚠", "Warning Sign", "Uyarı İşareti", "symbol warning alert uyari"),
        new("⌘", "Command Key", "Command Tuşu", "symbol key command mac tus"),
        new("⌥", "Option Key", "Option Tuşu", "symbol key option alt tus"),
        new("⇧", "Shift Key", "Shift Tuşu", "symbol key shift tus"),
        new("⏎", "Return Symbol", "Enter Sembolü", "symbol key return enter tus")
    ];

    public override IEnumerable<SearchResult> Search(SearchQuery query)
    {
        if (query.IsEmpty)
            return [];

        var hasKeyword = TryExtractKeywordFilter(query.Raw, out var filter);
        if (!hasKeyword && query.Folded.Length < 2 && !IsSingleCatalogSymbol(query.Raw))
            return [];

        if (hasKeyword && filter.Length == 0)
        {
            return Catalog.Take(MaxKeywordResults).Select((entry, i) =>
                ToResult(entry, 1.0 - (i * 0.01), SearchRelevance.High));
        }

        var searchQuery = hasKeyword ? new SearchQuery(filter) : query;
        var results = new List<(SearchResult Result, double Score)>();
        var limit = hasKeyword ? MaxKeywordResults : MaxDirectResults;

        foreach (var entry in Catalog)
        {
            var score = ScoreEntry(entry, searchQuery);
            if (score <= 0)
                continue;

            var categoryRelevance = hasKeyword
                ? SearchRelevance.Highest
                : score >= 0.9 ? SearchRelevance.Moderate : SearchRelevance.Low;
            results.Add((ToResult(entry, score, categoryRelevance), score));
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .Select(r => r.Result);
    }

    private static double ScoreEntry(SpecialCharEntry entry, SearchQuery query)
    {
        if (string.Equals(query.Raw.Trim(), entry.Symbol, StringComparison.Ordinal))
            return 1.0;

        var codePoint = $"u+{(int)entry.Symbol[0]:x4}";
        if (string.Equals(query.Folded, codePoint, StringComparison.Ordinal))
            return 0.95;

        var enScore = SearchMatcher.Score(query, SearchText.Fold(entry.EnglishName));
        var trScore = SearchMatcher.Score(query, SearchText.Fold(entry.TurkishName));
        var kwScore = SearchMatcher.Score(query, entry.Keywords) * 0.75;
        return Math.Max(enScore, Math.Max(trScore, kwScore));
    }

    private SearchResult ToResult(SpecialCharEntry entry, double score, int categoryRelevance)
    {
        var displayName = _turkish ? entry.TurkishName : entry.EnglishName;
        var codePoint = $"U+{(int)entry.Symbol[0]:X4}";
        return new SearchResult("char:" + codePoint, Name, $"{entry.Symbol}  {displayName}", () => shell.CopyText(entry.Symbol))
        {
            Subtitle = $"{codePoint} · {t("SearchHintCopy")}",
            Glyph = SearchGlyph.Symbol,
            CategoryRelevance = categoryRelevance,
            Relevance = score,
            RestoresFocus = true,
            Drag = new SearchDragData(SearchDragKind.Text, entry.Symbol)
        };
    }

    private bool TryExtractKeywordFilter(string raw, out string filter)
    {
        var trimmed = raw.Trim();
        var keywords = t("SearchKwSpecialChars").Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var kw in keywords)
        {
            if (string.Equals(trimmed, kw, StringComparison.OrdinalIgnoreCase))
            {
                filter = "";
                return true;
            }

            if (trimmed.StartsWith(kw + " ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(kw + ":", StringComparison.OrdinalIgnoreCase))
            {
                filter = trimmed[(kw.Length + 1)..].Trim();
                return true;
            }
        }

        filter = "";
        return false;
    }

    private static bool IsSingleCatalogSymbol(string raw)
    {
        var s = raw.Trim();
        return s.Length == 1 && Catalog.Any(c => c.Symbol == s);
    }
}
