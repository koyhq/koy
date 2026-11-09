using Koy.Core.Lyrics;

namespace Koy.Core.Tests;

public class LyricsQueryTests
{
    private static readonly TimeSpan Length = TimeSpan.FromMinutes(3);

    [Theory]
    [InlineData("Song (Official Music Video)", "Song")]
    [InlineData("Song [Lyrics]", "Song")]
    [InlineData("Song (feat. Someone)", "Song")]
    [InlineData("Song (Resmi Video)", "Song")]
    [InlineData("Song - Remastered 2011", "Song")]
    [InlineData("Song - 2011 Remaster", "Song")]
    [InlineData("Song (Live at Wembley)", "Song (Live at Wembley)")]
    public void Removes_title_noise_only(string raw, string expected)
    {
        Assert.Equal(expected, LyricsQuery.Create(raw, "Artist", Length)!.Title);
    }

    [Theory]
    [InlineData("Queen - Topic", "Queen")]
    [InlineData("QueenVEVO", "Queen")]
    [InlineData("Queen", "Queen")]
    public void Removes_channel_suffixes(string raw, string expected)
    {
        Assert.Equal(expected, LyricsQuery.Create("Song", raw, Length)!.Artist);
    }

    [Fact]
    public void Splits_video_title_when_artist_is_missing()
    {
        var query = LyricsQuery.Create("Artist - Song (Official Video)", "", Length)!;

        Assert.Equal("Artist", query.Artist);
        Assert.Equal("Song", query.Title);
    }

    [Fact]
    public void Strips_artist_prefix_when_it_repeats_the_artist_field()
    {
        Assert.Equal("Song", LyricsQuery.Create("artist - Song", "Artist", Length)!.Title);
    }

    [Theory]
    [InlineData("Queen - Bohemian Rhapsody (Official Video)", "Queen Official", "Queen", "Bohemian Rhapsody")]
    [InlineData("Daft Punk - Around the World", "Punk", "Daft Punk", "Around the World")]
    public void Splits_video_title_when_channel_name_overlaps_the_artist(string title, string channel, string artist, string track)
    {
        var query = LyricsQuery.Create(title, channel, Length)!;

        Assert.Equal(artist, query.Artist);
        Assert.Equal(track, query.Title);
    }

    [Fact]
    public void Does_not_split_on_a_partial_word_match()
    {
        var query = LyricsQuery.Create("Anything - Else", "A", Length)!;

        Assert.Equal("A", query.Artist);
        Assert.Equal("Anything - Else", query.Title);
    }

    [Fact]
    public void Keeps_dash_when_left_side_is_not_the_artist()
    {
        Assert.Equal("Part 1 - Intro", LyricsQuery.Create("Part 1 - Intro", "Artist", Length)!.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("(Official Video)")]
    public void Returns_null_without_a_title(string? title)
    {
        Assert.Null(LyricsQuery.Create(title, "Artist", Length));
    }

    [Fact]
    public void Rejects_absurdly_long_titles()
    {
        Assert.Null(LyricsQuery.Create(new string('x', 201), "Artist", Length));
    }

    [Fact]
    public void Unknown_duration_is_null()
    {
        Assert.Null(LyricsQuery.Create("Song", "Artist", TimeSpan.Zero)!.Duration);
    }

    [Fact]
    public void Key_ignores_duration_and_case()
    {
        var first = LyricsQuery.Create("Song", "Artist", Length)!;
        var second = LyricsQuery.Create("SONG", "ARTIST", TimeSpan.Zero)!;

        Assert.Equal(first.Key, second.Key);
    }

    [Fact]
    public void Primary_artist_is_the_first_name()
    {
        Assert.Equal("A", LyricsQuery.Create("Song", "A, B", Length)!.PrimaryArtist);
    }
}
