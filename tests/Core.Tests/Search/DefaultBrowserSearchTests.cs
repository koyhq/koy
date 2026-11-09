using Koy.Core.Search;

namespace Koy.Core.Tests.Search;

public class DefaultBrowserSearchTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\Google\Chrome\Application\chrome.exe"" --single-argument %1", "Chrome", BrowserFamily.Chromium)]
    [InlineData(@"""C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"" --single-argument %1", "Edge", BrowserFamily.Chromium)]
    [InlineData(@"""C:\Program Files\Mozilla Firefox\firefox.exe"" -osint -url ""%1""", "Firefox", BrowserFamily.Firefox)]
    [InlineData(@"C:\Brave\brave.exe --single-argument %1", "Brave", BrowserFamily.Chromium)]
    public void Recognizes_browser_from_open_command(string command, string name, BrowserFamily family)
    {
        var browser = DefaultBrowserSearch.FromOpenCommand(command);

        Assert.NotNull(browser);
        Assert.Equal(name, browser.Name);
        Assert.Equal(family, browser.Family);
        Assert.EndsWith(".exe", browser.Executable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"""C:\Tools\unknown.exe"" %1")]
    [InlineData(@"""C:\Opera\launcher.exe"" %1")]
    [InlineData("rundll32.exe url.dll,FileProtocolHandler %1")]
    public void Unknown_or_missing_browser_gives_null(string? command) =>
        Assert.Null(DefaultBrowserSearch.FromOpenCommand(command));

    [Fact]
    public void Chromium_search_is_a_single_question_mark_argument()
    {
        var command = DefaultBrowserSearch.SearchCommand(new DefaultBrowser("chrome.exe", "Chrome", BrowserFamily.Chromium), "  --incognito kedi ");

        Assert.NotNull(command);
        // Terim seçenek gibi başlasa da "?" ile tek adres olarak gider.
        Assert.Equal(["--single-argument", "? --incognito kedi"], command.Arguments);
    }

    [Fact]
    public void Firefox_uses_search_option_but_not_for_dash_terms()
    {
        var firefox = new DefaultBrowser("firefox.exe", "Firefox", BrowserFamily.Firefox);

        Assert.Equal(["-search", "kedi"], DefaultBrowserSearch.SearchCommand(firefox, "kedi")!.Arguments);
        Assert.Null(DefaultBrowserSearch.SearchCommand(firefox, "-private"));
    }

    [Fact]
    public void Empty_term_gives_no_command() =>
        Assert.Null(DefaultBrowserSearch.SearchCommand(new DefaultBrowser("chrome.exe", "Chrome", BrowserFamily.Chromium), "  "));
}
