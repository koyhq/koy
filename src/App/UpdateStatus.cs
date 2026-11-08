using System.Reflection;
using Velopack;

namespace Koy.App;

public enum UpdateCheckResult
{
    None,
    UpToDate,
    /// <summary>Yeni sürüm bulundu, kullanıcı onayı bekleniyor.</summary>
    UpdateAvailable,
    /// <summary>Yeni sürüm indirildi; Koy kapanınca kurulacak.</summary>
    UpdateReady,
    Failed,
    NotInstalled
}

/// <summary>Bu oturumdaki gerçek güncelleme denetimi sonucu (otomatik ve elle denetim aynı yere yazar). Uydurma zaman gösterilmez.</summary>
public static class UpdateStatus
{
    public static DateTime? LastCheck { get; private set; }
    public static UpdateCheckResult LastResult { get; private set; }
    public static UpdateInfo? AvailableUpdate { get; private set; }
    public static string? AvailableVersion => AvailableUpdate?.TargetFullRelease?.Version?.ToString();
    public static bool HasAvailableUpdate => AvailableUpdate is not null && LastResult == UpdateCheckResult.UpdateAvailable;

    public static event Action? Changed;

    public static void Record(UpdateCheckResult result)
    {
        LastResult = result;
        // Kurulu olmayan sürümde denetim yapılmadığı için zaman kaydedilmez.
        LastCheck = result == UpdateCheckResult.NotInstalled ? LastCheck : DateTime.Now;
        if (result != UpdateCheckResult.UpdateAvailable)
        {
            AvailableUpdate = null;
        }
        Changed?.Invoke();
    }

    public static void RecordAvailable(UpdateInfo update)
    {
        AvailableUpdate = update;
        LastResult = UpdateCheckResult.UpdateAvailable;
        LastCheck = DateTime.Now;
        Changed?.Invoke();
    }
}

/// <summary>Çalışan uygulamanın sürümü.</summary>
public static class AppInfo
{
    public static string Version { get; } = Read();

    private static string Read()
    {
        var asm = typeof(AppInfo).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? asm.GetName().Version?.ToString(3)
                   ?? "?";
        var plus = info.IndexOf('+');
        return plus > 0 ? info[..plus] : info;
    }
}
