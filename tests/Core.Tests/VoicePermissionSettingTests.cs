using Koy.Core.Settings;
using Koy.Core.Voice;

namespace Koy.Core.Tests;

public class VoicePermissionSettingTests
{
    [Fact]
    public void Default_permission_is_read_only() =>
        Assert.Equal(VoiceClaudePermission.Default, new AppSettings().Voice.ClaudePermission);

    [Fact]
    public void Settings_file_without_the_key_stays_read_only()
    {
        // Eski kurulumun ayar dosyasında anahtar yok: güncelleme izni sessizce genişletmemeli.
        var dir = Path.Combine(Path.GetTempPath(), "koy-voiceperm-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, """{ "VoiceHarness": "Claude", "VoiceModel": "default" }""");

            var loaded = new SettingsStore(path).Load();

            Assert.Equal(VoiceClaudePermission.Default, loaded.Voice.ClaudePermission);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(VoiceClaudePermission.Auto)]
    [InlineData(VoiceClaudePermission.AcceptEdits)]
    public void Chosen_permission_and_model_survive_a_save_and_load_round_trip(VoiceClaudePermission permission)
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-voiceperm-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            var settings = new AppSettings { Voice = new VoiceSettings { ClaudePermission = permission, Model = "sonnet" } };

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal(permission, loaded.Voice.ClaudePermission);
            Assert.Equal("sonnet", loaded.Voice.Model);
            Assert.Equal(settings, loaded);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
