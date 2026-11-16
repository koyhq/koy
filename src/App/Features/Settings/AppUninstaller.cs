using System.Diagnostics;
using System.IO;
using System.Windows;
using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Koy.Core.Uninstall;
using Koy.Platform.Shell;
using Velopack.Locators;

namespace Koy.App.Features.Settings;

/// <summary>
/// Uygulama içinden kaldırma. Karar ve silme mantığı Core'da (<see cref="UninstallPlan"/>, <see cref="UninstallCleaner"/>);
/// burada yalnız Velopack'in yerini bulmak, kaldırıcıyı başlatmak ve Koy'u kapatmak var.
/// </summary>
internal static class AppUninstaller
{
    /// <summary>Kaldırma başladı: ayarlar artık yazılmamalı (kapanırken pencereler kaydetmeye çalışır).</summary>
    public static bool InProgress { get; private set; }

    private static bool _deleteSettings;

    private static string RoamingDir => Path.GetDirectoryName(SettingsStore.DefaultPath)!;
    private static string LocalDir => Path.GetDirectoryName(CrashLog.DefaultPath)!;

    public static UninstallAvailability GetAvailability()
    {
        try
        {
            var locator = VelopackLocator.Current;
            return UninstallPlan.Evaluate(
                isInstalled: locator.CurrentlyInstalledVersion is not null,
                isPortable: locator.IsPortable,
                locator.UpdateExePath,
                File.Exists);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Kaldırma durumu okunamadı", ex);
            return UninstallAvailability.NotInstalled;
        }
    }

    /// <summary>
    /// Sıra: görev çubuğunu geri getir, verileri sil, Velopack kaldırıcısını (<c>Update.exe --uninstall</c>) başlat, Koy'u kapat.
    /// Kaldırıcı Koy süreçlerini kendisi sonlandırır; yine de kapanış ayrıca istenir.
    /// Kaldırıcı başlamazsa false döner ve Koy çalışmaya devam eder.
    /// </summary>
    public static bool Begin(bool keepSettings)
    {
        if (GetAvailability() != UninstallAvailability.Available)
            return false;

        var updateExe = VelopackLocator.Current.UpdateExePath!;

        InProgress = true;
        _deleteSettings = !keepSettings;

        App.SafeRestoreTaskbar();
        StartupService.SetEnabled(false);
        var clean = UninstallCleaner.Run(RoamingDir, LocalDir, keepSettings);
        if (clean.Failed.Count > 0)
            CrashLog.Write("Kaldırma: silinemeyen dosyalar", string.Join(Environment.NewLine, clean.Failed));

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = updateExe,
                Arguments = UninstallPlan.UpdaterArguments,
                WorkingDirectory = Path.GetDirectoryName(updateExe)!,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            InProgress = false;
            CrashLog.Write("Kaldırıcı başlatılamadı", ex);
            return false;
        }

        Application.Current?.Shutdown();
        return true;
    }

    /// <summary>Kapanışta bir kez daha: pencereler kapanırken yeniden yazılmış olabilecek ayar dosyalarını sil.</summary>
    public static void CleanAfterExit()
    {
        if (InProgress)
            UninstallCleaner.Run(RoamingDir, LocalDir, keepSettings: !_deleteSettings);
    }

    /// <summary>
    /// Velopack "kaldırmadan önce" kancası (Windows'un Yüklü uygulamalar'ı ya da uygulama içi kaldırma).
    /// Onay ekranı yok: 30 sn sınırlı, iptal edilemez. Görev çubuğu geri gelir, otomatik başlatma kaydı silinir,
    /// durum ve tanı dosyaları gider; ayarlar silinmez (<see cref="UninstallPlan.KeepSettingsOnExternalUninstall"/>).
    /// Her adım ayrı korunur: biri patlasa diğerleri çalışır.
    /// </summary>
    public static void OnBeforeUninstallHook()
    {
        App.SafeRestoreTaskbar();

        try
        {
            if (!StartupService.SetEnabled(false))
                CrashLog.Write("Kaldırma kancası: başlatma kaydı silinemedi", "StartupService.SetEnabled(false) başarısız oldu.");
        }
        catch (Exception ex)
        {
            CrashLog.Write("Kaldırma kancası: başlatma kaydı silinemedi", ex);
        }

        try { UninstallCleaner.Run(RoamingDir, LocalDir, UninstallPlan.KeepSettingsOnExternalUninstall); }
        catch (Exception ex) { CrashLog.Write("Kaldırma kancası: dosyalar silinemedi", ex); }
    }
}
