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
/// Kullanıcı stil dosyalarının (user-theme.json, yedeği, durum dosyası) bellekteki kopyası. Ayar kaydı başarısız olursa
/// dosyalar buna döndürülür. Her alan null ise dosya yoktu.
/// </summary>
public sealed record UserStyleSnapshot(byte[]? Style, byte[]? Backup, byte[]? State);

/// <summary>
/// <see cref="ExtensionApplyPlanner"/> planının dosya işlerini yapar (kararlar Core'da, burada yalnız G/Ç). Kullanıcı stil
/// dosyası (user-theme.json) ve yedeği aynı klasörde durur; her yazma geçici dosya + taşıma ile yapılır. Beklenen G/Ç hatası
/// fırlatmaz, anahtar döner.
/// </summary>
public sealed class ExtensionFileActionRunner(string userStyleFolder, ProfileService profiles)
{
    public string UserStylePath { get; } = Path.Combine(userStyleFolder, UserStyleFile.FileName);

    public string BackupPath { get; } = Path.Combine(userStyleFolder, ExtensionApplyPlanner.UserStyleBackupFileName);

    public string StatePath { get; } = Path.Combine(userStyleFolder, UserStyleBackup.StateFileName);

    /// <summary>
    /// Diskteki özgün durum yedeği; hiç yedek yoksa null. Durum dosyası yoksa (eski sürümün yedeği) özgün
    /// geçersiz kılma, yedek dosyasının varlığından çıkarılır.
    /// </summary>
    public UserStyleBackup? ReadBackup()
    {
        var hasFile = File.Exists(BackupPath);
        bool? overrides = null;
        try
        {
            if (File.Exists(StatePath) && UserStyleBackup.TryParseState(File.ReadAllText(StatePath, Encoding.UTF8), out var value))
                overrides = value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Okunamayan durum dosyası yok sayılır.
        }

        if (!hasFile && overrides is null && !File.Exists(StatePath))
            return null;

        return new UserStyleBackup(hasFile, overrides ?? hasFile);
    }

    /// <summary>user-theme.json ve yedek dosyalarının geri yüklenebilir kopyası (okunamazsa null).</summary>
    public UserStyleSnapshot? TakeSnapshot()
    {
        try
        {
            return new UserStyleSnapshot(ReadOrNull(UserStylePath), ReadOrNull(BackupPath), ReadOrNull(StatePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Dosyaları <see cref="TakeSnapshot"/> anına döndürür (en iyi çaba; başarıyı bildirir).</summary>
    public bool RestoreSnapshot(UserStyleSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(userStyleFolder);
            WriteOrDelete(UserStylePath, snapshot.Style);
            WriteOrDelete(BackupPath, snapshot.Backup);
            WriteOrDelete(StatePath, snapshot.State);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static byte[]? ReadOrNull(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

    private static void WriteOrDelete(string path, byte[]? data)
    {
        if (data is null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        var temp = path + ".tmp";
        File.WriteAllBytes(temp, data);
        File.Move(temp, path, overwrite: true);
    }

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
                        // İlk özgün durum kalsın: yedek ya da durum dosyası varsa üzerine yazılmaz. Önce dosya, sonra durum
                        // yazılır (arada çökerse durum yok, yedek var: eski sürümün yedeği gibi okunur).
                        if (File.Exists(BackupPath) || File.Exists(StatePath))
                            break;
                        Directory.CreateDirectory(userStyleFolder);
                        if (File.Exists(UserStylePath))
                            File.Copy(UserStylePath, BackupPath);
                        var stateTemp = StatePath + ".tmp";
                        File.WriteAllText(stateTemp, UserStyleBackup.SerializeState(action.OriginalOverridesEnabled), Encoding.UTF8);
                        File.Move(stateTemp, StatePath, overwrite: true);
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
                        if (File.Exists(StatePath))
                            File.Delete(StatePath);
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
