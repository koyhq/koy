using System.Text.RegularExpressions;
using Koy.Core.Appearance;

namespace Koy.Core.Tests;

public class AppearanceTests
{
    [Theory]
    [InlineData(AppTheme.Dark, false, ResolvedTheme.Dark)]
    [InlineData(AppTheme.Dark, true, ResolvedTheme.Dark)]
    [InlineData(AppTheme.Light, false, ResolvedTheme.Light)]
    [InlineData(AppTheme.System, true, ResolvedTheme.Light)]
    [InlineData(AppTheme.System, false, ResolvedTheme.Dark)]
    public void Theme_setting_resolves_system_and_fixed_values(AppTheme value, bool systemLight, ResolvedTheme expected)
    {
        Assert.Equal(expected, ThemeSetting.Resolve(value, systemLight));
    }

    [Theory]
    [InlineData(AnimationSpeed.Calm, 1.5)]
    [InlineData(AnimationSpeed.Normal, 1.0)]
    [InlineData(AnimationSpeed.Fast, 0.65)]
    [InlineData(AnimationSpeed.Off, 0.0)]
    public void Speed_factor_follows_setting(AnimationSpeed speed, double expected)
    {
        Assert.Equal(expected, MotionPolicy.SpeedFactor(speed));
    }

    [Fact]
    public void Reduce_motion_and_battery_saver_turn_motion_off()
    {
        Assert.Equal(1.0, MotionPolicy.Scale(AnimationSpeed.Normal, true, true, false));
        Assert.Equal(0.0, MotionPolicy.Scale(AnimationSpeed.Normal, true, false, false));
        Assert.Equal(1.0, MotionPolicy.Scale(AnimationSpeed.Normal, false, false, false));
        Assert.Equal(0.0, MotionPolicy.Scale(AnimationSpeed.Calm, false, true, true));
        Assert.Equal(0.0, MotionPolicy.Scale(AnimationSpeed.Off, false, true, false));
    }

    [Fact]
    public void Scaled_milliseconds_rounds_and_zero_scale_is_instant()
    {
        Assert.Equal(270, MotionPolicy.ScaledMilliseconds(180, 1.5));
        Assert.Equal(0, MotionPolicy.ScaledMilliseconds(180, 0));
    }

    [Theory]
    [InlineData(0, CornerStyle.Standard)]
    [InlineData(6, CornerStyle.Sharp)]
    [InlineData(12, CornerStyle.Standard)]
    [InlineData(18, CornerStyle.Standard)] // eski varsayılan: seçim sayılmaz
    [InlineData(17, CornerStyle.Round)]
    public void Corner_setting_maps_stored_value(double stored, CornerStyle expected)
    {
        Assert.Equal(expected, CornerSetting.Resolve(stored));
        Assert.Equal(expected, CornerSetting.Resolve(CornerSetting.ToStored(expected)));
    }

    [Fact]
    public void Contrast_matches_known_values()
    {
        Assert.Equal(21.0, ColorMath.Contrast("#000000", "#FFFFFF"), 1);
        Assert.Equal(1.0, ColorMath.Contrast("#3B82F6", "#3B82F6"), 3);
        Assert.InRange(ColorMath.Contrast("#3B82F6", "#0A0A0B"), 5.3, 5.5);
    }

    [Theory]
    [InlineData("#3B82F6", ResolvedTheme.Dark, "#18181B")]
    [InlineData("#3B82F6", ResolvedTheme.Light, "#FFFFFF")]
    [InlineData("#8B5CF6", ResolvedTheme.Dark, "#18181B")]
    [InlineData("#8B5CF6", ResolvedTheme.Light, "#FFFFFF")]
    [InlineData("#F97316", ResolvedTheme.Light, "#FFFFFF")]
    [InlineData("#22C55E", ResolvedTheme.Light, "#FFFFFF")]
    public void Accent_palette_text_and_on_accent_meet_contrast(string accent, ResolvedTheme theme, string surface)
    {
        var p = AccentPalette.Derive(accent, theme, surface);
        Assert.True(ColorMath.Contrast(p.Text, surface) >= 4.5, $"Text {p.Text}");
        Assert.True(ColorMath.Contrast(p.OnAccent, p.Base) >= 4.5, $"OnAccent {p.OnAccent}");
    }

    [Fact]
    public void Invalid_accent_falls_back_to_koy_blue()
    {
        Assert.Equal("#3B82F6", AccentPalette.Derive("yok", ResolvedTheme.Dark, "#18181B").Base);
    }

    // --- Tema sözlüklerini (XAML) doğrudan okuyan denetim: renk anahtarları ve kontrast ---

    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Koy.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "App", "Resources");
    }

    private static Dictionary<string, string> Palette(string file)
    {
        var text = File.ReadAllText(Path.Combine(ResourcesDir(), file));
        return Regex.Matches(text, "x:Key=\"(\\w+)\"\\s+Color=\"(#[0-9A-Fa-f]{6,8})\"")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    }

    [Fact]
    public void Dark_and_light_palettes_define_the_same_keys()
    {
        var dark = Palette("Theme.Dark.xaml");
        var light = Palette("Theme.Light.xaml");
        Assert.NotEmpty(dark);
        Assert.Equal(dark.Keys.OrderBy(k => k), light.Keys.OrderBy(k => k));
    }

    [Theory]
    [InlineData("Theme.Dark.xaml")]
    [InlineData("Theme.Light.xaml")]
    public void Text_tokens_meet_aa_on_surfaces(string file)
    {
        var p = Palette(file);
        foreach (var surface in new[] { "KoyBgWindow", "KoyBgRow", "KoyBgShell" })
        {
            foreach (var text in new[] { "KoyTextPrimary", "KoyTextSecondary", "KoyTextMuted" })
                Assert.True(ColorMath.Contrast(p[text], p[surface]) >= 4.5, $"{file}: {text} / {surface}");
        }

        Assert.True(ColorMath.Contrast(p["KoyDangerText"], p["KoyBgRow"]) >= 4.5, $"{file}: danger");
        Assert.True(ColorMath.Contrast(p["KoyFgInfo"], p["KoyBgInfo"]) >= 4.5, $"{file}: info");
        Assert.True(ColorMath.Contrast(p["KoyAccentText"], p["KoyBgRow"]) >= 4.5, $"{file}: accent text");
        Assert.True(ColorMath.Contrast(p["KoyOnAccent"], p["KoyAccent"]) >= 4.5, $"{file}: on accent");
    }

    [Theory]
    [InlineData("Theme.Dark.xaml")]
    [InlineData("Theme.Light.xaml")]
    public void Control_boundaries_meet_non_text_contrast(string file)
    {
        var p = Palette(file);
        // Kapalı anahtarın kenarı ve odak halkası, satır zemininde en az 3:1 olmalı (WCAG 1.4.11).
        Assert.True(ColorMath.Contrast(p["KoyBorderControl"], p["KoyBgRow"]) >= 3.0, $"{file}: border");
        Assert.True(ColorMath.Contrast(p["KoyFocusRing"], p["KoyBgRow"]) >= 3.0, $"{file}: focus");
        Assert.True(ColorMath.Contrast(p["KoyToggleKnobOff"], p["KoyToggleTrackOff"]) >= 3.0, $"{file}: knob");
        Assert.True(ColorMath.Contrast(p["KoyToggleKnobOn"], p["KoyAccent"]) >= 3.0, $"{file}: knob on");
    }
}
