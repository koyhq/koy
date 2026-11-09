using Koy.Core.Lyrics;

namespace Koy.Core.Tests;

public sealed class LyricsStorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-lyrics-" + Guid.NewGuid().ToString("N"));

    private static readonly LyricsQuery Query = new("Song", "Artist", null);

    public LyricsStorageTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private void WriteLrc(string relativePath, string content = "[00:01.00]a")
    {
        var path = Path.Combine(_dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public async Task Local_finds_exact_file_name()
    {
        WriteLrc("Artist - Song.lrc", "exact");

        var result = await new LocalLyricsSource(_dir).FindAsync(Query, default);

        Assert.Equal(LyricsLookup.Found("exact"), result);
    }

    [Fact]
    public async Task Local_replaces_characters_windows_forbids()
    {
        WriteLrc("AC_DC - Song_.lrc", "safe");

        var result = await new LocalLyricsSource(_dir).FindAsync(new LyricsQuery("Song?", "AC/DC", null), default);

        Assert.Equal("safe", result.Lrc);
    }

    [Fact]
    public async Task Local_scans_subfolders_ignoring_case()
    {
        WriteLrc(Path.Combine("albums", "01 ARTIST song.lrc"), "nested");

        var result = await new LocalLyricsSource(_dir).FindAsync(Query, default);

        Assert.Equal("nested", result.Lrc);
    }

    [Fact]
    public async Task Local_does_not_guess_without_an_artist()
    {
        WriteLrc("Someone - Song.lrc");

        var result = await new LocalLyricsSource(_dir).FindAsync(new LyricsQuery("Song", "", null), default);

        Assert.Equal(LyricsLookupKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task Local_ignores_oversized_files()
    {
        WriteLrc("Artist - Song.lrc", new string('x', 1024 * 1024 + 1));

        var result = await new LocalLyricsSource(_dir).FindAsync(Query, default);

        Assert.Equal(LyricsLookupKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task Local_is_not_found_when_folder_is_missing()
    {
        var result = await new LocalLyricsSource(Path.Combine(_dir, "nope")).FindAsync(Query, default);

        Assert.Equal(LyricsLookupKind.NotFound, result.Kind);
    }

    [Fact]
    public void Cache_returns_what_was_written()
    {
        var cache = new LyricsCache(_dir);

        Assert.Equal(LyricsCacheStatus.Miss, cache.Read(Query).Status);
        cache.Write(Query, "lrc");

        Assert.Equal((LyricsCacheStatus.Hit, "lrc"), cache.Read(Query));
    }

    [Fact]
    public void Cache_file_name_does_not_expose_the_track()
    {
        new LyricsCache(_dir).Write(Query, "lrc");

        var name = Path.GetFileName(Assert.Single(Directory.GetFiles(_dir)));
        Assert.DoesNotContain("song", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cache_missing_marker_expires()
    {
        var now = DateTimeOffset.UtcNow;
        new LyricsCache(_dir).WriteMissing(Query);

        Assert.Equal(LyricsCacheStatus.KnownMissing, new LyricsCache(_dir, new FixedTime(now.AddDays(6))).Read(Query).Status);
        Assert.Equal(LyricsCacheStatus.Miss, new LyricsCache(_dir, new FixedTime(now.AddDays(8))).Read(Query).Status);
    }

    [Fact]
    public void Cache_found_lyrics_do_not_expire()
    {
        var now = DateTimeOffset.UtcNow;
        new LyricsCache(_dir).Write(Query, "lrc");

        Assert.Equal(LyricsCacheStatus.Hit, new LyricsCache(_dir, new FixedTime(now.AddYears(5))).Read(Query).Status);
    }

    [Fact]
    public void Cache_removes_oldest_files_beyond_the_limit()
    {
        var cache = new LyricsCache(_dir, maxFiles: 10);
        var start = DateTime.UtcNow.AddDays(-1);

        LyricsQuery Numbered(int i) => new($"Song {i}", "Artist", null);
        for (var i = 0; i < 10; i++)
        {
            cache.Write(Numbered(i), "lrc");
            foreach (var file in Directory.GetFiles(_dir))
            {
                if (File.GetLastWriteTimeUtc(file) > start.AddHours(1))
                    File.SetLastWriteTimeUtc(file, start.AddMinutes(i));
            }
        }

        cache.Write(Numbered(10), "lrc");

        Assert.Equal(8, Directory.GetFiles(_dir).Length);
        Assert.Equal(LyricsCacheStatus.Miss, cache.Read(Numbered(0)).Status);
        Assert.Equal(LyricsCacheStatus.Hit, cache.Read(Numbered(10)).Status);
    }
}
