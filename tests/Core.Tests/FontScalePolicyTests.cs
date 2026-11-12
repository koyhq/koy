using Koy.Core.Appearance;
using Koy.Core.Settings;

namespace Koy.Core.Tests;

public class FontScalePolicyTests
{
    private static readonly string[] Installed = ["Segoe UI", "Cascadia Mono", "Consolas"];

    [Theory]
    [InlineData(100, 1920, 1040, 1.0)]   // sığıyor
    [InlineData(150, 1920, 1040, 1.5)]   // 1320x1020 sığıyor
    [InlineData(150, 1366, 728, 1.07)]   // 728/680 ≈ 1,07'ye iner
    [InlineData(120, 800, 600, 0.88)]    // 600/680 ≈ 0,88
    [InlineData(120, 640, 480, 0.8)]     // çok küçük ekranda alt sınır
    [InlineData(400, 3840, 2100, 1.5)]   // önce 150'ye kısılır
    [InlineData(130, 0, 0, 1.3)]         // çalışma alanı bilinmiyor
    public void Fit_scale_keeps_window_on_the_work_area(int percent, double workW, double workH, double expected)
    {
        Assert.Equal(expected, FontScalePolicy.FitScale(percent, 880, 680, workW, workH), 2);
    }

    [Fact]
    public void Fit_scale_ignores_nan_work_area() =>
        Assert.Equal(1.2, FontScalePolicy.FitScale(120, 880, 680, double.NaN, 900), 3);

    [Theory]
    [InlineData(13, 13, 13)]
    [InlineData(20, 13, 20)]
    [InlineData(12.5, 16, 15.5)]  // 12,5 x 16/13 = 15,38 → 15,5
    [InlineData(20, 10, 15.5)]    // 15,38 → 15,5
    [InlineData(12, 99, 16.5)]    // boyut 18'e kısılır: 12 x 18/13 = 16,6 → 16,5
    public void Font_tokens_scale_with_base_size(double token, double baseSize, double expected)
    {
        Assert.Equal(expected, FontScalePolicy.ScaleFontToken(token, baseSize));
    }

    [Theory]
    [InlineData(50, 80)]
    [InlineData(80, 80)]
    [InlineData(100, 100)]
    [InlineData(150, 150)]
    [InlineData(400, 150)]
    [InlineData(-5, 80)]
    public void Ui_scale_is_clamped_to_80_150(int requested, int expected)
    {
        Assert.Equal(expected, FontScalePolicy.ClampUiScale(requested));
    }

    [Fact]
    public void Scaled_dip_applies_clamped_percent()
    {
        Assert.Equal(28.0, FontScalePolicy.ScaleDip(28.0, 100));
        Assert.Equal(42.0, FontScalePolicy.ScaleDip(28.0, 150));
        // Sınır dışı yüzde önce sınırlanır: 400 değil 150 uygulanır.
        Assert.Equal(42.0, FontScalePolicy.ScaleDip(28.0, 400));
        Assert.Equal(22.4, FontScalePolicy.ScaleDip(28.0, 80), 6);
    }

    [Theory]
    [InlineData(4, 10)]
    [InlineData(13, 13)]
    [InlineData(40, 18)]
    [InlineData(double.NaN, 13)]
    [InlineData(double.PositiveInfinity, 13)]
    public void Base_font_size_is_clamped_and_non_finite_falls_back(double requested, double expected)
    {
        Assert.Equal(expected, FontScalePolicy.ClampBaseFontSize(requested));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(450, 500)]
    [InlineData(449, 400)]
    [InlineData(400, 400)]
    [InlineData(1200, 900)]
    public void Font_weight_is_clamped_and_snapped_to_hundreds(int requested, int expected)
    {
        Assert.Equal(expected, FontScalePolicy.ClampFontWeight(requested));
    }

    [Fact]
    public void Font_family_resolves_case_insensitively_to_installed_spelling()
    {
        Assert.Equal("Cascadia Mono", FontScalePolicy.ResolveFontFamily("cascadia mono", Installed));
        Assert.Equal("Consolas", FontScalePolicy.ResolveFontFamily("  CONSOLAS ", Installed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Kaldirilmis Font")]
    public void Missing_or_empty_font_family_falls_back_to_default(string? requested)
    {
        Assert.Equal(FontScalePolicy.DefaultFontFamily, FontScalePolicy.ResolveFontFamily(requested, Installed));
    }

    [Fact]
    public void Overlong_font_family_falls_back_without_matching()
    {
        var huge = new string('a', FontScalePolicy.MaxFontFamilyLength + 1);

        Assert.Equal(FontScalePolicy.DefaultFontFamily, FontScalePolicy.ResolveFontFamily(huge, [huge]));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Non_finite_dip_scales_to_zero(double dip)
    {
        Assert.Equal(0.0, FontScalePolicy.ScaleDip(dip, 100));
    }

    [Fact]
    public void Custom_fallback_is_used_when_font_is_missing()
    {
        Assert.Equal("Consolas", FontScalePolicy.ResolveFontFamily("Yok", Installed, "Consolas"));
    }

    [Fact]
    public void Clock_font_falls_back_to_ui_font_when_empty_or_missing()
    {
        Assert.Equal("Segoe UI", FontScalePolicy.ResolveClockFontFamily("", "Segoe UI", Installed));
        Assert.Equal("Segoe UI", FontScalePolicy.ResolveClockFontFamily("Yok", "Segoe UI", Installed));
        Assert.Equal("Cascadia Mono", FontScalePolicy.ResolveClockFontFamily("cascadia mono", "Segoe UI", Installed));
    }

    [Fact]
    public void Default_appearance_font_values_are_safe()
    {
        var appearance = new AppearanceSettings();

        Assert.Equal("", appearance.FontFamily);
        Assert.Equal("", appearance.ClockFontFamily);
        Assert.Equal(FontScalePolicy.DefaultBaseFontSize, appearance.BaseFontSize);
        Assert.Equal(FontScalePolicy.DefaultFontWeight, appearance.FontWeight);
        Assert.Equal(FontScalePolicy.DefaultUiScale, appearance.UiScalePercent);
    }

    [Fact]
    public void Font_settings_survive_a_save_and_load_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-fonts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            var settings = new AppSettings
            {
                Appearance = new AppearanceSettings
                {
                    FontFamily = "Cascadia Mono",
                    ClockFontFamily = "Consolas",
                    BaseFontSize = 15,
                    FontWeight = 600,
                    UiScalePercent = 125
                }
            };

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal(settings.Appearance, loaded.Appearance);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_font_fields_in_file_read_as_safe_defaults()
    {
        var read = SettingsFile.Load("""{ "Appearance": { "Theme": "Dark" } }""");

        Assert.Equal("", read.Appearance.FontFamily);
        Assert.Equal(FontScalePolicy.DefaultUiScale, read.Appearance.UiScalePercent);
        Assert.Equal(FontScalePolicy.DefaultBaseFontSize, read.Appearance.BaseFontSize);
    }
}
