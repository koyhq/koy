namespace Koy.Core.Uninstall;

/// <summary>Uygulama içinden kaldırmanın mümkün olup olmadığı.</summary>
public enum UninstallAvailability
{
    /// <summary>Velopack ile kurulu ve kaldırıcı (Update.exe) yerinde.</summary>
    Available,

    /// <summary>Kurulu değil (dotnet run, elle çıkarılmış klasör).</summary>
    NotInstalled,

    /// <summary>Taşınabilir paket: kaldırılacak bir kurulum yok, klasörü silmek yeter.</summary>
    Portable,

    /// <summary>Kurulu görünüyor ama Update.exe bulunamadı (bozuk kurulum).</summary>
    UpdaterMissing
}

/// <summary>
/// Kaldırmanın saf kararları: kaldırıcı komutu, neyin silineceği ve ne zaman mümkün olduğu.
/// Dosya silme işi <see cref="UninstallCleaner"/>'da, süreç başlatma App katmanındadır.
/// </summary>
public static class UninstallPlan
{
    /// <summary>
    /// Velopack kaldırıcısının argümanı. Kaynak: Windows "Yüklü uygulamalar" kaydındaki UninstallString
    /// (<c>"...\Update.exe" --uninstall</c>), sessiz varyantı ek olarak <c>--silent</c> alır.
    /// Sessiz değil: kaldırıcı kendi ilerleme ve "kaldırıldı" penceresini gösterir, kullanıcı bitişi görür.
    /// </summary>
    public const string UpdaterArguments = "--uninstall";

    /// <summary>Kullanıcı tercihleri ve içeriği: yalnız "ayarlarımı sakla" kapalıysa silinir.</summary>
    public static readonly IReadOnlyList<string> SettingFiles =
    [
        "settings.json", "settings.json.tmp", "shelf.json", "events.json", "events.json.tmp",
        // Harici takvim: hesap ayarları, DPAPI ile şifreli belirteçler ve çevrimdışı etkinlik önbelleği.
        "calendar-settings.json", "calendar-settings.json.tmp",
        "calendar-token-google.dat", "calendar-token-google.dat.tmp",
        "calendar-token-outlook.dat", "calendar-token-outlook.dat.tmp",
        "calendar-cache.json", "calendar-cache.json.tmp"
    ];

    /// <summary>Çalışma durumu: yeniden kurulumda işe yaramaz, her kaldırmada silinir.</summary>
    public static readonly IReadOnlyList<string> StateFiles = ["taskbar-state.json"];

    /// <summary>Tanı kayıtları: Velopack kök klasörüyle zaten gider; ayrıca hiçbir şey bırakılmaz.</summary>
    public static readonly IReadOnlyList<string> DiagnosticFiles = ["crash.log", "crash.log.old"];

    /// <summary>
    /// Windows'un "Yüklü uygulamalar"ından kaldırmada ayarlar silinmez: kullanıcı Koy'u yeniden kurarsa
    /// tercihleri korunur. Bu yolda onay ekranı yoktur (Velopack kancası 30 sn sınırlı ve iptal edilemez).
    /// </summary>
    public const bool KeepSettingsOnExternalUninstall = true;

    public static UninstallAvailability Evaluate(bool isInstalled, bool isPortable, string? updateExePath, Func<string, bool> fileExists)
    {
        if (isPortable)
            return UninstallAvailability.Portable;
        if (!isInstalled)
            return UninstallAvailability.NotInstalled;
        if (string.IsNullOrWhiteSpace(updateExePath) || !fileExists(updateExePath))
            return UninstallAvailability.UpdaterMissing;
        return UninstallAvailability.Available;
    }

    /// <summary>
    /// Silinecek dosyaların tam yolları. Ayarlar %AppData%\Koy (roaming), tanı kayıtları %LOCALAPPDATA%\Koy altındadır;
    /// kurulu sürümde ikincisi Velopack'in kök klasörüdür, bu yüzden klasöre dokunulmaz, yalnız bilinen dosyalar silinir.
    /// </summary>
    public static IReadOnlyList<string> FilesToDelete(string roamingDir, string localDir, bool keepSettings)
    {
        var files = new List<string>();
        if (!keepSettings)
            files.AddRange(SettingFiles.Select(f => Path.Combine(roamingDir, f)));
        files.AddRange(StateFiles.Select(f => Path.Combine(roamingDir, f)));
        files.AddRange(DiagnosticFiles.Select(f => Path.Combine(localDir, f)));
        return files;
    }
}
