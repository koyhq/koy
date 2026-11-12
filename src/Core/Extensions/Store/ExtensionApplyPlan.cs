using Koy.Core.Appearance;
using Koy.Core.Notch;
using Koy.Core.Settings;

namespace Koy.Core.Extensions.Store;

public enum ExtensionApplyError
{
    None,

    /// <summary>Kurulum klasörü ya da ana dosya yolu güvenli bir yerel yol değil.</summary>
    InvalidPath,
}

/// <summary>Plana göre App/Platform'un yapacağı dosya işi. Plan dosyaya dokunmaz, yalnız ne yapılacağını söyler.</summary>
public enum ExtensionFileActionKind
{
    /// <summary>
    /// Kullanıcının ÖZGÜN stil durumunu sakla: user-theme.json varsa user-theme.backup.json olarak kopyala, her durumda
    /// özgün <c>UserStyleOverridesEnabled</c> değerini (<see cref="ExtensionFileAction.OriginalOverridesEnabled"/>) durum
    /// dosyasına yaz ("özgün dosya yoktu" da böyle kaydedilir). Yedek ya da durum dosyası zaten varsa dokunma (ilk özgün kalsın).
    /// </summary>
    BackupUserStyle,

    /// <summary><see cref="ExtensionFileAction.SourcePath"/> dosyasını user-theme.json olarak kopyala.</summary>
    CopyThemeToUserStyle,

    /// <summary>Yedek varsa user-theme.json üzerine geri taşı; yoksa (özgün dosya yoktu) user-theme.json'u sil. Durum dosyasını da temizler.</summary>
    RestoreUserStyleBackup,

    /// <summary>
    /// <see cref="ExtensionFileAction.SourcePath"/> profil dosyasını oku, <c>SettingsProfileCodec.Read</c> ile doğrula ve
    /// <c>ProfileLibrary.AddImported</c> ile kitaplığa ekle. Ayarlara uygulanmaz.
    /// </summary>
    ImportProfile,
}

public sealed record ExtensionFileAction(
    ExtensionFileActionKind Kind, string? SourcePath = null, bool OriginalOverridesEnabled = false);

/// <summary>
/// Tema uygulanmadan önceki kullanıcı stil durumu (diskteki yedekten okunur). <see cref="HasFile"/>: özgün user-theme.json
/// vardı (yedeği duruyor); false ise özgün durumda dosya yoktu. <see cref="OverridesEnabled"/>: özgün
/// <c>UserStyleOverridesEnabled</c>. Yedek de durum dosyası da yoksa çağıran <c>null</c> verir.
/// </summary>
public sealed record UserStyleBackup(bool HasFile, bool OverridesEnabled)
{
    /// <summary>Durum dosyasının adı (user-theme.json ile aynı klasörde).</summary>
    public const string StateFileName = "user-theme.backup.state";

    public static string SerializeState(bool overridesEnabled) => "overrides=" + (overridesEnabled ? "true" : "false");

    public static bool TryParseState(string? text, out bool overridesEnabled)
    {
        overridesEnabled = false;
        switch (text?.Trim())
        {
            case "overrides=true":
                overridesEnabled = true;
                return true;
            case "overrides=false":
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// Uygula/Kaldır kararı. <see cref="Settings"/> yeni ayarlardır (çağıranın verdiği nesne değişmez); değişiklik yoksa
/// aynı değerler taşıyan bir kopyadır. Dosya işleri sırayla yapılır, sonra ayarlar kaydedilir.
/// </summary>
public sealed record ExtensionApplyPlan(
    ExtensionApplyError Error,
    AppSettings? Settings,
    IReadOnlyList<ExtensionFileAction> FileActions)
{
    public bool IsSuccess => Error == ExtensionApplyError.None && Settings is not null;

    public static ExtensionApplyPlan Fail(ExtensionApplyError error) => new(error, null, []);
}

/// <summary>
/// "Uygula" ve kaldırmada ayar geri alma kararları (docs/EXTENSION-STORE.md › Uygulama). Saf: dosya sistemi ve
/// ayar dosyası yok; mevcut alanlar kullanılır (IconPack, IconPackFolder, UserStyleOverridesEnabled,
/// Notch.LiveBackground*).
/// </summary>
public static class ExtensionApplyPlanner
{
    public const string UserStyleBackupFileName = "user-theme.backup.json";

    /// <summary>Uygulanabilir ayar karşılığı olan türden eklentiyi uygulayan plan.</summary>
    /// <param name="themeAlreadyApplied">
    /// Tema için: kayıtta zaten uygulanmış bir mağaza teması var (bu dahil). O zaman şu anki user-theme.json bir
    /// mağaza temasıdır, kullanıcının özgünü değil; yedek alınmaz (ilk uygulamadaki özgün yedek korunur).
    /// </param>
    public static ExtensionApplyPlan Apply(
        AppSettings current, InstalledEntry entry, string installFolder, bool themeAlreadyApplied = false)
    {
        if (!IconPackFiles.IsSafeFolder(installFolder))
        {
            return ExtensionApplyPlan.Fail(ExtensionApplyError.InvalidPath);
        }

        switch (entry.Kind)
        {
            case ExtensionKind.Theme:
            {
                var source = Path.Combine(installFolder, PackageEntryPolicy.ThemeName);
                var settings = current with { Appearance = current.Appearance with { UserStyleOverridesEnabled = true } };
                List<ExtensionFileAction> actions = [];
                if (!themeAlreadyApplied)
                {
                    actions.Add(new(
                        ExtensionFileActionKind.BackupUserStyle,
                        OriginalOverridesEnabled: current.Appearance.UserStyleOverridesEnabled));
                }

                actions.Add(new(ExtensionFileActionKind.CopyThemeToUserStyle, source));
                return new(ExtensionApplyError.None, settings, actions);
            }

            case ExtensionKind.IconPack:
            {
                var settings = current with
                {
                    Appearance = current.Appearance with
                    {
                        IconPack = IconPack.Folder,
                        IconPackFolder = IconsFolderOf(installFolder),
                    },
                };
                return new(ExtensionApplyError.None, settings, []);
            }

            case ExtensionKind.Background:
            {
                var path = BackgroundPathOf(entry, installFolder);
                if (path is null || LiveBackgroundFileValidator.Validate(path, 1) != LiveBackgroundFileError.None)
                {
                    return ExtensionApplyPlan.Fail(ExtensionApplyError.InvalidPath);
                }

                // Hedef kapalıysa çentik: yoksa arka planı seçmek hiçbir şey göstermezdi.
                var target = current.Notch.LiveBackgroundTarget == LiveBackgroundTarget.None
                    ? LiveBackgroundTarget.Notch
                    : current.Notch.LiveBackgroundTarget;
                var settings = current with
                {
                    Notch = current.Notch with
                    {
                        LiveBackgroundSource = LiveBackgroundSourceKind.LocalFile,
                        LiveBackgroundFilePath = path,
                        LiveBackgroundTarget = target,
                    },
                };
                return new(ExtensionApplyError.None, settings, []);
            }

            case ExtensionKind.Profile:
                // Profil uygulanmaz, yalnız kitaplığa girer; kullanıcı Profiller bölümünden uygular.
                return new(ExtensionApplyError.None, current with { }, [
                    new(ExtensionFileActionKind.ImportProfile, Path.Combine(installFolder, PackageEntryPolicy.ProfileName)),
                ]);

            default:
                return ExtensionApplyPlan.Fail(ExtensionApplyError.InvalidPath);
        }
    }

    /// <summary>
    /// Kaldırmadan önce ayarı geri alan plan: simge paketi → Yerleşik, arka plan → Ambiyans, tema → yedek geri
    /// yüklenir. Simge paketi ve arka plan için ayarın hâlâ bu eklentiyi göstermesine bakılır (kullanıcı sonradan
    /// başka bir şey seçtiyse dokunulmaz); tema için kayıttaki <c>Applied</c> bayrağına. Hedef (Notch / Müzik kartı)
    /// değişmez. Profil türünde yapılacak bir şey yoktur (içe alınan profil kitaplıkta kalır).
    /// </summary>
    /// <param name="backup">
    /// Tema için: diskteki özgün durum yedeği. Özgün dosya varsa geri yüklenir, yoksa user-theme.json silinir;
    /// geçersiz kılma her iki durumda özgün değerine döner. Yedek hiç yoksa (null) dosya silinir ve kapatılır.
    /// </param>
    public static ExtensionApplyPlan RevertOnUninstall(
        AppSettings current, InstalledEntry entry, string installFolder, UserStyleBackup? backup = null)
    {
        switch (entry.Kind)
        {
            case ExtensionKind.Theme when entry.Applied:
                return new(ExtensionApplyError.None,
                    current with
                    {
                        Appearance = current.Appearance with { UserStyleOverridesEnabled = backup?.OverridesEnabled ?? false },
                    },
                    [new(ExtensionFileActionKind.RestoreUserStyleBackup)]);

            case ExtensionKind.IconPack when PointsAtIconPack(current, installFolder):
                return new(ExtensionApplyError.None,
                    current with
                    {
                        Appearance = current.Appearance with { IconPack = IconPack.Builtin, IconPackFolder = "" },
                    },
                    []);

            case ExtensionKind.Background when PointsAtBackground(current, entry, installFolder):
                return new(ExtensionApplyError.None,
                    current with
                    {
                        Notch = current.Notch with
                        {
                            LiveBackgroundSource = LiveBackgroundSourceKind.BuiltInAmbient,
                            LiveBackgroundFilePath = "",
                        },
                    },
                    []);

            default:
                return new(ExtensionApplyError.None, current with { }, []);
        }
    }

    /// <summary>
    /// Eklenti şu an etkin mi (arayüzde "Uygulandı" rozeti). Simge paketi ve arka plan için ayarın gösterdiği yola,
    /// tema için kayıt bayrağı ve geçersiz kılmanın açık olmasına bakılır; profil hiçbir zaman etkin sayılmaz.
    /// </summary>
    public static bool IsActive(AppSettings current, InstalledEntry entry, string installFolder) => entry.Kind switch
    {
        ExtensionKind.Theme => entry.Applied && current.Appearance.UserStyleOverridesEnabled,
        ExtensionKind.IconPack => PointsAtIconPack(current, installFolder),
        ExtensionKind.Background => PointsAtBackground(current, entry, installFolder),
        _ => false,
    };

    private static string IconsFolderOf(string installFolder) => Path.Combine(installFolder, PackageEntryPolicy.IconsFolder);

    private static string? BackgroundPathOf(InstalledEntry entry, string installFolder) =>
        PackageEntryPolicy.IsBackgroundName(entry.PayloadFile) ? Path.Combine(installFolder, entry.PayloadFile) : null;

    private static bool PointsAtIconPack(AppSettings s, string installFolder) =>
        s.Appearance.IconPack == IconPack.Folder
        && SamePath(s.Appearance.IconPackFolder, IconsFolderOf(installFolder));

    private static bool PointsAtBackground(AppSettings s, InstalledEntry entry, string installFolder) =>
        s.Notch.LiveBackgroundSource == LiveBackgroundSourceKind.LocalFile
        && BackgroundPathOf(entry, installFolder) is { } path
        && SamePath(s.Notch.LiveBackgroundFilePath, path);

    // Yol karşılaştırması: büyük/küçük harf duyarsız, sondaki ayırıcı yok sayılır.
    private static bool SamePath(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
        && string.Equals(a.Trim().TrimEnd('\\', '/'), b.Trim().TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
