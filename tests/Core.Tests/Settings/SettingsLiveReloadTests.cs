using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

/// <summary>Canlı yeniden yükleme için katı okuma ve kendi kaydını tanıma (issue #69).</summary>
public sealed class SettingsLiveReloadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-live-" + Guid.NewGuid());

    private string FilePath => Path.Combine(_dir, "settings.json");

    private SettingsStore NewStore() => new(FilePath);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Own_save_is_recognized_and_external_edit_is_not()
    {
        var store = NewStore();
        store.Save(new AppSettings());
        Assert.True(store.IsOwnWrite());
        // Aynı dosyaya bakan başka bir örnek de kendi kaydını tanır (yol başına tutulur).
        Assert.True(NewStore().IsOwnWrite());

        var text = File.ReadAllText(FilePath);
        File.WriteAllText(FilePath, text);
        File.SetLastWriteTimeUtc(FilePath, DateTime.UtcNow.AddSeconds(5));

        Assert.False(store.IsOwnWrite());
    }

    [Fact]
    public void Strict_read_accepts_a_valid_current_file()
    {
        var store = NewStore();
        var settings = new AppSettings { General = new GeneralSettings { Language = "en" } };
        store.Save(settings);

        Assert.True(store.TryReadStrict(out var read));
        Assert.Equal("en", read.General.Language);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("""{ "Version": 4, "Dock": { "Style": "Bogus" } }""")]
    [InlineData("""{ "Version": 3, "DockEnabled": true }""")]
    public void Strict_read_rejects_broken_unknown_or_old_files(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, json);

        Assert.False(NewStore().TryReadStrict(out var read));
        Assert.Equal(new AppSettings(), read);
    }

    [Fact]
    public void Strict_read_of_missing_file_is_false() => Assert.False(NewStore().TryReadStrict(out _));
}
