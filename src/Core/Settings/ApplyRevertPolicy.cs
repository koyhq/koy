namespace Koy.Core.Settings;

/// <summary>Kullanıcının riskli ayar için verdiği yanıt; yoksa <see cref="None"/>.</summary>
public enum ApplyRevertChoice
{
    None,
    Confirm,
    Revert,
}

/// <summary>Riskli ayarın ne olacağı: beklemeye devam, değeri tut ya da önceki değere dön.</summary>
public enum ApplyRevertDecision
{
    Wait,
    Keep,
    Revert,
}

/// <summary>
/// Riskli bir ayar (dock konumu, görev çubuğu gizleme, monitör ataması) uygulandıktan sonra, kullanıcı onaylamazsa
/// önceki değere dönme kararı. Saf: zaman ve kullanıcı yanıtı dışarıdan verilir, zamanlayıcı App'te kurulur.
/// Açık onay her zaman zaman aşımından önce gelir; onay yoksa süre dolunca geri alınır.
/// </summary>
public static class ApplyRevertPolicy
{
    /// <summary>Onay için varsayılan süre (~15 sn).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static ApplyRevertDecision Decide(DateTimeOffset appliedAt, DateTimeOffset now, ApplyRevertChoice choice, TimeSpan timeout)
    {
        ValidateTimeout(timeout);

        if (choice == ApplyRevertChoice.Confirm)
        {
            return ApplyRevertDecision.Keep;
        }

        if (choice == ApplyRevertChoice.Revert)
        {
            return ApplyRevertDecision.Revert;
        }

        return now - appliedAt >= timeout ? ApplyRevertDecision.Revert : ApplyRevertDecision.Wait;
    }

    /// <summary>Onay penceresinde kalan süre; dolmuşsa sıfır.</summary>
    public static TimeSpan Remaining(DateTimeOffset appliedAt, DateTimeOffset now, TimeSpan timeout)
    {
        ValidateTimeout(timeout);

        // Saat geriye giderse (now < appliedAt) kalan süre zaman aşımını aşmasın. Çıkarmadan önce bakılır:
        // çok büyük zaman aşımında timeout - (negatif) TimeSpan'i taşırırdı.
        var elapsed = now - appliedAt;
        if (elapsed <= TimeSpan.Zero) return timeout;

        var left = timeout - elapsed;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Zaman aşımı pozitif olmalı.");
        }
    }
}
