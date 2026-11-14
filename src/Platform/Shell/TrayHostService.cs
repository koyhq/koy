using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Koy.Core.Diagnostics;
using Koy.Core.Tray;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.DataExchange;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// Koy'un kendi sistem tepsisi. Dock Windows görev çubuğunu gizlediği için tepsi simgelerine başka yoldan ulaşılamıyor.
///
/// Shell_NotifyIcon, simge kaydını "Shell_TrayWnd" sınıflı ilk pencereye WM_COPYDATA ile yollar. Koy bu sınıfla gizli,
/// en üstte bir pencere açar; böylece kayıtlar önce Koy'a gelir. Her mesaj ayrıca Windows görev çubuğuna iletilir:
/// Koy kapanır ya da çökerse pencere süreçle birlikte gider ve Windows tepsisi hiçbir şey kaybetmemiş olur.
/// Görev çubuğuna gelen diğer mesajlar (AppBar, WM_COMMAND, WM_USER+) olduğu gibi Windows'a aktarılır.
/// Yöntem RetroBar ve Cairo Shell'in (ManagedShell) kullandığıyla aynıdır.
///
/// Pencere kendi iş parçacığında çalışır: arayüz meşgulken uygulamaların Shell_NotifyIcon çağrıları beklemesin.
///
/// Süreç nasıl ölürse ölsün sistemde iz kalmaz: pencere ve pencere sınıfları sürece aittir (süreç bitince Windows
/// yok eder), diske, kayıt defterine, AppBar'a ya da görev çubuğu durumuna hiçbir şey yazılmaz, her kayıt Windows'a da
/// iletildiği için Gezgin simgeleri zaten bilir. Geriye yalnız görev çubuğunun gizli kalması kalır; onu
/// açılıştaki TaskbarService.Restore (ve çökme işleyicilerindeki SafeRestoreTaskbar) geri getirir.
/// Temiz kapanışta (Stop) pencere yok edilir ve uygulamalara bir kez daha TaskbarCreated yayınlanır:
/// iletimi kaçan (Gezgin o an yanıt vermiyorsa) kayıtlar da Windows tepsisine yeniden eklenir.
/// </summary>
public sealed class TrayHostService : ITrayService
{
    private const string NotifyClassName = "TrayNotifyWnd";

    private const uint WmDestroy = 0x0002;
    private const uint WmClose = 0x0010;
    private const uint WmActivateApp = 0x001C;
    private const uint WmCopyData = 0x004A;
    private const uint WmCommand = 0x0111;
    private const uint WmTimer = 0x0113;
    private const uint WmUser = 0x0400;
    private const uint WmResync = 0x8000 + 0x4B; // WM_APP: başka bir iş parçacığından yeniden öne geçme isteği

    private const nuint ResyncTimerId = 1;
    private const nuint CatchUpTimerId = 2;
    private const uint ResyncDelayMs = 1500;
    private const uint CatchUpDelayMs = 2000;
    private const uint ForwardTimeoutMs = 2000;
    private static readonly TimeSpan StopWait = TimeSpan.FromMilliseconds(250);

    // Kendi "TaskbarCreated" yayınımız bize de gelir; bu süre içindekiler yok sayılır.
    // Yayın bütün uygulamalara simgelerini yeniden ekletir; Gezgin'le sıra kavgasında art arda yayın yapılmasın.
    private static readonly TimeSpan MinResyncInterval = TimeSpan.FromSeconds(5);

    // Stop beklemeden döner; kapanmakta olan iş parçacığı burada tutulur ki sıradaki Start sınıf adı boşalana dek beklesin.
    private static Thread? s_closingThread;

    private readonly object _gate = new();
    private readonly TrayIconStore _store = new();
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private readonly WNDPROC _trayProc;
    private readonly WNDPROC _notifyProc;
    private readonly Dictionary<uint, string> _appNames = [];
    private readonly HashSet<uint> _pendingNames = [];

    private Thread? _thread;
    // Pencere tanıtıcısını tepsi iş parçacığı yazar, arayüz iş parçacığı okur (IsRunning, EnsureFirst, Stop).
    private nint _hwndValue;
    private uint _taskbarCreatedMessage;
    // Son kendi yayınımız (UTC ticks). Statik: Koy'un öteki pencereleri (TrayIconManager) yankıyı Gezgin yeniden başladı sanmasın.
    private static long s_lastBroadcastTicks = DateTime.MinValue.Ticks;
    // Yayın anında Gezgin'in Shell_TrayWnd tutamacı: Gezgin gerçekten yeniden başladıysa tutamaç değişir.
    private static long s_broadcastExplorerTray;
    private static DateTime LastBroadcastUtc => new(Interlocked.Read(ref s_lastBroadcastTicks), DateTimeKind.Utc);

    /// <summary>Az önce gelen "TaskbarCreated" bizim kendi yayınımızın yankısı mı (Gezgin'in yeniden başlaması değil).</summary>
    public static unsafe bool IsOwnTaskbarCreatedBroadcast() =>
        TaskbarCreatedPolicy.IsOwnBroadcast(
            LastBroadcastUtc, DateTime.UtcNow, (nint)Interlocked.Read(ref s_broadcastExplorerTray), (nint)ShellTrayLocator.FindExplorerTray().Value);
    private bool _disposed;

    public event EventHandler? IconsChanged;

    /// <summary>
    /// Pencere biz istemeden kapandı (iş parçacığı çıktı, dışarıdan WM_CLOSE/WM_DESTROY, mesaj döngüsü hatası).
    /// Tepsi iş parçacığından gelir; dinleyen arayüze kendisi döner. Stop / RequestClose / Dispose ile kapanışta gelmez.
    /// </summary>
    public event EventHandler? Exited;

    // Kapanış bizden mi geldi (Exited yalnız beklenmedik kapanışta tetiklenir).
    private int _closeRequested;

    public TrayHostService()
    {
        // Geri çağrı delegeleri alanda tutulur; aksi halde Windows çağırırken GC toplayabilir.
        _trayProc = TrayWndProc;
        _notifyProc = (hwnd, msg, wParam, lParam) => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private HWND Hwnd
    {
        get => new(Volatile.Read(ref _hwndValue));
        set { unsafe { Volatile.Write(ref _hwndValue, (nint)value.Value); } }
    }

    /// <summary>Tepsi penceresi gerçekten kuruldu ve çalışıyor (başlatma başarısızsa ya da kapandıysa false).</summary>
    public bool IsRunning => !Hwnd.IsNull;

    public void Start()
    {
        if (_thread is not null || _disposed)
            return;

        // Önceki örnek arka planda kapanıyor olabilir (Stop beklemez); "Shell_TrayWnd" sınıfı boşalmadan yenisi kaydolamaz.
        Volatile.Read(ref s_closingThread)?.Join(TimeSpan.FromSeconds(2));

        var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Koy tepsi" };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Pencereyi beklemeden kapatma isteği yollar (çökme işleyicileri için: arayüz iş parçacığı bloklanmaz).
    /// Kapanış tepsi iş parçacığında biter; tam bekleme için <see cref="Stop"/> kullanılır.
    /// </summary>
    public void RequestClose()
    {
        Volatile.Write(ref _closeRequested, 1);
        var hwnd = Hwnd;
        if (!hwnd.IsNull)
            PInvoke.PostMessage(hwnd, WmClose, default, default);
    }

    public void Stop()
    {
        var thread = _thread;
        if (thread is null)
            return;

        Volatile.Write(ref _closeRequested, 1);
        var hwnd = Hwnd;
        if (!hwnd.IsNull)
            PInvoke.PostMessage(hwnd, WmClose, default, default);

        // Arayüz iş parçacığı burada bekler: uzun bekleme kapanışta donma demek. Kısa beklenir; tepsi iş parçacığı
        // gecikirse (Gezgin yanıtsızsa Forward 2 sn bekleyebilir) temizliği (pencere, sınıflar, TaskbarCreated yayını)
        // arka planda bitirir. Windows tepsisi için bir şey kaybolmaz: kayıtlar zaten iletilmişti, yayın sonra gelir.
        Volatile.Write(ref s_closingThread, thread);
        thread.Join(StopWait);
        _thread = null;
    }

    /// <summary>
    /// Gezgin kendi görev çubuğunu öne alırsa yeni simge kayıtları Koy'a gelmez. Ön plan değişiminde çağrılır;
    /// Koy hâlâ ilk sıradaysa yalnız bir FindWindow maliyeti vardır.
    /// </summary>
    public void EnsureFirst()
    {
        var hwnd = Hwnd;
        if (hwnd.IsNull)
            return;

        if (PInvoke.FindWindow(ShellTrayLocator.TrayClassName, null) != hwnd)
            PInvoke.PostMessage(hwnd, WmResync, default, default);
    }

    public IReadOnlyList<TrayIcon> GetVisibleIcons()
    {
        List<TrayIcon> icons;
        lock (_gate)
            icons = [.. _store.Visible];

        // Bazı uygulamalar (Discord, Edge…) ipucu göndermiyor; üzerine gelince ne olduğu anlaşılsın diye uygulama adı.
        for (var i = 0; i < icons.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(icons[i].Tooltip))
                icons[i] = icons[i] with { Tooltip = GetAppName(icons[i].OwnerHwnd) };
        }

        return icons;
    }

    public byte[]? GetIconImage(TrayIconKey key)
    {
        // Simge kopyası tepsi iş parçacığında değiştirilip yok edilebilir: kilit altında yalnız kopyası alınır,
        // GDI okuması ve PNG kodlaması kilit dışında yapılır (uygulamaların Shell_NotifyIcon çağrıları beklemesin).
        nint copy;
        lock (_gate)
        {
            var icon = Find(key);
            if (icon is null || icon.IconHandle == 0)
                return null;

            unsafe { copy = (nint)PInvoke.CopyIcon(new HICON(icon.IconHandle)).Value; }
        }

        if (copy == 0)
            return null;

        try
        {
            return IconHelper.HIconToPng(copy);
        }
        finally
        {
            PInvoke.DestroyIcon(new HICON(copy));
        }
    }

    public unsafe void SendMouse(TrayIconKey key, TrayMouseAction action, int cursorX, int cursorY)
    {
        TrayIcon? icon;
        lock (_gate)
            icon = Find(key);

        if (icon is null)
            return;

        var owner = new HWND(icon.OwnerHwnd);
        uint processId = 0;
        _ = PInvoke.GetWindowThreadProcessId(owner, &processId);
        // Son girdiyi Koy aldığı için öne geçme hakkını uygulamaya devredebilir; menüsü ve penceresi öne gelsin.
        if (processId != 0)
            PInvoke.AllowSetForegroundWindow(processId);

        foreach (var message in TrayCallback.Build(icon, action, cursorX, cursorY))
            PInvoke.SendNotifyMessage(owner, message.Message, new WPARAM((nuint)message.WParam), new LPARAM(message.LParam));
    }

    public void RemoveDeadIcons()
    {
        bool changed;
        lock (_gate)
        {
            var released = _store.RemoveDead(h => PInvoke.IsWindow(new HWND(h)));
            DestroyIcons(released);
            changed = released.Count > 0;
        }

        if (changed)
        {
            // Süreç numaraları yeniden kullanılabilir; ölen uygulamanın adı yenisine yapışmasın.
            lock (_appNames)
                _appNames.Clear();
        }

        if (changed)
            IconsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }

    private void Run(ManualResetEventSlim ready)
    {
        HINSTANCE instance = default;
        try
        {
            // Ham sürüm: kolay sürümün SafeHandle'ı serbest bırakılırken ana modüle FreeLibrary çağırmasın.
            unsafe { instance = new HINSTANCE(PInvoke.GetModuleHandle(default(PCWSTR)).Value); }
            if (!CreateWindows(instance))
                return;
        }
        catch (Exception ex)
        {
            CrashLog.Write("TrayHostService.Run (pencere kurulumu)", ex);
            return;
        }
        finally
        {
            ready.Set();
        }

        BroadcastTaskbarCreated();

        // Koy açılırken görev çubuğunu geri getirip hemen gizler; Gezgin buna tepki olarak kendi penceresini
        // birkaç ms sonra yeniden öne alır ve ilk yayına cevap veren uygulamalar kaydı Windows'a yollar.
        // Gezgin durulunca bir kez daha öne geçip yayın yapılır; kaçan kayıtlar böylece gelir.
        PInvoke.SetTimer(Hwnd, CatchUpTimerId, CatchUpDelayMs, null);

        try
        {
            int got;
            while ((got = (int)PInvoke.GetMessage(out var msg, HWND.Null, 0, 0)) > 0)
            {
                PInvoke.TranslateMessage(msg);
                PInvoke.DispatchMessage(msg);
            }

            if (got < 0)
                CrashLog.Write("TrayHostService.Run (GetMessage hata döndü)", new InvalidOperationException($"GetMessage={got}"));
        }
        catch (Exception ex)
        {
            CrashLog.Write("TrayHostService.Run (mesaj döngüsü)", ex);
        }

        var unexpected = Volatile.Read(ref _closeRequested) == 0;
        Hwnd = HWND.Null;
        lock (_gate)
            DestroyIcons(_store.Clear());

        UnregisterClass(ShellTrayLocator.TrayClassName, instance);
        UnregisterClass(NotifyClassName, instance);
        IconsChanged?.Invoke(this, EventArgs.Empty);

        // Koy artık Shell_TrayWnd sahibi değil: uygulamalar simgelerini Windows'un tepsisine yeniden eklesin.
        BroadcastTaskbarCreated();

        if (unexpected)
        {
            // Düğmenin "bilinmeyen nedenle" kaybolmasının izi: sebep sonradan okunabilsin.
            CrashLog.Write("TrayHostService.Run (pencere beklenmedik kapandı)", new InvalidOperationException("Tepsi penceresi dışarıdan kapatıldı ya da mesaj döngüsü bitti."));
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }

    private unsafe bool CreateWindows(HINSTANCE instance)
    {
        _taskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");

        fixed (char* trayClass = ShellTrayLocator.TrayClassName)
        fixed (char* notifyClass = NotifyClassName)
        {
            var trayWc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = _trayProc,
                hInstance = instance,
                lpszClassName = trayClass
            };
            if (PInvoke.RegisterClassEx(trayWc) == 0)
                return false;

            var notifyWc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = _notifyProc,
                hInstance = instance,
                lpszClassName = notifyClass
            };
            PInvoke.RegisterClassEx(notifyWc);

            // Pencere hiç gösterilmez ama Gezgin'in görev çubuğuyla aynı dikdörtgende durur: bazı uygulamalar
            // bildirimlerini "Shell_TrayWnd"nin konumuna göre yerleştiriyor, (0,0)'da kalırsa sol üstte açılırlar.
            var bounds = GetExplorerTrayBounds();
            var hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TOPMOST,
                trayClass,
                null,
                WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_CLIPCHILDREN | WINDOW_STYLE.WS_CLIPSIBLINGS,
                bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top,
                HWND.Null,
                HMENU.Null,
                instance,
                null);

            if (hwnd.IsNull)
            {
                UnregisterClass(ShellTrayLocator.TrayClassName, instance);
                UnregisterClass(NotifyClassName, instance);
                return false;
            }

            // Bazı uygulamalar simge konumunu bulmak için görev çubuğunun altında bu alt pencereyi arar.
            PInvoke.CreateWindowEx(0, notifyClass, null, WINDOW_STYLE.WS_CHILD, 0, 0, 0, 0, hwnd, HMENU.Null, instance, null);

            Hwnd = hwnd;
        }

        MakeTopmost();
        return true;
    }

    private LRESULT TrayWndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            switch (msg)
            {
                case WmCopyData:
                    return OnCopyData(msg, wParam, lParam);

                case WmTimer when wParam.Value is ResyncTimerId or CatchUpTimerId:
                    PInvoke.KillTimer(hwnd, wParam.Value);
                    Resync();
                    return default;

                case WmResync:
                    Resync();
                    return default;

                case WmClose:
                    PInvoke.DestroyWindow(hwnd);
                    return default;

                case WmDestroy:
                    PInvoke.PostQuitMessage(0);
                    return default;
            }

            if (msg == _taskbarCreatedMessage && msg != 0)
            {
                // Bizim yayınımız değilse Gezgin yeniden başlamıştır: kendi tepsisi yeniden öne geçti.
                // Gezgin hazır olsun diye kısa bir süre sonra öne geçip uygulamalardan simgeleri yeniden isteriz.
                if (!IsOwnTaskbarCreatedBroadcast())
                    PInvoke.SetTimer(hwnd, ResyncTimerId, ResyncDelayMs, null);
                return default;
            }

            if (msg is WmCommand or WmActivateApp || msg >= WmUser)
                return Forward(msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            // Yerel geri çağrıdan istisna sızarsa süreç çöker; tepsi mesajı kaybolsun, Koy ayakta kalsın. İz bırakılır.
            CrashLog.Write("TrayHostService.TrayWndProc", ex);
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private unsafe LRESULT OnCopyData(uint msg, WPARAM wParam, LPARAM lParam)
    {
        var copyData = (COPYDATASTRUCT*)lParam.Value;
        // Pencereye herhangi bir süreç WM_COPYDATA gönderebilir: boyut Core'daki sınırlarla denetlenir, bozuk
        // ya da devasa blok span kurulmadan (cbData işaretsiz, int'e çevrilince negatif olabilir) reddedilir.
        if (copyData is not null
            && copyData->dwData == TrayMessageParser.CopyDataTrayMessage
            && copyData->lpData is not null
            && TrayMessageParser.IsAcceptableLength(copyData->cbData)
            && TrayMessageParser.TryParse(new ReadOnlySpan<byte>(copyData->lpData, (int)copyData->cbData), out var data))
        {
            var handled = HandleTrayMessage(data);
            var forwarded = Forward(msg, wParam, lParam);
            // Windows'un tepsisi simgeyi zaten biliyorsa (yeniden ekleme) hata döner; uygulamaya başarı bildirilir.
            return new LRESULT(handled || forwarded.Value != 0 ? 1 : 0);
        }

        return Forward(msg, wParam, lParam);
    }

    private unsafe bool HandleTrayMessage(TrayNotifyData data)
    {
        if (data.OwnerHwnd != 0)
        {
            uint processId = 0;
            _ = PInvoke.GetWindowThreadProcessId(new HWND(data.OwnerHwnd), &processId);
            // Koy'un kendi simgesi (dock kapalıyken menüye erişim için) dock'un tepsisinde gösterilmez.
            if (TrayHostPolicy.IsOwnIcon(processId, _ownProcessId))
                return true;
        }

        if ((data.Fields & TrayIconFields.Icon) != 0 && data.IconHandle != 0)
        {
            // Uygulama simgesini sonradan yok edebilir; kendi kopyamızı tutarız.
            data = data with { IconHandle = (nint)PInvoke.CopyIcon(new HICON(data.IconHandle)).Value };
        }

        bool success;
        lock (_gate)
        {
            var result = _store.Apply(data);
            DestroyIcons(result.ReleasedIcons);
            success = result.Success;
        }

        if (success)
            IconsChanged?.Invoke(this, EventArgs.Empty);

        return success;
    }

    private static unsafe LRESULT Forward(uint msg, WPARAM wParam, LPARAM lParam)
    {
        var explorer = ShellTrayLocator.FindExplorerTray();
        if (explorer.IsNull)
            return default;

        nuint result = 0;
        PInvoke.SendMessageTimeout(explorer, msg, wParam, lParam, SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG, ForwardTimeoutMs, &result);
        return new LRESULT((nint)result);
    }

    /// <summary>
    /// Öne geçer ve uygulamalardan simgelerini yeniden ister. Öne geçme kaybedildiyse arada gelen kayıtlar
    /// Windows'a gitmiştir; yayın atlanmaz, yalnız son yayından kısa süre geçtiyse ertelenir.
    /// </summary>
    private void Resync()
    {
        MakeTopmost();

        var sinceLast = DateTime.UtcNow - LastBroadcastUtc;
        if (sinceLast >= MinResyncInterval)
        {
            BroadcastTaskbarCreated();
            return;
        }

        var wait = MinResyncInterval - sinceLast;
        PInvoke.SetTimer(Hwnd, ResyncTimerId, (uint)Math.Ceiling(wait.TotalMilliseconds) + 50, null);
    }

    private void MakeTopmost()
    {
        if (Hwnd.IsNull)
            return;

        // Görev çubuğu yer değiştirmiş olabilir (ekran değişimi); konum da eşitlenir. Pencere gösterilmez.
        var bounds = GetExplorerTrayBounds();
        var flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        if (bounds.right <= bounds.left || bounds.bottom <= bounds.top)
            flags |= SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

        PInvoke.SetWindowPos(
            Hwnd,
            new HWND(-1), // HWND_TOPMOST
            bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top,
            flags);
    }

    private static RECT GetExplorerTrayBounds()
    {
        var explorer = ShellTrayLocator.FindExplorerTray();
        return !explorer.IsNull && PInvoke.GetWindowRect(explorer, out var rect) ? rect : default;
    }

    /// <summary>Uygulamalardan tepsi simgelerini yeniden eklemelerini ister (Gezgin yeniden başlamış gibi).</summary>
    private void BroadcastTaskbarCreated()
    {
        if (_taskbarCreatedMessage == 0)
            return;

        unsafe { Interlocked.Exchange(ref s_broadcastExplorerTray, (nint)ShellTrayLocator.FindExplorerTray().Value); }
        Interlocked.Exchange(ref s_lastBroadcastTicks, DateTime.UtcNow.Ticks);
        PInvoke.SendNotifyMessage(new HWND(0xFFFF), _taskbarCreatedMessage, default, default); // HWND_BROADCAST
    }

    private unsafe string GetAppName(nint ownerHwnd)
    {
        uint processId = 0;
        _ = PInvoke.GetWindowThreadProcessId(new HWND(ownerHwnd), &processId);
        if (processId == 0)
            return string.Empty;

        lock (_appNames)
        {
            if (_appNames.TryGetValue(processId, out var cached))
                return cached;

            // Dosya sürüm bilgisi diskten okunur: arayüz iş parçacığında değil, havuzda çözülür. Çözülünce
            // IconsChanged ile pencere yenilenir; o ana dek ipucu boş kalır.
            if (!_pendingNames.Add(processId))
                return string.Empty;
        }

        var pid = processId;
        ThreadPool.QueueUserWorkItem(_ => ResolveAppName(pid));
        return string.Empty;
    }

    private void ResolveAppName(uint processId)
    {
        var name = string.Empty;
        try
        {
            var path = WindowTracker.GetProcessPath(processId);
            if (!string.IsNullOrEmpty(path))
            {
                var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
                name = !string.IsNullOrWhiteSpace(description) ? description.Trim() : Path.GetFileNameWithoutExtension(path);
            }
        }
        catch
        {
            // Erişilemeyen süreçte ipucu boş kalır.
        }

        lock (_appNames)
        {
            _pendingNames.Remove(processId);
            if (_disposed)
                return;

            _appNames[processId] = name;
        }

        if (name.Length > 0)
            IconsChanged?.Invoke(this, EventArgs.Empty);
    }

    private TrayIcon? Find(TrayIconKey key)
    {
        foreach (var icon in _store.All)
        {
            if (icon.Key == key)
                return icon;
        }

        return null;
    }

    private static unsafe void UnregisterClass(string className, HINSTANCE instance)
    {
        fixed (char* name = className)
            PInvoke.UnregisterClass(name, instance);
    }

    private static void DestroyIcons(IReadOnlyList<nint> handles)
    {
        foreach (var handle in handles)
            PInvoke.DestroyIcon(new HICON(handle));
    }
}
