using System.IO;
using Koy.Core.Diagnostics;
using Koy.Core.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// Windows görev çubuğunu gizler ve geri getirir.
/// Güvenlik kuralı (AGENTS.md #7): Çökse bile görev çubuğu geri gelmeli.
/// Görev çubuğu gizlenmeden ÖNCE durum %AppData%\Koy\taskbar-state.json dosyasına atomik yazılır (geçici dosya + taşıma),
/// geri getirildiğinde dosya silinir. Açılışta bu dosya kontrol edilir. Gizlemenin her yolu (Hide, EnsureHidden) gözcünün
/// çalıştığını ve RunOnce yedeğinin yazıldığını garanti eder (OnHidden).
/// Bu dosya Koy.Watchdog projesine de bağlanarak derlenir (gözcü, Koy ölünce aynı Restore'u çalıştırır); bu yüzden
/// yalnız Core ve Win32'ye bağımlı kalır. Koy'a özel kısım (gözcüyü başlatma/durdurma) TaskbarService.Watchdog.cs
/// içindeki kısmi yöntemlerdedir.
/// </summary>
public static partial class TaskbarService
{
    private const uint ABM_GETSTATE = 0x00000004;
    private const uint ABM_SETSTATE = 0x0000000A;
    private const int ABS_AUTOHIDE = TaskbarStatePolicy.AutoHide;

    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy",
        "taskbar-state.json");

    private static readonly object Gate = new();

    // Koy'un SW_HIDE ile gizlediği çubuklar: dock'u olmayan monitörler ya da kapanan dock'lar için yalnız bunlar geri gösterilir.
    private static readonly HashSet<nint> HiddenBars = [];
    private static bool _hiddenByUs;
    private static readonly TaskbarShutdownGuard ShutdownGuard = new();
    private static long _lastStateErrorLogMs = long.MinValue / 2;

    /// <summary>Windows Gezgini görev çubuğu penceresi hazır mı (oturum açılışında henüz oluşmamış olabilir).</summary>
    public static bool IsExplorerTaskbarReady => ShellTrayLocator.FindExplorerTray() != HWND.Null;

    /// <summary>Önceki oturumdan kalan geri yüklenecek görev çubuğu durumu var mı (gizli kayıt ya da bozuk dosya).</summary>
    public static bool HasPendingRestore => TaskbarStatePolicy.HasPendingRestore(ReadStateFile());

    /// <summary>
    /// Oturum kapanıyor: görev çubuğu geri getirildikten sonra dock'un yeniden gizlemesi kısa süre bastırılır
    /// (kapatma iptal edilirse süre dolunca dock yeniden gizler).
    /// </summary>
    public static void BeginShutdown()
    {
        lock (Gate)
            ShutdownGuard.Begin(Environment.TickCount64);
    }

    /// <summary>
    /// Dock'lu monitörlerdeki görev çubuklarını gizler (dock'suz monitörlerinki görünür kalır). Birden çok kez
    /// çağrılabilir (idempotent): zaten gizliyken kayıtlı "önceki durum" korunur, böylece bizim koyduğumuz AUTOHIDE
    /// kullanıcının gerçek durumu sanılıp üzerine yazılmaz. Sıra: durum dosyası → gözcü + RunOnce → ABM_SETSTATE → SW_HIDE;
    /// durum yazılamazsa hiçbir şey gizlenmez. Explorer yeniden başlayınca da aynı çağrı gizlemeyi yeniden uygular.
    /// </summary>
    /// <param name="dockMonitors">Dock penceresi açık monitörlerin HMONITOR tutamaçları.</param>
    /// <returns>Görev çubuğu(ları) gizlendi mi.</returns>
    public static unsafe bool Hide(IReadOnlyCollection<nint> dockMonitors)
    {
        lock (Gate)
        {
            using var stateLock = EnterStateLock();
            if (ShutdownGuard.IsSuppressed(Environment.TickCount64))
                return false;

            var primary = ShellTrayLocator.FindExplorerTray();
            if (primary == HWND.Null)
            {
                // Gezgin henüz görev çubuğunu kurmadıysa (oturum açılışı): önceki durumu tahmin edip dosyaya yazma;
                // Shell_TrayWnd oluşunca ReapplyTaskbarHiding gerçek durumu okuyup gizler.
                return false;
            }

            var read = ReadStateFile();
            var data = new APPBARDATA
            {
                cbSize = (uint)sizeof(APPBARDATA),
                hWnd = primary
            };
            var current = (int)SendAppBarMessage(ABM_GETSTATE, ref data);
            var prevState = TaskbarStatePolicy.ChoosePreviousState(read, current);

            // 1. Durum önce yazılır: SETSTATE'ten sonra ölürsek Restore'un neyi geri getireceği bellidir.
            var desired = new TaskbarSavedState(true, prevState);
            if (read.State != desired && !SaveState(desired))
                return false;

            // 2. Gözcü ve RunOnce yedeği, çubuk gizlenmeden önce hazır.
            OnHidden();

            // 3. Zaten otomatik gizlemedeyse yeniden SETSTATE gönderilmez: Gezgin her SETSTATE'te çalışma alanını yeniden
            // hesaplar ve dock'un AppBar payını bir an düşürür (büyütülmüş pencereler sıçrar).
            if ((current & ABS_AUTOHIDE) == 0)
            {
                data.lParam = new LPARAM((nint)ABS_AUTOHIDE);
                SendAppBarMessage(ABM_SETSTATE, ref data);
            }

            // 4. Yalnız dock'lu monitörlerdeki çubuklar SW_HIDE; daha önce gizlediğimiz ama artık hedef olmayan geri gösterilir.
            ApplyBarVisibility(dockMonitors);
            _hiddenByUs = true;
            return true;
        }
    }

    /// <summary>
    /// Görev çubuğunu (birincil ve bütün ikincil monitörlerdekileri) geri getirir ve durumu önceki haline döndürür.
    /// Explorer çalışmıyorsa (birincil çubuk yok) durum dosyası silinmez; sonraki açılış yeniden dener.
    /// Gözcü ve RunOnce yedeği en SON, iş bitince kaldırılır: Restore yarıda ölürse hâlâ korunuyoruz.
    /// </summary>
    /// <param name="alsoSkipProcessId">Gözcü için: ölen Koy sürecinin tepsi penceresini atla.</param>
    /// <param name="force">Kurtarma amaçlı: kayıtlı durum olmasa bile ABM_SETSTATE gönder.</param>
    /// <param name="keepPending">
    /// Oturum kapanışı: çubuk geri getirilir ama durum dosyası, gözcü ve RunOnce yedeği kalır. Kapanmakta olan Gezgin
    /// ABM_SETSTATE'i kalıcı yazmayabilir; dosya kalırsa sonraki oturum (RunOnce ya da Koy'un açılışı) yeniden geri yükler.
    /// Kapatma iptal edilirse dock yeniden gizlerken kayıtlı önceki durumu kullanır.
    /// </param>
    /// <returns>Görev çubuğunun durumu doğrulanarak geri geldi mi (false: dosya kaldı, sonraki fırsatta yeniden denenir).</returns>
    public static unsafe bool Restore(uint alsoSkipProcessId = 0, bool force = false, bool keepPending = false)
    {
        lock (Gate)
        {
            using var stateLock = EnterStateLock();

            // Oturum kapanışı sürerken (SessionEnding'den sonra OnExit, çökme işleyicileri) de dosya korunur.
            keepPending |= ShutdownGuard.IsSuppressed(Environment.TickCount64);

            var read = ReadStateFile();

            SetAllBarsVisible();
            _hiddenByUs = false;

            var primary = ShellTrayLocator.FindExplorerTray(alsoSkipProcessId);
            if (primary == HWND.Null)
            {
                // Explorer yok: bekleyen kayıt varsa dosya ve yedekler kalır; yoksa temizlik.
                if (!force && !TaskbarStatePolicy.HasPendingRestore(read))
                {
                    Finish();
                    return true;
                }

                return false;
            }

            var data = new APPBARDATA
            {
                cbSize = (uint)sizeof(APPBARDATA),
                hWnd = primary
            };
            var current = (int)SendAppBarMessage(ABM_GETSTATE, ref data);
            var decision = TaskbarStatePolicy.DecideRestore(read, current, force);
            var applied = !decision.SendState || SetStateVerified(ref data, decision.State);

            if (keepPending)
                return applied;

            // Gezgin ABM_SETSTATE'i uygulamadıysa (oturum açılışında henüz hazır değil, yanıtsız) dosya silinmez: silinirse
            // kalan AUTOHIDE bir sonraki gizlemede "kullanıcının tercihi" sanılır ve tik kalıcı açık kalırdı.
            if (!applied)
            {
                CrashLog.Write($"Görev çubuğu otomatik gizleme durumu geri yüklenemedi (istenen={decision.State}); durum dosyası korunuyor", null);
                return false;
            }

            Finish();
            return true;
        }
    }

    /// <summary>
    /// ABM_SETSTATE gönderir ve ABM_GETSTATE ile otomatik gizleme bitinin gerçekten değiştiğini doğrular; tutmazsa kısa
    /// aralıklarla birkaç kez yeniden dener. Windows 11 her zaman üstte bayrağını yok saydığından yalnız AUTOHIDE karşılaştırılır.
    /// </summary>
    private static bool SetStateVerified(ref APPBARDATA data, int desired)
    {
        for (var attempt = 0; attempt < TaskbarStatePolicy.SetStateAttempts; attempt++)
        {
            if (attempt > 0)
                Thread.Sleep(TaskbarStatePolicy.SetStateRetryDelayMs);

            data.lParam = new LPARAM((nint)desired);
            SendAppBarMessage(ABM_SETSTATE, ref data);
            var actual = (int)SendAppBarMessage(ABM_GETSTATE, ref data);
            if (TaskbarStatePolicy.IsAutoHideApplied(desired, actual))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gezgin, otomatik gizleme durumu değişince ya da kendi kendine görev çubuğunu yeniden gösterir;
    /// o zaman alt kenarda bizim dock'la birlikte ikinci bir çubuk çıkar. Dock'lu monitörlerdeki çubuklardan biri
    /// görünür olduysa Hide'ın tam yoluyla yeniden gizlenir (durum dosyası, gözcü ve RunOnce de yeniden doğrulanır).
    /// Gizliyken gözcü ölmüşse yeniden başlatılır.
    /// </summary>
    public static bool EnsureHidden(IReadOnlyCollection<nint> dockMonitors)
    {
        bool anyVisible;
        lock (Gate)
        {
            if (ShutdownGuard.IsSuppressed(Environment.TickCount64))
                return false;

            var targets = TaskbarBarSelector.SelectBarsToHide(EnumerateBars(), dockMonitors);
            anyVisible = targets.Any(h => PInvoke.IsWindowVisible(new HWND(h)));
            if (!anyVisible && _hiddenByUs)
                OnHidden();
        }

        return anyVisible && Hide(dockMonitors);
    }

    /// <summary>
    /// Koy ile gözcü (ve RunOnce'tan açılan geri yükleme kipi) aynı durum dosyasına ve aynı Gezgin durumuna dokunur. Süreçler
    /// arası kilit olmadan yarış şuydu: Koy dosyayı "gizli, önceki=kapalı" okuyup yeniden yazmayı atlar, gözcü aynı anda geri
    /// yükleyip dosyayı siler, Koy ardından AUTOHIDE koyar; dosyasız kalan AUTOHIDE sonraki gizlemede kullanıcının tercihi
    /// sanılır ve Windows'taki "otomatik gizle" tiki kalıcı açık kalırdı. Kilit alınamazsa (zaman aşımı) yine de devam edilir:
    /// görev çubuğunu geri getirmek hiçbir koşulda engellenmez.
    /// </summary>
    private static readonly Mutex StateMutex = new(false, TaskbarStatePolicy.StateMutexName);

    private static StateLock EnterStateLock()
    {
        try
        {
            return new StateLock(StateMutex.WaitOne(TaskbarStatePolicy.StateLockTimeoutMs));
        }
        catch (AbandonedMutexException)
        {
            // Öteki süreç kilit elindeyken öldü: kilit artık bizde.
            return new StateLock(true);
        }
    }

    private readonly struct StateLock(bool acquired) : IDisposable
    {
        public void Dispose()
        {
            if (acquired)
                StateMutex.ReleaseMutex();
        }
    }

    // Koy'da gözcüyü başlatır/RunOnce yazar ve durdurur/siler; gözcü projesinde gövde yoktur (derleyici çağrıyı siler).
    private static unsafe nuint SendAppBarMessage(uint message, ref APPBARDATA data)
    {
        nuint result = 0;
        var handled = false;
        SendAppBarMessageOverride(message, ref data, ref result, ref handled);
        return handled ? result : PInvoke.SHAppBarMessage(message, ref data);
    }

    static partial void SendAppBarMessageOverride(uint message, ref APPBARDATA data, ref nuint result, ref bool handled);

    static partial void OnHidden();

    static partial void OnRestoring();

    // Geri yükleme bitti: durum dosyası silinir, gözcü durdurulur, RunOnce kaydı kaldırılır.
    private static void Finish()
    {
        DeleteState();
        TaskbarRunOnce.Unregister();
        OnRestoring();
    }

    private static void SetAllBarsVisible()
    {
        foreach (var bar in EnumerateBars())
            PInvoke.ShowWindow(new HWND(bar.Handle), SHOW_WINDOW_CMD.SW_SHOW);

        HiddenBars.Clear();
    }

    private static void ApplyBarVisibility(IReadOnlyCollection<nint> dockMonitors)
    {
        var bars = EnumerateBars();
        var targets = TaskbarBarSelector.SelectBarsToHide(bars, dockMonitors);

        foreach (var bar in bars)
        {
            var hwnd = new HWND(bar.Handle);
            if (targets.Contains(bar.Handle))
            {
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
                HiddenBars.Add(bar.Handle);
            }
            else if (HiddenBars.Remove(bar.Handle))
            {
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOW);
            }
        }
    }

    /// <summary>Birincil ve ikincil görev çubuğu pencereleri, bulundukları monitörle birlikte.</summary>
    private static unsafe List<TaskbarBar> EnumerateBars()
    {
        var bars = new List<TaskbarBar>();

        var primary = ShellTrayLocator.FindExplorerTray();
        if (primary != HWND.Null)
            bars.Add(new TaskbarBar((nint)primary.Value, MonitorOf(primary)));

        var secondary = HWND.Null;
        while ((secondary = PInvoke.FindWindowEx(HWND.Null, secondary, "Shell_SecondaryTrayWnd", null)) != HWND.Null)
            bars.Add(new TaskbarBar((nint)secondary.Value, MonitorOf(secondary)));

        return bars;
    }

    private static unsafe nint MonitorOf(HWND hwnd)
        => (nint)PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST).Value;

    /// <summary>Durumu atomik yazar (geçici dosya + taşıma). Başarısızlık CrashLog'a gider, çağıran gizlemeyi bırakır.</summary>
    private static bool SaveState(TaskbarSavedState state)
    {
        var temp = StateFilePath + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(StateFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(temp, TaskbarStatePolicy.Serialize(state, DateTime.UtcNow));
            File.Move(temp, StateFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            // Window olaylarında her denemede log basılmasın: dakikada bir.
            var now = Environment.TickCount64;
            if (now - _lastStateErrorLogMs >= 60_000)
            {
                _lastStateErrorLogMs = now;
                CrashLog.Write("Görev çubuğu durum dosyası yazılamadı; görev çubuğu gizlenmedi", ex);
            }

            try
            {
                File.Delete(temp);
            }
            catch
            {
            }

            return false;
        }
    }

    private static TaskbarStateRead ReadStateFile()
    {
        try
        {
            return File.Exists(StateFilePath)
                ? TaskbarStatePolicy.Read(File.ReadAllText(StateFilePath))
                : TaskbarStateRead.Missing;
        }
        catch (Exception ex)
        {
            // Okunamayan dosya (kilit, erişim) bozuk sayılır: güvenli tarafta geri yüklenir.
            CrashLog.Write("Görev çubuğu durum dosyası okunamadı", ex);
            return new TaskbarStateRead(TaskbarStateFileStatus.Corrupt, null);
        }
    }

    private static void DeleteState()
    {
        try
        {
            File.Delete(StateFilePath);
            File.Delete(StateFilePath + ".tmp");
        }
        catch (Exception ex)
        {
            CrashLog.Write("Görev çubuğu durum dosyası silinemedi", ex);
        }
    }
}
