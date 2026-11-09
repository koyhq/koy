using Koy.Core.Lyrics;

namespace Koy.Core.Tests;

public class LrclibApiTests
{
    private static LyricsQuery Query(double? seconds = 200) =>
        new("Song", "Artist", seconds is { } s ? TimeSpan.FromSeconds(s) : null);

    // JSON sayısı kültürden bağımsız yazılır: Türkçe Windows'ta 200.5 "200,5" olur ve JSON bozulur.
    private static string Item(string title, double duration, string? synced) =>
        $$"""{"trackName":"{{title}}","duration":{{duration.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"syncedLyrics":{{(synced is null ? "null" : $"\"{synced}\"")}}}""";

    [Fact]
    public void Get_url_escapes_fields_and_rounds_duration()
    {
        var url = LrclibApi.GetUrl(new LyricsQuery("Şarkı & Söz", "A/B", TimeSpan.FromSeconds(199.6)));

        Assert.Equal("https://lrclib.net/api/get?track_name=%C5%9Eark%C4%B1%20%26%20S%C3%B6z&artist_name=A%2FB&duration=200", url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.5)]
    [InlineData(4000d)]
    public void Get_url_omits_missing_or_out_of_range_duration(double? seconds)
    {
        Assert.DoesNotContain("duration", LrclibApi.GetUrl(Query(seconds)));
    }

    [Fact]
    public void Search_url_uses_primary_artist()
    {
        var url = LrclibApi.SearchUrl(new LyricsQuery("Song", "A, B", null));

        Assert.EndsWith("artist_name=A", url);
    }

    [Theory]
    [InlineData("""{"syncedLyrics":"[00:01.00]a"}""", "[00:01.00]a")]
    [InlineData("""{"syncedLyrics":null,"plainLyrics":"a"}""", null)]
    [InlineData("""{"syncedLyrics":"  "}""", null)]
    [InlineData("[]", null)]
    [InlineData("not json", null)]
    public void Reads_only_synced_lyrics(string json, string? expected)
    {
        Assert.Equal(expected, LrclibApi.ParseSynced(json));
    }

    [Fact]
    public void Picks_closest_duration_within_tolerance()
    {
        var json = $"[{Item("Song", 205, "far")},{Item("Song", 202, "near")},{Item("Song", 200.5, "nearest")}]";

        Assert.Equal("nearest", LrclibApi.PickBest(json, Query()));
    }

    [Fact]
    public void Rejects_versions_outside_tolerance()
    {
        Assert.Null(LrclibApi.PickBest($"[{Item("Song", 260, "other version")}]", Query()));
    }

    [Fact]
    public void Skips_entries_without_synced_lyrics()
    {
        var json = $"[{Item("Song", 200, null)},{Item("Song", 201, "ok")}]";

        Assert.Equal("ok", LrclibApi.PickBest(json, Query()));
    }

    [Fact]
    public void Rejects_a_different_song()
    {
        Assert.Null(LrclibApi.PickBest($"[{Item("Completely Different", 200, "x")}]", Query()));
    }

    [Fact]
    public void Without_duration_takes_first_matching_title()
    {
        var json = $"[{Item("Other", 100, "no")},{Item("Song (Remix)", 100, "first")},{Item("Song", 200, "second")}]";

        Assert.Equal("first", LrclibApi.PickBest(json, Query(null)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("broken")]
    public void Survives_unexpected_search_responses(string json)
    {
        Assert.Null(LrclibApi.PickBest(json, Query()));
    }
}
