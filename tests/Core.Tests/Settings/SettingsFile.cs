using Koy.Core.Settings;

namespace Koy.Core.Tests;

/// <summary>Testlerde ham JSON'u gerçek <see cref="SettingsStore"/> yolundan (göç dahil) okur.</summary>
internal static class SettingsFile
{
    /// <summary>Altın dosya: v0.2.6'nın yazdığı biçimde, her alanı varsayılandan farklı v3 ayarları.</summary>
    public static string FixtureV3 { get; } = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Settings", "Fixtures", "settings-v3.json"));

    public static AppSettings Load(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-sf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, json);
            return new SettingsStore(path).Load();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
