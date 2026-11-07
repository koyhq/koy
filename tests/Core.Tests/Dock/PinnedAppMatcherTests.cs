using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.Core.Tests.Dock;

public class PinnedAppMatcherTests
{
    private static readonly WindowRect Bounds = new(0, 0, 800, 600);

    private static OpenWindowInfo Win(string title, string? path, string? aumid = null) =>
        new(1, title, path, 10, Bounds, null, aumid);

    [Fact]
    public void Explorer_pin_without_target_matches_explorer_window_by_aumid()
    {
        var pin = new PinnedApp("File Explorer", @"C:\Pins\File Explorer.lnk", null, null, "Microsoft.Windows.Explorer");

        Assert.True(PinnedAppMatcher.IsMatch(pin, Win("Downloads", @"C:\WINDOWS\Explorer.EXE", "Microsoft.Windows.Explorer")));
    }

    [Fact]
    public void Explorer_window_without_own_aumid_matches_pin_via_known_table()
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot")!;
        // Gerçek durum: kısayol AUMID'i dolu, hedef yerel ad ("Dosya Gezgini") yol değil, pencerenin AUMID'i yok.
        var pin = new PinnedApp("Dosya Gezgini", @"C:\Pins\File Explorer.lnk", "Dosya Gezgini", null, "Microsoft.Windows.Explorer");

        Assert.True(PinnedAppMatcher.IsMatch(pin, Win("İndirilenler - Dosya Gezgini", Path.Combine(systemRoot, "Explorer.EXE"))));
        Assert.False(PinnedAppMatcher.IsMatch(pin, Win("Notlar", @"C:\Windows\notepad.exe")));
    }

    [Fact]
    public void Known_table_only_knows_the_system_explorer()
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot")!;
        Assert.Equal(KnownAppUserModelIds.Explorer, KnownAppUserModelIds.ForProcessPath(Path.Combine(systemRoot, "explorer.exe")));
        Assert.Null(KnownAppUserModelIds.ForProcessPath(@"C:\Tools\explorer.exe"));
        Assert.Null(KnownAppUserModelIds.ForProcessPath(null));
    }

    [Fact]
    public void Explorer_pin_does_not_swallow_other_windows()
    {
        var pin = new PinnedApp("File Explorer", @"C:\Pins\File Explorer.lnk", null, null, "Microsoft.Windows.Explorer");

        Assert.False(PinnedAppMatcher.IsMatch(pin, Win("Notes", @"C:\Windows\notepad.exe", "Microsoft.Windows.Notepad")));
    }

    [Fact]
    public void Different_aumid_with_same_exe_stays_separate_chrome_profiles()
    {
        var pin = new PinnedApp("Google Chrome", @"C:\Pins\Chrome.lnk", @"C:\Program Files\Google\Chrome\Application\chrome.exe", null, "Chrome");

        Assert.True(PinnedAppMatcher.IsMatch(pin, Win("a", @"C:\Program Files\Google\Chrome\Application\chrome.exe", "Chrome")));
        Assert.False(PinnedAppMatcher.IsMatch(pin, Win("b", @"C:\Program Files\Google\Chrome\Application\chrome.exe", "Chrome.Profile2")));
    }

    [Fact]
    public void Aumid_comparison_ignores_case()
    {
        var pin = new PinnedApp("X", @"C:\Pins\X.lnk", null, null, "Vendor.App");
        Assert.True(PinnedAppMatcher.IsMatch(pin, Win("t", null, "vendor.app")));
    }

    [Fact]
    public void Uwp_shell_appsfolder_window_matches_by_aumid()
    {
        var pin = new PinnedApp("Spotify", @"C:\Pins\Spotify.lnk", null, null, "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify");
        var win = Win("Spotify", @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify");

        Assert.True(PinnedAppMatcher.IsMatch(pin, win));
    }

    [Fact]
    public void Without_aumid_full_path_matches_case_insensitively()
    {
        var pin = new PinnedApp("Code", @"C:\Pins\Code.lnk", @"C:\Users\x\AppData\Local\Programs\Microsoft VS Code\Code.exe", null);

        Assert.True(PinnedAppMatcher.IsMatch(pin, Win("a", @"c:\users\x\appdata\local\programs\microsoft vs code\code.exe")));
    }

    [Fact]
    public void System32_and_syswow64_paths_are_the_same_program()
    {
        Assert.True(PinnedAppMatcher.SamePath(@"C:\Windows\SysWOW64\notepad.exe", @"C:\WINDOWS\System32\NOTEPAD.EXE"));
        Assert.True(PinnedAppMatcher.SamePath(@"C:\Windows\Sysnative\cmd.exe", @"C:\Windows\System32\cmd.exe"));
    }

    [Fact]
    public void Environment_variables_in_target_are_expanded()
    {
        var root = Environment.GetEnvironmentVariable("SystemRoot")!;
        Assert.True(PinnedAppMatcher.SamePath(@"%SystemRoot%\explorer.exe", Path.Combine(root, "Explorer.EXE")));
    }

    [Fact]
    public void Only_one_side_has_aumid_falls_back_to_path()
    {
        var pin = new PinnedApp("Code", @"C:\Pins\Code.lnk", @"C:\VSCode\code.exe", null, "Microsoft.VisualStudioCode");
        Assert.True(PinnedAppMatcher.IsMatch(pin, Win("a", @"C:\VSCode\code.exe")));
        Assert.False(PinnedAppMatcher.IsMatch(pin, Win("a", @"C:\Other\notepad.exe")));
    }

    [Fact]
    public void Title_is_the_last_resort_for_pins_without_target_or_aumid()
    {
        var pin = new PinnedApp("Obsidian", @"C:\Pins\Obsidian.lnk", null, null);
        Assert.True(PinnedAppMatcher.IsMatch(pin, Win("Notes - Obsidian", null)));
    }
}
