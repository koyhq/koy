using System.Globalization;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class AppLanguageTests
{
    [Theory]
    [InlineData("tr-TR", "tr")]
    [InlineData("tr", "tr")]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "en")]
    [InlineData("ar-SA", "en")]
    public void First_run_language_follows_the_system(string culture, string expected) =>
        Assert.Equal(expected, AppLanguage.FromCulture(CultureInfo.GetCultureInfo(culture)));

    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    public void Saved_language_is_kept(string saved)
    {
        var path = Path.Combine(Path.GetTempPath(), "koy-lang-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new AppSettings { Language = saved });
            Assert.Equal(saved, store.Load().Language);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
