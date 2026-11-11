using Koy.Core.Media;
using Koy.Core.Settings;

namespace Koy.Core.Tests;

public class MusicCardStyleSettingTests
{
    [Theory]
    [InlineData("Neutral", "Neutral")]
    [InlineData("neutral", "Neutral")]
    [InlineData("Düz", "Neutral")]
    [InlineData("Tinted", "Tinted")]
    [InlineData("  tinted ", "Tinted")]
    [InlineData("Artwork", "Artwork")]
    [InlineData("artwork", "Artwork")]
    [InlineData("Albüm kapağı", "Artwork")]
    [InlineData("", "Tinted")]       // boş: varsayılan Albüm rengi
    [InlineData(null, "Tinted")]
    [InlineData("bilinmeyen", "Tinted")]
    public void Normalize_maps_known_and_unknown_values(string? value, string expected) =>
        Assert.Equal(expected, MusicCardStyleSetting.Normalize(value));

    [Fact]
    public void Default_setting_is_tinted() =>
        Assert.Equal(MusicCardStyleSetting.Tinted, new AppSettings().MusicCardStyle);

    [Fact]
    public void Style_survives_a_save_and_load_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-musicstyle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            var settings = new AppSettings { MusicCardStyle = MusicCardStyleSetting.Artwork };

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal(MusicCardStyleSetting.Artwork, loaded.MusicCardStyle);
            Assert.Equal(settings, loaded);
            Assert.NotEqual(new AppSettings(), loaded);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
