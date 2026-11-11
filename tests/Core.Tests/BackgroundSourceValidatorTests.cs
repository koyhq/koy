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
    public void Validation_accepts_a_path_that_does_not_exist_yet()
    {
        // Doğrulayıcı saf metin denetimidir: dosyanın var olup olmadığına bakmaz.
        var source = new BackgroundSource(BackgroundKind.Video, @"C:\yok\hic\bu-dosya-yok.mp4");

        Assert.Equal(BackgroundSourceError.None, BackgroundSourceValidator.Validate(source));
    }

    [Theory]
    [InlineData(@"\\?\C:\Pictures\a.png")]
    [InlineData(@"\\.\C:\Pictures\a.png")]
    [InlineData(@"\\server\share\a.png")]
    [InlineData("//server/share/a.png")]
    [InlineData("C:/Users/ali/Pictures/a.png")]
    public void Device_prefixes_unc_and_forward_slashes_are_not_local(string path)
    {
        Assert.Equal(BackgroundSourceError.NotLocalPath,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.StillImage, path)));
    }

    [Theory]
    [InlineData(@"C:\Pictures\dosya.png:gizli")]
    [InlineData(@"C:\Pictures:ads.png")]
    [InlineData(@"C:\Pictures\a.png:Zone.Identifier")]
    public void Alternate_data_streams_are_rejected(string path)
    {
        Assert.Equal(BackgroundSourceError.InvalidCharacters,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.StillImage, path)));
    }

    [Theory]
    [InlineData(BackgroundKind.StillImage, @"C:\Pictures\NUL.png")]
    [InlineData(BackgroundKind.AnimatedImage, @"C:\Pictures\com1.gif")]
    [InlineData(BackgroundKind.Video, @"C:\Pictures\LPT1.mp4")]
    public void Windows_device_names_are_rejected_with_any_extension(BackgroundKind kind, string path)
    {
        Assert.Equal(BackgroundSourceError.ReservedDeviceName,
            BackgroundSourceValidator.Validate(new BackgroundSource(kind, path)));
    }

    [Fact]
    public void Names_that_only_start_like_a_device_are_accepted()
    {
        Assert.Equal(BackgroundSourceError.None,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.StillImage, Dir + "CONsole.png")));
    }

    [Theory]
    [InlineData(@"C:\Pictures\..\a.png")]
    [InlineData(@"C:\Pictures\.\a.png")]
    [InlineData(@"C:\Pictures\\a.png")]
    [InlineData(@"C:\Pictures\a.png.")]
    [InlineData(@"C:\Pictures\a.png ")]
    [InlineData(@"C:\Pictures \a.png")]
    [InlineData(@"C:\Pictures\")]
    public void Non_canonical_paths_are_rejected(string path)
    {
        // Windows bu yolları sessizce normalleştirir; görünen ile açılan yol ayrışır.
        Assert.Equal(BackgroundSourceError.NonCanonicalPath,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.StillImage, path)));
    }

    [Theory]
    [InlineData(@"C:\Pictures\*.png")]
    [InlineData(@"C:\Pictures\a?.png")]
    [InlineData(@"C:\Pictures\a<b>.png")]
    [InlineData("C:\\Pictures\\a\"b.png")]
    [InlineData(@"C:\Pictures\a|b.png")]
    [InlineData("C:\\Pictures\\a\u202Egnp.png")]
    public void Wildcards_and_format_characters_are_rejected(string path)
    {
        Assert.Equal(BackgroundSourceError.InvalidCharacters,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.StillImage, path)));
    }

    [Theory]
    [InlineData(@"C:\nul\a.png")]
    [InlineData(@"C:\Pictures\NUL .png")]
    [InlineData(@"C:\Pictures\CONIN$.png")]
    [InlineData(@"C:\Pictures\CONOUT$.png")]
    [InlineData("C:\\Pictures\\COM\u00B9.png")]
    public void Device_names_in_any_segment_are_rejected(string path)
    {
        Assert.Equal(BackgroundSourceError.ReservedDeviceName,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.StillImage, path)));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void Undefined_kind_is_rejected(int raw)
    {
        Assert.Equal(BackgroundSourceError.KindMismatch,
            BackgroundSourceValidator.Validate(new BackgroundSource((BackgroundKind)raw, Dir + "a.png")));
        Assert.Equal(BackgroundSourceError.KindMismatch,
            BackgroundSourceValidator.Validate(new BackgroundSource((BackgroundKind)raw, string.Empty)));
    }

    [Fact]
    public void Null_path_from_deserialization_is_handled()
    {
        Assert.Equal(BackgroundSourceError.MissingPath,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.Video, null!)));
        Assert.Equal(BackgroundSourceError.None,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.None, null!)));
    }

    [Fact]
    public void Max_path_length_leaves_room_for_the_terminating_null()
    {
        // MAX_PATH 260 null'ı da sayar; 260 karakterlik metin klasik API'lerde açılmaz.
        Assert.Equal(259, BackgroundSourceValidator.MaxPathLength);
    }

    [Fact]
    public void Extension_check_is_case_insensitive_for_every_kind()
    {
        Assert.Equal(BackgroundSourceError.None,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.Video, Dir + "a.MP4")));
        Assert.Equal(BackgroundSourceError.None,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.AnimatedImage, Dir + "a.GiF")));
        Assert.Equal(BackgroundSourceError.None,
            BackgroundSourceValidator.Validate(new BackgroundSource(BackgroundKind.StillImage, Dir + "a.WEBP")));
    }

    [Fact]
    public void Enum_values_are_pinned_for_persistence()
    {
        // Bu değerler ayar dosyasında saklanabilir; değiştirmek mevcut kayıtları bozar.
        Assert.Equal(3, (int)BackgroundKind.Video);
        Assert.Equal(7, (int)BackgroundSourceError.ReservedDeviceName);
        Assert.Equal(8, (int)BackgroundSourceError.NonCanonicalPath);
    }
}
