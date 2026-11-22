using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Koy.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Koy.App;

/// <summary>
/// Güncelleme kaynağı ve denetimi tek yerde: otomatik denetim (<see cref="AutoUpdater"/>) ve Koy penceresindeki
/// "Şimdi denetle" aynı yoldan geçer, sonuç <see cref="UpdateStatus"/>'a yazılır. Aynı anda tek denetim çalışır.
/// </summary>
internal static class UpdateClient
{
    public const string RepoUrl = "https://github.com/koyhq/koy";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    // Aynı HTTP indirici tüm denetim ve indirmelerde paylaşılır (TCP/TLS bağlantısı yeniden kullanılır).
    private static readonly HttpClientFileDownloader SharedDownloader = new();

    public static UpdateManager CreateManager(UpdateChannel channel)
    {
        // Kararlı kanalda GitHub Releases API + 10 sayfa taramak yerine doğrudan releases/latest/download/releases.win.json okunur.
        IUpdateSource source = UpdateFlowPolicy.UsesDirectLatestFeed(channel)
            ? new SimpleWebSource(UpdateFlowPolicy.LatestDownloadBaseUrl(RepoUrl), SharedDownloader)
            : new GithubSource(RepoUrl, null, true, SharedDownloader);
        return new UpdateManager(source);
    }

    /// <summary>Sürümün GitHub sayfası (bütün notlar).</summary>
    public static string ReleasePageUrl(string version) =>
        $"{RepoUrl}/releases/tag/{UpdateFlowPolicy.FormatDisplayVersion(version, prefixWithV: true)}";

    /// <summary>
    /// Denetler ve sonucu kaydeder. Arayüz iş parçacığından çağrılır (<see cref="UpdateStatus.Changed"/> oradan tetiklenir).
    /// Başka bir denetim sürüyorsa beklemeden döner. İndirilmiş bir güncelleme varken yeniden denetlenmez.
    /// </summary>
    public static async Task CheckAsync(UpdateChannel channel, string logContext)
    {
        if (UpdateStatus.LastResult == UpdateCheckResult.UpdateReady || !await Gate.WaitAsync(0))
            return;

        try
        {
            var manager = CreateManager(channel);
            if (!manager.IsInstalled)
            {
                // Geliştirme çalıştırmasında denetim yapılamaz; başarı gibi gösterilmez.
                UpdateStatus.Record(UpdateCheckResult.NotInstalled);
                return;
            }

            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
                UpdateStatus.Record(UpdateCheckResult.UpToDate);
            else
                UpdateStatus.RecordAvailable(update);
        }
        catch (Exception ex)
        {
            // Ağ yoksa ya da GitHub yanıt vermiyorsa sonuç "başarısız" olarak gösterilir; ayrıntı günlüğe.
            UpdateStatus.Record(UpdateCheckResult.Failed);
            CrashLog.Write(logContext, ex);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Bulunan güncellemeyi arka planda indirir ve kapanışta kurulmak üzere hazır kaydeder.
    /// Aynı anda tek denetim/indirme çalışır; hata olursa UpdateAvailable korunur (elle kurulum açık kalır).
    /// </summary>
    public static async Task DownloadAndStageForExitAsync(UpdateChannel channel, string logContext)
    {
        if (UpdateStatus.LastResult == UpdateCheckResult.UpdateReady || UpdateStatus.AvailableUpdate is null || !await Gate.WaitAsync(0))
            return;

        try
        {
            if (UpdateStatus.LastResult == UpdateCheckResult.UpdateReady || UpdateStatus.AvailableUpdate is not { } update)
                return;

            var manager = CreateManager(channel);
            if (!manager.IsInstalled)
                return;

            await manager.DownloadUpdatesAsync(update);
            UpdateStatus.RecordReady(update.TargetFullRelease);
        }
        catch (Exception ex)
        {
            CrashLog.Write(logContext, ex);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Arka planda indirilmiş güncelleme varsa Koy kapanırken sessizce uygular (App.OnExit sonrası yeniden başlatmaz).
    /// WaitExitThenApplyUpdates 60 sn içinde çıkış beklediği için yalnız gerçek kapanışta çağrılır.
    /// </summary>
    public static void ApplyPendingOnExit()
    {
        if (UpdateStatus.LastResult != UpdateCheckResult.UpdateReady || UpdateStatus.PendingRelease is not { } release)
            return;

        try
        {
            var manager = CreateManager(UpdateChannel.Stable);
            if (!manager.IsInstalled)
                return;

            var target = manager.UpdatePendingRestart ?? release;
            manager.WaitExitThenApplyUpdates(target, silent: true, restart: false);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Kapanışta güncelleme kurulumu", ex);
        }
    }
}
