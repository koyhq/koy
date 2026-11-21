namespace Koy.Core.Updates;

/// <summary>
/// Otomatik güncelleme denetiminin ne zaman yeniden yapılacağı. Koy günlerce açık kalabildiği için yalnız açılıştaki
/// denetim yetmez; ama yoklama zamanlayıcısı da kurulmaz (AGENTS #5): denetim, sistem uykudan dönünce ya da oturum
/// kilidi açılınca bu karara sorularak yapılır. Saf C#.
/// </summary>
public static class UpdateCheckSchedule
{
    /// <summary>Başarılı bir denetimden sonra bir sonrakine kadar en az bu kadar beklenir.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    /// <summary>Başarısız denetimden (ağ yok, GitHub yanıt vermedi) sonra daha erken yeniden denenir.</summary>
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(30);

    /// <summary>Şimdi denetlenmeli mi. Hiç denetlenmediyse ya da saat geri alındıysa (son denetim gelecekte) evet.</summary>
    public static bool IsDue(DateTime? lastCheck, bool lastFailed, DateTime now)
    {
        if (lastCheck is not { } last || last > now)
            return true;

        return now - last >= (lastFailed ? RetryAfterFailure : Interval);
    }
}
