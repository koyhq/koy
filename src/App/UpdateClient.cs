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

    public static UpdateManager CreateManager(UpdateChannel channel) =>
        new(new GithubSource(RepoUrl, null, channel == UpdateChannel.Preview));

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
}
