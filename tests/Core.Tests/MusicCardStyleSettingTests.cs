using Koy.Core.Media;
using Koy.Core.Settings;

namespace Koy.Core.Tests;

public class MusicCardStyleSettingTests
{
    [Fact]
    public void Default_setting_is_tinted() =>
        Assert.Equal(MusicCardStyle.Tinted, new AppSettings().MusicCardStyle);

    [Fact]
    public void Style_survives_a_save_and_load_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-musicstyle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            var settings = new AppSettings { MusicCardStyle = MusicCardStyle.Artwork };

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal(MusicCardStyle.Artwork, loaded.MusicCardStyle);
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
