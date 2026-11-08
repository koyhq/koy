using Koy.Core.Search;

namespace Koy.Core.Tests.Search;

public class SearchMatchingTests
{
    private static double Score(string query, string text) => SearchMatcher.Score(new SearchQuery(query), SearchText.Fold(text));

    [Theory]
    [InlineData("IŞIK", "isik")]
    [InlineData("ışık", "isik")]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("ÇĞÖŞÜ", "cgosu")]
    [InlineData("i̇", "i")]
    [InlineData("", "")]
    public void Fold_handles_turkish_letters(string input, string expected) => Assert.Equal(expected, SearchText.Fold(input));

    [Fact]
    public void Query_splits_words_and_trims()
    {
        var q = new SearchQuery("  Not   Defteri ");

        Assert.Equal("Not   Defteri", q.Raw);
        Assert.Equal(["not", "defteri"], q.Words);
    }

    [Fact]
    public void Empty_query_matches_nothing() => Assert.Equal(0, Score("   ", "Chrome"));

    [Fact]
    public void Exact_match_is_best() => Assert.Equal(1, Score("chrome", "Chrome"));

    [Fact]
    public void Turkish_query_written_without_diacritics_matches() => Assert.True(Score("sifre", "Şifre Yöneticisi") > 0);

    [Fact]
    public void Prefix_beats_later_word_beats_contained()
    {
        var prefix = Score("not", "Notepad");
        var word = Score("not", "Microsoft Notes App");
        var inside = Score("not", "Canoto Player");

        Assert.True(prefix > word);
        Assert.True(word > inside);
        Assert.True(inside > 0);
    }

    [Fact]
    public void Shorter_name_wins_between_same_kind_of_match() =>
        Assert.True(Score("not", "Notes") > Score("not", "Notes and Reminders Manager"));

    [Fact]
    public void Initials_match() => Assert.True(Score("vsc", "Visual Studio Code") > 0);

    [Fact]
    public void Every_word_must_match()
    {
        Assert.True(Score("not def", "Not Defteri") > 0);
        Assert.Equal(0, Score("not xyz", "Not Defteri"));
    }

    [Theory]
    [InlineData("chorme", "Chrome")] // yan yana iki harf
    [InlineData("chrme", "Chrome")] // eksik harf
    [InlineData("chroome", "Chrome")] // fazla harf
    [InlineData("chrone", "Chrome")] // yanlış harf
    public void One_typo_is_tolerated(string query, string text) => Assert.True(Score(query, text) > 0);

    [Fact]
    public void Typo_ranks_below_real_match() => Assert.True(Score("chrome", "Chrome Canary") > Score("chrome", "Chrone"));

    [Fact]
    public void Short_terms_get_no_typo_tolerance() => Assert.Equal(0, Score("vsz", "Visual"));
}
