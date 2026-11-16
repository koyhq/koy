using System.IO;
using Koy.Core.Diagnostics;
using Koy.Core.Shell;
using Microsoft.Win32;

namespace Koy.Platform.Shell;

public static class StartupService
{
    private static Func<string?> _targetResolver = () => Environment.ProcessPath;
    private static Func<bool> _isInstalledResolver = () => false;

    /// <summary>
    /// App katmanı Velopack bilgisiyle hedef yol çözücüsünü bağlar (Platform katmanı Velopack'e bağımlı olmaz).
    /// </summary>
    public static void Configure(Func<string?> targetResolver, Func<bool>? isInstalledResolver = null)
    {
        _targetResolver = targetResolver ?? (() => Environment.ProcessPath);
        _isInstalledResolver = isInstalledResolver ?? (() => false);
    }

    public static bool IsInstalledBuild => _isInstalledResolver();

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupPathPolicy.RunKeyPath, writable: false);
                var rawCommand = key?.GetValue(StartupPathPolicy.AppValueName) as string;
                if (string.IsNullOrWhiteSpace(rawCommand))
                    return false;

                var approved = ReadStartupApprovedBytes();
                var expectedPath = _targetResolver();
                return StartupPathPolicy.IsEntryValid(rawCommand, approved, expectedPath, File.Exists);
            }
            catch (Exception ex)
            {
                CrashLog.Write("StartupService.IsEnabled", ex);
                return false;
            }
        }
    }

    public static bool SetEnabled(bool enable)
    {
        try
        {
            if (enable)
            {
                var exePath = _targetResolver();
                if (string.IsNullOrWhiteSpace(exePath))
                    return false;

                // Temiz Windows kurulumunda HKCU\...\Run anahtarı henüz oluşmamış olabilir.
                using var key = Registry.CurrentUser.CreateSubKey(StartupPathPolicy.RunKeyPath, writable: true);
                if (key is null)
                    return false;

                key.SetValue(StartupPathPolicy.AppValueName, StartupPathPolicy.FormatCommandLine(exePath));
                ClearStartupApproved();
                return true;
            }

            using (var key = Registry.CurrentUser.OpenSubKey(StartupPathPolicy.RunKeyPath, writable: true))
            {
                key?.DeleteValue(StartupPathPolicy.AppValueName, throwOnMissingValue: false);
            }

            ClearStartupApproved();
            return true;
        }
        catch (Exception ex)
        {
            CrashLog.Write("StartupService.SetEnabled", ex);
            return false;
        }
    }

    /// <summary>
    /// Velopack kurulu sürüm açılırken Run kaydı eski/geliştirme/taşınabilir ya da <c>current\Koy.exe</c>
    /// yoluna bakıyorsa kalıcı kök saplamaya geçirir. Kurulu olmayan çalıştırmada dokunmaz.
    /// </summary>
    public static void ReconcileInstalled()
    {
        if (!_isInstalledResolver())
            return;

        try
        {
            var expectedPath = _targetResolver();
            if (string.IsNullOrWhiteSpace(expectedPath) || !File.Exists(expectedPath))
                return;

            using var key = Registry.CurrentUser.OpenSubKey(StartupPathPolicy.RunKeyPath, writable: true);
            var rawCommand = key?.GetValue(StartupPathPolicy.AppValueName) as string;
            if (key is null || string.IsNullOrWhiteSpace(rawCommand))
                return;

            var approved = ReadStartupApprovedBytes();
            if (StartupPathPolicy.ShouldMigrateInstalledEntry(rawCommand, approved, expectedPath, File.Exists))
            {
                key.SetValue(StartupPathPolicy.AppValueName, StartupPathPolicy.FormatCommandLine(expectedPath));
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("StartupService.ReconcileInstalled", ex);
        }
    }

    private static byte[]? ReadStartupApprovedBytes()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPathPolicy.StartupApprovedRunKeyPath, writable: false);
            return key?.GetValue(StartupPathPolicy.AppValueName) as byte[];
        }
        catch
        {
            return null;
        }
    }

    private static void ClearStartupApproved()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPathPolicy.StartupApprovedRunKeyPath, writable: true);
            key?.DeleteValue(StartupPathPolicy.AppValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            CrashLog.Write("StartupService.ClearStartupApproved", ex);
        }
    }
}
