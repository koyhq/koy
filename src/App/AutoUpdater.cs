using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Velopack;
using Velopack.Sources;

namespace Koy.App;

/// <summary>
/// "Güncellemeleri otomatik denetle" ayarı açıkken açılıştan kısa süre sonra bir kez denetler.
/// Yeni sürüm bulunursa arka planda indirilir ve uygulama kapanırken uygulanır; kullanıcının çalışması yarıda kesilmez
/// (hemen yeniden başlatma yalnız Hakkında sayfasındaki "Şimdi denetle" düğmesiyle olur).
/// Kurulu değilse (geliştirme çalıştırması) hiçbir şey yapılmaz.
/// </summary>
internal static class AutoUpdater
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    public static async Task RunAsync(AppSettings settings)
    {
        if (!settings.AutoCheckUpdates)
            return;

        try
        {
            // Açılış yükü (ağ, TLS, disk) kullanıcı çentik ve dock'u kullanmaya başladıktan sonra binsin.
            await Task.Delay(StartupDelay);

            var source = new GithubSource("https://github.com/koyhq/koy", null, settings.UpdateChannel == UpdateChannel.Preview);
            var manager = new UpdateManager(source);
            if (!manager.IsInstalled)
                return;

            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                UpdateStatus.Record(UpdateCheckResult.UpToDate);
                return;
            }

            // Otomatik indirme ve kurma yapılmaz: kullanıcı onayı için durum kaydedilir.
            UpdateStatus.RecordAvailable(update);
        }
        catch (Exception ex)
        {
            // Ağ yoksa ya da GitHub yanıt vermiyorsa sessizce vazgeç; iz kalsın.
            UpdateStatus.Record(UpdateCheckResult.Failed);
            CrashLog.Write("Otomatik güncelleme denetimi", ex);
        }
    }
}
