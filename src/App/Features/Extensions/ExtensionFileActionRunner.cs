using System.IO;
using System.Text;
using Koy.App.Features.Settings;
using Koy.Core.Appearance;
using Koy.Core.Extensions.Store;
using Koy.Core.Settings.Profiles;

namespace Koy.App.Features.Extensions;

/// <summary>Dosya işlerinin sonucu: hata varsa resx anahtarı; profil içe alındıysa kayıt (zaten varsa <see cref="ProfileAlreadyPresent"/>).</summary>
public sealed record ExtensionFileActionResult(string? ErrorKey, SavedProfile? ImportedProfile = null, bool ProfileAlreadyPresent = false)
{
    public bool IsSuccess => ErrorKey is null;
}

/// <summary>
/// <see cref="ExtensionApplyPlanner"/> planının dosya işlerini yapar (kararlar Core'da, burada yalnız G/Ç). Kullanıcı stil
/// dosyası (user-theme.json) ve yedeği aynı klasörde durur; her yazma geçici dosya + taşıma ile yapılır. Beklenen G/Ç hatası
/// fırlatmaz, anahtar döner.
/// </summary>
public sealed class ExtensionFileActionRunner(string userStyleFolder, ProfileService profiles)
{
    public string UserStylePath { get; } = Path.Combine(userStyleFolder, UserStyleFile.FileName);

    public string BackupPath { get; } = Path.Combine(userStyleFolder, ExtensionApplyPlanner.UserStyleBackupFileName);

    public bool BackupExists => File.Exists(BackupPath);

    public ExtensionFileActionResult Run(IEnumerable<ExtensionFileAction> actions)
    {
        SavedProfile? imported = null;
        var already = false;
        try
        {
            foreach (var action in actions)
            {
                switch (action.Kind)
                {
                    case ExtensionFileActionKind.BackupUserStyle:
                        // İlk özgün dosya kalsın: yedek varsa üzerine yazılmaz.
                        if (File.Exists(UserStylePath) && !File.Exists(BackupPath))
                            File.Copy(UserStylePath, BackupPath);
                        break;

                    case ExtensionFileActionKind.CopyThemeToUserStyle:
                        if (action.SourcePath is not { } source || !File.Exists(source))
                            return new("ExtApplyFileError");
                        Directory.CreateDirectory(userStyleFolder);
                        var temp = UserStylePath + ".tmp";
                        File.Copy(source, temp, overwrite: true);
                        File.Move(temp, UserStylePath, overwrite: true);
                        break;

                    case ExtensionFileActionKind.RestoreUserStyleBackup:
                        if (File.Exists(BackupPath))
                            File.Move(BackupPath, UserStylePath, overwrite: true);
                        else if (File.Exists(UserStylePath))
                            File.Delete(UserStylePath);
                        break;

                    case ExtensionFileActionKind.ImportProfile:
                        var result = ImportProfile(action.SourcePath);
                        if (!result.IsSuccess)
                            return result;
                        imported = result.ImportedProfile;
                        already = result.ProfileAlreadyPresent;
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return new("ExtApplyFileError");
        }

        return new(null, imported, already);
    }

    private ExtensionFileActionResult ImportProfile(string? path)
    {
        if (path is null || !File.Exists(path))
            return new("ExtApplyFileError");

        if (new FileInfo(path).Length > ProfileLibrary.MaxFileBytes)
            return new("ProfileImportErrorTooLarge");

        var read = SettingsProfileCodec.Read(File.ReadAllText(path, Encoding.UTF8));
        if (!read.IsValid)
            return new("ProfileImportError" + read.Error);

        // Aynı ad kitaplıkta varsa bir daha eklenmez ("Ad (2)" yığılmasın): "Uygula" tekrar basılabilir.
        if (ProfileLibrary.Find(profiles.List(), read.Profile!.Name) is { } existing)
            return new(null, existing, ProfileAlreadyPresent: true);

        var added = profiles.AddImported(read.Profile!);
        return added.IsSuccess ? new(null, added.Profile) : new("ProfileError" + added.Error);
    }
}
