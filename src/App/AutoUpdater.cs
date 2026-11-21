using System.Windows.Threading;
using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Koy.Core.Updates;
using Microsoft.Win32;

namespace Koy.App;

/// <summary>
/// "Güncellemeleri otomatik denetle" açıkken açılıştan kısa süre sonra denetler; Koy günlerce açık kalabildiği için
/// sistem uykudan dönünce ve oturum kilidi açılınca da, son denetim eskimişse (<see cref="UpdateCheckSchedule"/>)
/// yeniden denetler. Zamanlayıcı yoktur (AGENTS #5). Yeni sürüm yalnız kaydedilir; indirme ve kurma kullanıcı onayıyla
/// Koy penceresinden yapılır. Kurulu değilse (geliştirme çalıştırması) hiçbir şey yapılmaz.
/// </summary>
internal static class AutoUpdater
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    private static Dispatcher? _dispatcher;
    private static bool _started;

    public static async Task StartAsync()
    {
        if (_started)
            return;
        _started = true;
        _dispatcher = Dispatcher.CurrentDispatcher;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        // Açılış yükü (ağ, TLS, disk) kullanıcı çentik ve dock'u kullanmaya başladıktan sonra binsin.
        await Task.Delay(StartupDelay);
        await CheckIfDueAsync();
    }

    public static void Stop()
    {
        if (!_started)
            return;
        _started = false;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    // SystemEvents kendi iş parçacığından bildirir; denetim arayüz iş parçacığında yürür.
    private static void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            Schedule();
    }

    private static void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
            Schedule();
    }

    private static void Schedule() =>
        _dispatcher?.InvokeAsync(async () =>
        {
            // Uykudan dönüşte ağ birkaç saniye sonra gelir; hemen denenirse boşuna "başarısız" kaydedilir.
            await Task.Delay(TimeSpan.FromSeconds(20));
            await CheckIfDueAsync();
        });

    private static async Task CheckIfDueAsync()
    {
        if (!_started)
            return;

        try
        {
            // Ayar o anki diskteki değerden okunur: kullanıcı oturum içinde kapatmış ya da kanalı değiştirmiş olabilir.
            var general = new SettingsStore(SettingsStore.DefaultPath).Load().General;
            if (!general.AutoCheckUpdates)
                return;

            var lastFailed = UpdateStatus.LastResult == UpdateCheckResult.Failed;
            if (!UpdateCheckSchedule.IsDue(UpdateStatus.LastCheck, lastFailed, DateTime.Now))
                return;

            await UpdateClient.CheckAsync(general.UpdateChannel, "Otomatik güncelleme denetimi");
        }
        catch (Exception ex)
        {
            CrashLog.Write("Otomatik güncelleme denetimi", ex);
        }
    }
}
