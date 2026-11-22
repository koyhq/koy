using System.Text.RegularExpressions;
using Koy.Core.Appearance;

namespace Koy.Core.Tests.Appearance;

public class TaskbarThemeTests
{
    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Koy.slnx")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "App", "Resources");
    }

    [Theory]
    [InlineData("KoyDockSurface", "KoyTaskbarSurface")]
    [InlineData("KoyDockGlyph", "KoyTaskbarGlyph")]
    [InlineData("KoyDockClockSubtext", "KoyTaskbarClockSubtext")]
    [InlineData("KoyDockHover", "KoyTaskbarHover")]
    public void Dock_roles_map_to_the_taskbar_tokens(string dock, string taskbar) =>
        Assert.Equal(taskbar, TaskbarTheme.TokenFor(dock));

    [Theory]
    [InlineData("KoyDockCapsuleSurface")]   // kapsül her temada koyu kalır
    [InlineData("KoyDockCapsuleBorder")]
    [InlineData("KoyDockCover")]
    [InlineData("KoyAccent")]
    public void Capsule_and_unrelated_tokens_are_not_remapped(string key) =>
        Assert.Equal(key, TaskbarTheme.TokenFor(key));

    [Theory]
    [InlineData("Theme.Dark.xaml")]
    [InlineData("Theme.Light.xaml")]
    public void Both_themes_define_every_taskbar_token(string file)
    {
        var text = File.ReadAllText(Path.Combine(ResourcesDir(), file));
        foreach (var key in TaskbarTheme.TaskbarKeys)
            Assert.Contains($"x:Key=\"{key}\"", text);
    }

    [Fact]
    public void Light_taskbar_is_a_light_surface_with_dark_text_and_dark_theme_stays_dark()
    {
        static string Value(string file, string key)
        {
            var text = File.ReadAllText(Path.Combine(ResourcesDir(), file));
            return Regex.Match(text, $"x:Key=\"{key}\" Color=\"(#[0-9A-Fa-f]+)\"").Groups[1].Value;
        }

        static double Luma(string hex)
        {
            var h = hex.TrimStart('#');
            if (h.Length == 8) h = h[2..];
            var r = Convert.ToInt32(h[..2], 16); var g = Convert.ToInt32(h[2..4], 16); var b = Convert.ToInt32(h[4..6], 16);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        Assert.True(Luma(Value("Theme.Light.xaml", "KoyTaskbarSurface")) > 200);
        Assert.True(Luma(Value("Theme.Light.xaml", "KoyTaskbarText")) < 60);
        Assert.True(Luma(Value("Theme.Light.xaml", "KoyTaskbarGlyph")) < 60);
        Assert.True(Luma(Value("Theme.Dark.xaml", "KoyTaskbarSurface")) < 60);
        Assert.True(Luma(Value("Theme.Dark.xaml", "KoyTaskbarText")) > 200);
        // Kapsül yüzeyi iki temada da koyu.
        Assert.True(Luma(Value("Theme.Light.xaml", "KoyDockCapsuleSurface")) < 60);
    }

    [Fact]
    public void Taskbar_tokens_are_user_theme_tokens()
    {
        foreach (var key in TaskbarTheme.TaskbarKeys)
            Assert.Contains(key, AllowedTokens.All);
    }
}
