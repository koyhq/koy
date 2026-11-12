using Koy.Core.Appearance;

namespace Koy.Core.Tests;

// Simge paketi eşleştirmesi ve uygulama anahtarı (dosya okumadan).
public class IconPackFilesTests
{
    [Theory]
    [InlineData("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "Notepad.lnk", "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App")]
    [InlineData(null, "Notepad.lnk", "Notepad.lnk")]
    [InlineData("", "Notepad.lnk", "Notepad.lnk")]
    [InlineData(null, @"C:\Users\x\Start\Notepad.lnk", "Notepad.lnk")]
    [InlineData(null, "C:/Users/x/Notepad.lnk", "Notepad.lnk")]
    [InlineData("bad\\aumid", "Notepad.lnk", "Notepad.lnk")]
    public void Key_prefers_aumid_then_shortcut_name(string? aumid, string? id, string expected)
        => Assert.Equal(expected, AppIconKey.For(aumid, id));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "  ")]
    [InlineData(null, @"C:\folder\")]
    public void Key_is_null_when_nothing_valid(string? aumid, string? id)
        => Assert.Null(AppIconKey.For(aumid, id));

    [Theory]
    [InlineData(@"\\server\share\icons")]
    [InlineData(@"\\?\C:\icons")]
    [InlineData(@"C:\icons\..\other")]
    [InlineData(@"relative\icons")]
    [InlineData(@"C:\CON")]
    [InlineData("")]
    [InlineData(null)]
    public void Pack_folder_rejects_network_relative_and_unsafe_paths(string? folder)
        => Assert.False(IconPackFiles.IsSafeFolder(folder));

    // Sürücü harfli yol çözümü yol kütüphanesine bağlı: yalnız Windows'ta anlamlı (Linux taban çizgisi AppIconOverrideTests ile aynı).
    [Theory]
    [InlineData(@"C:\Icons")]
    [InlineData(@"C:\Icons\")]
    [InlineData(@"  D:\Packs\Mine  ")]
    public void Pack_folder_accepts_local_drive_paths_on_windows(string folder)
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.True(IconPackFiles.IsSafeFolder(folder));
    }

    [Fact]
    public void All_lists_aumid_then_shortcut_name_without_duplicates()
    {
        Assert.Equal(["App!Main", "Notepad.lnk"], AppIconKey.All("App!Main", @"C:\x\Notepad.lnk"));
        Assert.Equal(["Notepad.lnk"], AppIconKey.All(null, "Notepad.lnk"));
        Assert.Equal(["notepad.lnk"], AppIconKey.All("notepad.lnk", "Notepad.lnk"));
        Assert.Empty(AppIconKey.All(null, null));
    }

    [Fact]
    public void Matches_by_stem_ignoring_case_and_shortcut_extension()
    {
        string[] files = ["firefox.PNG", "chrome.png"];

        Assert.Equal("firefox.PNG", IconPackFiles.Find(files, "Firefox.lnk", allowSvg: false));
        Assert.Equal("chrome.png", IconPackFiles.Find(files, "Chrome.exe", allowSvg: false));
        Assert.Null(IconPackFiles.Find(files, "Edge.lnk", allowSvg: false));
    }

    [Fact]
    public void Prefers_png_then_ico_then_svg_regardless_of_listing_order()
    {
        Assert.Equal("a.png", IconPackFiles.Find(["a.svg", "a.ico", "a.png"], "a.lnk", allowSvg: true));
        Assert.Equal("a.ico", IconPackFiles.Find(["a.svg", "a.ico"], "a.lnk", allowSvg: true));
        Assert.Equal("a.svg", IconPackFiles.Find(["a.svg"], "a.lnk", allowSvg: true));
    }

    [Fact]
    public void Svg_is_ignored_when_the_renderer_cannot_show_it()
    {
        Assert.Null(IconPackFiles.Find(["a.svg"], "a.lnk", allowSvg: false));
        Assert.Equal("a.ico", IconPackFiles.Find(["a.svg", "a.ico"], "a.lnk", allowSvg: false));
    }

    [Fact]
    public void Aumid_keys_match_the_whole_name()
    {
        const string aumid = "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App";
        Assert.Equal(aumid + ".png", IconPackFiles.Find([aumid + ".png"], aumid, allowSvg: false));
    }

    [Fact]
    public void Unsafe_file_names_and_keys_are_skipped()
    {
        Assert.Null(IconPackFiles.Find(["CON.png", "..\\a.png", "a.exe", "a.txt"], "a.lnk", allowSvg: true));
        Assert.Null(IconPackFiles.Find(["CON.png"], "CON.lnk", allowSvg: true));
        Assert.Null(IconPackFiles.Find(["a.png"], "", allowSvg: true));
        Assert.Null(IconPackFiles.Find(["a.png"], null, allowSvg: true));
    }

    [Fact]
    public void Result_feeds_the_resolver_as_a_pack_choice()
    {
        var packFile = IconPackFiles.Find(["Notepad.png"], "Notepad.lnk", allowSvg: false);

        var choice = IconResolver.Resolve("Notepad.lnk", null, IconPack.Folder, packFile);

        Assert.Equal(new IconChoice(IconSourceKind.Pack, "Notepad.png"), choice);
    }
}
