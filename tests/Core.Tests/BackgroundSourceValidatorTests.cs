using Koy.Core.Background;

namespace Koy.Core.Tests;

public class BackgroundSourceValidatorTests
{
    private const string Dir = @"C:\Users\ali\Pictures\";

    [Theory]
    [InlineData(BackgroundKind.StillImage, ".png")]
    [InlineData(BackgroundKind.StillImage, ".JPG")]
    [InlineData(BackgroundKind.StillImage, ".jpeg")]
    [InlineData(BackgroundKind.StillImage, ".webp")]
    [InlineData(BackgroundKind.StillImage, ".bmp")]
    [InlineData(BackgroundKind.AnimatedImage, ".gif")]
    [InlineData(BackgroundKind.Video, ".mp4")]
    [InlineData(BackgroundKind.Video, ".webm")]
    public void Known_extension_with_matching_kind_is_valid(BackgroundKind kind, string extension)
    {
        var source = new BackgroundSource(kind, Dir + "arka" + extension);

        Assert.Equal(BackgroundSourceError.None, BackgroundSourceValidator.Validate(source));
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".ps1")]
    [InlineData(".lnk")]
    [InlineData(".url")]
    [InlineData(".txt")]
    [InlineData(".mkv")]
    public void Executable_shortcut_and_unknown_extensions_are_rejected(string extension)
    {
        var source = new BackgroundSource(BackgroundKind.Video, Dir + "calistir" + extension);

        Assert.Equal(BackgroundSourceError.UnknownExtension, BackgroundSourceValidator.Validate(source));
    }

    [Fact]
    public void Path_without_extension_is_rejected()
    {
        var source = new BackgroundSource(BackgroundKind.StillImage, Dir + "dosya");

        Assert.Equal(BackgroundSourceError.UnknownExtension, BackgroundSourceValidator.Validate(source));
    }

    [Fact]
    public void Dot_in_directory_name_is_not_taken_as_extension()
    {
        var source = new BackgroundSource(BackgroundKind.StillImage, @"C:\Benim.Klasorum\dosya");

        Assert.Equal(BackgroundSourceError.UnknownExtension, BackgroundSourceValidator.Validate(source));
    }

    [Fact]
    public void Kind_must_match_extension()
    {
        // GIF'i durağan görsel diye vermek de, MP4'ü GIF diye vermek de yanlış.
        Assert.Equal(BackgroundSourceError.KindMismatch,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.StillImage, Dir + "a.gif")));
        Assert.Equal(BackgroundSourceError.KindMismatch,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.AnimatedImage, Dir + "a.mp4")));
    }

    [Fact]
    public void Empty_or_whitespace_path_is_missing()
    {
        Assert.Equal(BackgroundSourceError.MissingPath,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.Video, "")));
        Assert.Equal(BackgroundSourceError.MissingPath,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.Video, "   ")));
    }

    [Fact]
    public void Path_longer_than_limit_is_rejected()
    {
        var longName = new string('a', BackgroundSourceValidator.MaxPathLength);
        var source = new BackgroundSource(BackgroundKind.AnimatedImage, Dir + longName + ".gif");

        Assert.Equal(BackgroundSourceError.PathTooLong, BackgroundSourceValidator.Validate(source));
    }

    [Fact]
    public void Path_exactly_at_limit_is_accepted()
    {
        var prefix = Dir + "x";
        var path = prefix + new string('a', BackgroundSourceValidator.MaxPathLength - prefix.Length - ".gif".Length) + ".gif";
        Assert.Equal(BackgroundSourceValidator.MaxPathLength, path.Length);

        Assert.Equal(BackgroundSourceError.None,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.AnimatedImage, path)));
    }

    [Theory]
    [InlineData("C:\\a\nb.gif")]
    [InlineData("C:\\a\tb.gif")]
    [InlineData("C:\\a\0b.gif")]
    public void Control_characters_are_rejected(string path)
    {
        Assert.Equal(BackgroundSourceError.InvalidCharacters,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.AnimatedImage, path)));
    }

    [Theory]
    [InlineData("https://example.com/a.gif")]
    [InlineData("file:///C:/a.gif")]
    [InlineData(@"\\server\share\a.gif")]
    [InlineData("a.gif")]
    [InlineData(@"Pictures\a.gif")]
    public void Remote_or_relative_paths_are_not_local(string path)
    {
        Assert.Equal(BackgroundSourceError.NotLocalPath,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.AnimatedImage, path)));
    }

    [Fact]
    public void None_kind_is_valid_only_without_path()
    {
        Assert.Equal(BackgroundSourceError.None, BackgroundSourceValidator.Validate(BackgroundSource.None));
        Assert.Equal(BackgroundSourceError.KindMismatch,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.None, Dir + "a.png")));
    }

    [Fact]
    public void Validation_does_not_touch_the_file_system()
    {
        // Dosya yok; doğrulama yine de yalnız metne bakıp kabul etmeli.
        var source = new BackgroundSource(BackgroundKind.Video, @"C:\yok\hic\bu-dosya-yok.mp4");

        Assert.Equal(BackgroundSourceError.None, BackgroundSourceValidator.Validate(source));
    }
}
