using System.Globalization;

namespace Koy.Core.Shell;

/// <summary>Gözcünün Koy sürecini beklerken aldığı sonuç.</summary>
public enum WatchdogWaitResult
{
    /// <summary>Koy süreci sonlandı (Görev Yöneticisi, çökme ya da normal çıkış).</summary>
    ProcessExited,

    /// <summary>Koy "temiz çıkış" işaretini bıraktı (dock durdu, görev çubuğunu kendisi geri getirdi).</summary>
    StopRequested,

    /// <summary>Gözcü başladığında Koy süreci zaten yoktu.</summary>
    ProcessNotFound,

    /// <summary>Bekleme beklenmedik biçimde başarısız oldu.</summary>
    Failed
}

public enum WatchdogAction
{
    /// <summary>Hiçbir şey yapma, çık.</summary>
    None,

    /// <summary>Windows görev çubuğunu geri getir.</summary>
    RestoreTaskbar
}

/// <summary>
/// Gözcü sürecin (Koy.Watchdog.exe) saf kararları: argüman çözümü, ortak adlar, bekleme sonucundan eylem.
/// Win32 yok; hem Koy hem gözcü aynı adları kullanır.
/// </summary>
public static class WatchdogPolicy
{
    public const string PidArgument = "--pid";

    /// <summary>
    /// `--restore-taskbar`: Koy'u beklemez; durum dosyası "gizli" diyorsa (ya da bozuksa) görev çubuğunu geri getirip çıkar.
    /// HKCU RunOnce kaydı bunu oturum açılışında çalıştırır (<see cref="TaskbarRunOncePolicy"/>).
    /// </summary>
    public const string RestoreOnlyArgument = "--restore-taskbar";

    /// <summary>Koy'un tek örnek muteksi (App/SingleInstance ile aynı ad). Koy çalışıyorsa yalnız-geri-getir kipi dokunmaz.</summary>
    public const string KoySingleInstanceMutexName = @"Local\Koy.SingleInstance";

    /// <summary>Yalnız-geri-getir kipinde Gezgin'in görev çubuğunu bekleme üst sınırı (oturum açılışında geç oluşur).</summary>
    public const int RestoreOnlyExplorerWaitMs = 60_000;

    /// <summary>Aynı anda tek gözcü: ikinci örnek bu muteksi alamayınca hemen çıkar.</summary>
    public const string SingletonMutexName = @"Local\Koy.Watchdog.Singleton";

    /// <summary>Yalnız-geri-getir kipinin ayrı muteksi: Koy'un watch kipindeki gözcüsünü engellemesin.</summary>
    public const string RestoreOnlySingletonMutexName = @"Local\Koy.Watchdog.RestoreOnly.Singleton";

    /// <summary>Koy'un temiz çıkış işareti için adlandırılmış olay (Koy PID'ine özel).</summary>
    public static string StopEventName(int koyProcessId)
        => @"Local\Koy.Watchdog.Stop." + koyProcessId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Gözcüye verilecek komut satırı argümanları.</summary>
    public static string BuildArguments(int koyProcessId)
        => PidArgument + " " + koyProcessId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Komut satırında `--restore-taskbar` var mı (büyük/küçük harf duyarsız).</summary>
    public static bool IsRestoreOnly(IReadOnlyList<string> args)
    {
        foreach (var a in args)
            if (string.Equals(a, RestoreOnlyArgument, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    /// <summary>`--pid N` argümanını çözer. Eksik, sayı olmayan ya da pozitif olmayan değerde false döner.</summary>
    public static bool TryParseProcessId(IReadOnlyList<string> args, out int processId)
    {
        processId = 0;
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (!string.Equals(args[i], PidArgument, StringComparison.OrdinalIgnoreCase))
                continue;

            return int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out processId)
                   && processId > 0;
        }

        return false;
    }

    /// <summary>
    /// Koy öldüyse (ya da hiç bulunamadıysa) görev çubuğu geri getirilir; temiz çıkışta ve beklenmedik
    /// bekleme hatasında dokunulmaz (hatada yanlışlıkla kullanıcının ayarını bozmamak güvenli taraftır).
    /// </summary>
    public static WatchdogAction Decide(WatchdogWaitResult result) => result switch
    {
        WatchdogWaitResult.ProcessExited => WatchdogAction.RestoreTaskbar,
        WatchdogWaitResult.ProcessNotFound => WatchdogAction.RestoreTaskbar,
        _ => WatchdogAction.None
    };
}
