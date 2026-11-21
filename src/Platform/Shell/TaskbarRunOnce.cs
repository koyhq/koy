using Koy.Core.Diagnostics;
using Koy.Core.Shell;
using Microsoft.Win32;

namespace Koy.Platform.Shell;

/// <summary>
/// HKCU RunOnce yedeği (bkz. <see cref="TaskbarRunOncePolicy"/>): görev çubuğu gizliyken Koy ve gözcü birlikte
/// öldürülürse, bir sonraki oturum açılışında gözcü `--restore-taskbar` ile çalışır. Bu dosya Koy.Watchdog projesine de
/// bağlanır (gözcü Restore'da kaydı siler).
/// </summary>
internal static class TaskbarRunOnce
{
    private static readonly object Gate = new();
    private static string? _written;

    /// <summary>Kaydı yazar (zaten aynıysa registry'ye dokunmaz). Hata günlüğe gider, dock'u bozmaz.</summary>
    public static void Register(string watchdogExePath)
    {
        var command = TaskbarRunOncePolicy.BuildCommand(watchdogExePath);
        lock (Gate)
        {
            if (!TaskbarRunOncePolicy.NeedsWrite(_written, command))
                return;

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(TaskbarRunOncePolicy.KeyPath, writable: true);
                if (TaskbarRunOncePolicy.NeedsWrite(key.GetValue(TaskbarRunOncePolicy.ValueName) as string, command))
                    key.SetValue(TaskbarRunOncePolicy.ValueName, command);

                _written = command;
            }
            catch (Exception ex)
            {
                CrashLog.Write("RunOnce görev çubuğu yedeği yazılamadı", ex);
            }
        }
    }

    /// <summary>Kaydı siler (yoksa sessiz).</summary>
    public static void Unregister()
    {
        lock (Gate)
        {
            _written = null;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(TaskbarRunOncePolicy.KeyPath, writable: true);
                key?.DeleteValue(TaskbarRunOncePolicy.ValueName, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                CrashLog.Write("RunOnce görev çubuğu yedeği silinemedi", ex);
            }
        }
    }
}
