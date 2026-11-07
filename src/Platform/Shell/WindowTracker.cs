using System.Diagnostics;
using System.Runtime.InteropServices;
using Koy.Core.Dock;
using Koy.Core.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Shell.PropertiesSystem;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

/// <summary>
/// SetWinEventHook kullanarak ön plandaki pencereyi, konum değişikliklerini ve açık pencereleri izler.
/// Hook delegesini GC'den korumak için alan olarak tutar.
/// </summary>
public sealed class WindowTracker : IWindowTracker
{
    private readonly WINEVENTPROC _hookProc;
    private HWINEVENTHOOK _foregroundHook;
    private HWINEVENTHOOK _locationHook;
    private HWINEVENTHOOK _lifecycleHook;
    private HWINEVENTHOOK _uncloakHook;
    private HWINEVENTHOOK _nameHook;
    private RefreshDebouncer? _nameChangeDebouncer;
    private bool _started;
    private bool _disposed;

    public event EventHandler<WindowInfo>? ForegroundWindowChanged;
    public event EventHandler? WindowsChanged;
    public event EventHandler<nint>? WindowFlashing;

    private readonly List<HWND> _shellHookWindows = [];
    public uint ShellHookMessage { get; private set; }

    public WindowInfo? CurrentForeground { get; private set; }

    /// <summary>İzleyici çalışıyor mu (paylaşımlı kullanımda ikinci özellik mevcut durumu elle okumalı).</summary>
    public bool IsStarted => _started;

    public WindowTracker()
    {
        // Delegasyon alan olarak tutulur; aksi halde Win32 geri çağrısı sırasında GC toplayabilir.
        _hookProc = OnWinEvent;
    }

    public void Start()
    {
        if (_started)
            return;

        _started = true;

        // Ön plan pencere değişimi (EVENT_SYSTEM_FOREGROUND = 0x0003)
        _foregroundHook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            default(HMODULE),
            _hookProc,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT);

        // Pencere konum/boyut değişimi (EVENT_OBJECT_LOCATIONCHANGE = 0x800B)
        _locationHook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
            default(HMODULE),
            _hookProc,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT);

        // Pencere oluşturulma, yok edilme, gösterilme, gizlenme olayları (0x8000..0x8003)
        _lifecycleHook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_CREATE,
            PInvoke.EVENT_OBJECT_HIDE,
            default(HMODULE),
            _hookProc,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT);

        // Pencere gizlendi / gizlilikten çıktı (sanal masaüstü geçişi, Mağaza uygulaması): SHOW/HIDE olayı gelmez, bunlar gelir.
        _uncloakHook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_CLOAKED,
            PInvoke.EVENT_OBJECT_UNCLOAKED,
            default(HMODULE),
            _hookProc,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT);

        // Başlık değişimi: sık gelebilir (tarayıcı sekmesi, terminal). Yalnız görev çubuğu penceresi olanlar sayılır ve
        // olaylar tek bir bildirimde birleşir (tek atımlık erteleme; boşta zamanlayıcı yok, yoklama yok).
        // Bildirim, Start'ın çağrıldığı iş parçacığına (arayüz) taşınır: WindowsChanged'e abone kod arayüze dokunur.
        var context = SynchronizationContext.Current;
        _nameChangeDebouncer = new RefreshDebouncer(
            () => WindowsChanged?.Invoke(this, EventArgs.Empty),
            NameChangeDelay,
            post: action => { if (context is not null) context.Post(_ => action(), null); else action(); });
        _nameHook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_NAMECHANGE,
            PInvoke.EVENT_OBJECT_NAMECHANGE,
            default(HMODULE),
            _hookProc,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT);

        UpdateForegroundWindow();
    }

    private static readonly TimeSpan NameChangeDelay = TimeSpan.FromMilliseconds(500);

    public void RegisterShellHook(nint windowHandle)
    {
        if (windowHandle == 0) return;
        var hwnd = new HWND(windowHandle);
        if (!_shellHookWindows.Contains(hwnd))
        {
            if (PInvoke.RegisterShellHookWindow(hwnd))
            {
                _shellHookWindows.Add(hwnd);
                ShellHookMessage = PInvoke.RegisterWindowMessage("SHELLHOOK");
            }
        }
    }

    public void DeregisterShellHook(nint windowHandle)
    {
        if (windowHandle == 0) return;
        var hwnd = new HWND(windowHandle);
        if (_shellHookWindows.Remove(hwnd))
        {
            PInvoke.DeregisterShellHookWindow(hwnd);
        }
    }

    public void HandleShellHookMessage(nint wParam, nint lParam)
    {
        var shellEvent = wParam.ToInt32();
        const int HSHELL_WINDOWCREATED = 1;
        const int HSHELL_WINDOWDESTROYED = 2;
        const int HSHELL_ACTIVATESHELLWINDOW = 3;
        const int HSHELL_WINDOWACTIVATED = 4;
        const int HSHELL_REDRAW = 6;
        const int HSHELL_FLASH = 0x8006;
        const int HSHELL_RUDEAPPACTIVATED = 0x8004;

        if (shellEvent == HSHELL_FLASH)
        {
            WindowFlashing?.Invoke(this, lParam);
            WindowsChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (shellEvent is HSHELL_WINDOWCREATED or HSHELL_WINDOWDESTROYED or HSHELL_REDRAW)
        {
            WindowsChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (shellEvent is HSHELL_WINDOWACTIVATED or HSHELL_RUDEAPPACTIVATED or HSHELL_ACTIVATESHELLWINDOW)
        {
            UpdateForegroundWindow();
        }
    }

    public void Stop()
    {
        if (!_started)
            return;

        _started = false;

        foreach (var hwnd in _shellHookWindows)
        {
            PInvoke.DeregisterShellHookWindow(hwnd);
        }
        _shellHookWindows.Clear();

        if (_foregroundHook.Value != 0)
        {
            PInvoke.UnhookWinEvent(_foregroundHook);
            _foregroundHook = default;
        }

        if (_locationHook.Value != 0)
        {
            PInvoke.UnhookWinEvent(_locationHook);
            _locationHook = default;
        }

        if (_lifecycleHook.Value != 0)
        {
            PInvoke.UnhookWinEvent(_lifecycleHook);
            _lifecycleHook = default;
        }

        if (_uncloakHook.Value != 0)
        {
            PInvoke.UnhookWinEvent(_uncloakHook);
            _uncloakHook = default;
        }

        if (_nameHook.Value != 0)
        {
            PInvoke.UnhookWinEvent(_nameHook);
            _nameHook = default;
        }

        _nameChangeDebouncer?.Dispose();
        _nameChangeDebouncer = null;
    }

    public bool GetCursorPosition(out int x, out int y)
    {
        if (PInvoke.GetCursorPos(out var pt))
        {
            x = pt.X;
            y = pt.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    public unsafe IReadOnlyList<OpenWindowInfo> GetOpenWindows()
    {
        var windows = new List<OpenWindowInfo>();

        var seenHandles = new HashSet<nint>();
        PInvoke.EnumWindows((HWND hwnd, LPARAM _) =>
        {
            if (hwnd.IsNull)
                return true;

            seenHandles.Add(hwnd.Value);

            // Görev çubuğu kuralı Core'da (TaskbarWindowRule): APPWINDOW her zaman girer; aksi halde sahipsiz ve
            // araç/etkinleşmeyen pencere olmamalı. Gizlenmiş (cloaked) pencereler ekranda yok.
            if (!TaskbarWindowRule.ShouldShow(ReadTaskbarFacts(hwnd)))
                return true;

            // Pencere başlığı oku. Boş başlık eleme sebebi değil (başlığı sonradan konan pencereler kaybolmasın);
            // boşsa dock süreç adını gösterir.
            Span<char> titleBuffer = stackalloc char[256];
            int titleLen;
            fixed (char* pTitle = titleBuffer)
            {
                titleLen = PInvoke.GetWindowText(hwnd, pTitle, 256);
            }

            var title = titleLen > 0 ? titleBuffer[..titleLen].ToString() : string.Empty;

            // Boyut kontrolü
            if (!PInvoke.GetWindowRect(hwnd, out var wr) || wr.right <= wr.left || wr.bottom <= wr.top)
                return true;

            // Süreç bilgisi
            uint pid = 0;
            PInvoke.GetWindowThreadProcessId(hwnd, &pid);

            // Mağaza (UWP) penceresinin sahibi ApplicationFrameHost.exe'dir ve bütün Mağaza uygulamalarına ortaktır.
            // Gerçek uygulama sürecini çocuk CoreWindow'dan bul; bulunamazsa 0 (askıdaki uygulama): "Sonlandır" çalışmaz
            // ama başka uygulamaları da kapatmaz.
            var isFrameWindow = IsApplicationFrameWindow(hwnd);
            if (isFrameWindow)
                pid = ResolveUwpProcessId(hwnd, pid);

            // AUMID: Windows görev çubuğu düğmeleri bununla gruplar; sabitli kısayolla eşleşmede önce o kullanılır.
            var aumid = GetAppUserModelId(hwnd);

            var procPath = GetProcessPath(pid);

            // Mağaza uygulamaları ApplicationFrameHost.exe içinde çalışır; hepsi aynı exe'ye düşüp genel simgeyle
            // tek öğede toplanmasın diye uygulama kimliğinden (AUMID) kabuk yolu üretilir. Bu yol hem simge hem başlatma için geçerli.
            if (isFrameWindow && aumid is { Length: > 0 })
                procPath = @"shell:AppsFolder\" + aumid;

            var iconData = !string.IsNullOrEmpty(procPath) ? IconHelper.GetIconBytes(procPath) : null;
            var bounds = new WindowRect(wr.left, wr.top, wr.right, wr.bottom);
            windows.Add(new OpenWindowInfo(hwnd.Value, title, procPath, (int)pid, bounds, iconData, aumid, GetClassNameString(hwnd)));

            return true;
        }, 0);

        PruneAumidCache(seenHandles);
        return windows;
    }

    public void ActivateWindow(nint handle)
    {
        if (handle == 0) return;
        var hwnd = new HWND(handle);

        if (PInvoke.IsIconic(hwnd))
            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);

        PInvoke.SetForegroundWindow(hwnd);
        PInvoke.BringWindowToTop(hwnd);
    }

    public void MinimizeWindow(nint handle)
    {
        if (handle == 0) return;
        PInvoke.ShowWindow(new HWND(handle), SHOW_WINDOW_CMD.SW_MINIMIZE);
    }

    public void CloseWindow(nint handle)
    {
        if (handle == 0) return;
        PInvoke.PostMessage(new HWND(handle), 0x0010 /* WM_CLOSE */, default, default);
    }

    public void TerminateProcess(int processId)
    {
        if (processId <= 0) return;
        try
        {
            using var proc = Process.GetProcessById(processId);
            // Ortak çatı süreçleri (ApplicationFrameHost, explorer…) öldürülürse ilgisiz uygulamalar da kapanır.
            if (!ProcessTerminationPolicy.CanTerminate(processId, proc.ProcessName, Environment.ProcessId))
                return;

            proc.Kill();
        }
        catch
        {
            // Süreç zaten sonlanmış olabilir
        }
    }

    private void OnWinEvent(
        HWINEVENTHOOK hWinEventHook,
        uint @event,
        HWND hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
    {
        // Yerel geri çağrıdan sızan istisna süreci düşürür; logla ve yut.
        try
        {
            HandleWinEvent(@event, hwnd, idObject, idChild);
        }
        catch (Exception ex)
        {
            Koy.Core.Diagnostics.CrashLog.Write("WindowTracker.OnWinEvent", ex);
        }
    }

    private void HandleWinEvent(uint @event, HWND hwnd, int idObject, int idChild)
    {
        if (hwnd.IsNull)
            return;

        // Koy'un kendi pencerelerinin (dock, açılır pencereler, önizleme kutucuğu, thumbnail host, menüler)
        // olayları yoksayılır; aksi halde popup açılışında döngüsel tetiklenme (flicker) ve kilitlenme oluşur.
        uint pid = 0;
        unsafe { PInvoke.GetWindowThreadProcessId(hwnd, &pid); }
        if (pid == (uint)Environment.ProcessId)
            return;

        if (@event == PInvoke.EVENT_OBJECT_DESTROY && idObject == 0 && idChild == 0)
        {
            lock (_aumidGate)
                _aumidCache.Remove(hwnd.Value);
        }

        // Yaşam döngüsü olayları (pencere açılma / kapanma)
        if (@event is PInvoke.EVENT_OBJECT_CREATE or PInvoke.EVENT_OBJECT_DESTROY
                   or PInvoke.EVENT_OBJECT_SHOW or PInvoke.EVENT_OBJECT_HIDE)
        {
            if (idObject == 0 && idChild == 0)
            {
                WindowsChanged?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        // Gizlilikten çıkış: yalnız üst düzey pencere (idObject 0, idChild 0) ve görev çubuğu penceresi ise.
        // Gizlenme (CLOAKED) pencereyi listeden düşürür; o an kural artık sağlanmadığından yalnız üst düzey olması aranır.
        if (@event is PInvoke.EVENT_OBJECT_UNCLOAKED or PInvoke.EVENT_OBJECT_CLOAKED)
        {
            if (idObject == 0 && idChild == 0 && PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd
                && (@event == PInvoke.EVENT_OBJECT_CLOAKED || IsTopLevelTaskbarWindow(hwnd)))
                WindowsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Başlık değişimi: görev çubuğu penceresi değilse (alt pencere, araç penceresi, kabuk) hiç ilgilenilmez.
        if (@event == PInvoke.EVENT_OBJECT_NAMECHANGE)
        {
            if (idObject == 0 && idChild == 0 && IsTopLevelTaskbarWindow(hwnd))
                _nameChangeDebouncer?.Request();
            return;
        }

        // Konum değişiminde yalnızca ana pencere (idObject == 0) seviyesindeki hareketleri dikkate al
        if (@event == PInvoke.EVENT_OBJECT_LOCATIONCHANGE)
        {
            if (idObject != 0)
                return;

            // Yalnızca mevcut ön plan penceresi hareket ettiyse güncelle (arka plan pencerelerinin gürültüsünü filtrele)
            if (CurrentForeground is not null && hwnd.Value != CurrentForeground.Handle)
                return;
        }

        UpdateForegroundWindow();
    }

    private void UpdateForegroundWindow()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull)
        {
            if (CurrentForeground is not null)
            {
                CurrentForeground = null;
                ForegroundWindowChanged?.Invoke(this, new WindowInfo(nint.Zero, WindowRect.Empty));
            }
            return;
        }

        var bounds = WindowRect.Empty;
        if (!IsShellOrDesktop(hwnd) && PInvoke.GetWindowRect(hwnd, out var wr))
        {
            bounds = new WindowRect(wr.left, wr.top, wr.right, wr.bottom);
        }

        var info = new WindowInfo(hwnd.Value, bounds);
        if (Equals(CurrentForeground, info))
            return;

        CurrentForeground = info;
        ForegroundWindowChanged?.Invoke(this, info);
    }

    internal static unsafe string? GetProcessPath(uint pid)
    {
        if (pid == 0)
            return null;

        // Process.MainModule yönetici olarak çalışan süreçlerde erişim hatası verir; sınırlı sorgu hakkı yeterli.
        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process.IsInvalid)
            return null;

        Span<char> buffer = stackalloc char[1024];
        var size = (uint)buffer.Length;
        fixed (char* pBuffer = buffer)
        {
            if (!PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, pBuffer, ref size))
                return null;
        }

        return buffer[..(int)size].ToString();
    }

    /// <summary>Pencerenin görev çubuğu olgularını okur (kural Core'daki TaskbarWindowRule'da).</summary>
    private static TaskbarWindowFacts ReadTaskbarFacts(HWND hwnd)
    {
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var visible = PInvoke.IsWindowVisible(hwnd);
        return new TaskbarWindowFacts(
            IsVisible: visible,
            IsCloaked: visible && IsCloaked(hwnd),
            IsShellWindow: IsShellOrDesktop(hwnd),
            HasOwner: !PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull,
            IsAppWindow: (exStyle & WINDOW_EX_STYLE.WS_EX_APPWINDOW) != 0,
            IsToolWindow: (exStyle & WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0,
            IsNoActivate: (exStyle & WINDOW_EX_STYLE.WS_EX_NOACTIVATE) != 0);
    }

    /// <summary>Üst düzey (kökü kendisi olan) ve görev çubuğuna girecek bir pencere mi.</summary>
    private static bool IsTopLevelTaskbarWindow(HWND hwnd)
        => PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd
           && TaskbarWindowRule.ShouldShow(ReadTaskbarFacts(hwnd));

    private static unsafe bool IsCloaked(HWND hwnd)
    {
        int cloaked = 0;
        var hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int));
        return hr.Succeeded && cloaked != 0;
    }

    private static unsafe uint ResolveUwpProcessId(HWND frameWindow, uint framePid)
    {
        uint result = 0;
        PInvoke.EnumChildWindows(frameWindow, (HWND child, LPARAM _) =>
        {
            uint childPid = 0;
            PInvoke.GetWindowThreadProcessId(child, &childPid);
            if (childPid != 0 && childPid != framePid)
            {
                result = childPid;
                return false;
            }

            return true;
        }, 0);

        return result;
    }

    private static unsafe bool IsApplicationFrameWindow(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[64];
        fixed (char* ptr = buffer)
        {
            var len = PInvoke.GetClassName(hwnd, ptr, buffer.Length);
            return len > 0 && buffer[..len].SequenceEqual("ApplicationFrameWindow");
        }
    }

    // AUMID pencere ömrü boyunca değişmez sayılır: HWND başına bir kez okunur (her yenilemede COM çağrısı yapılmaz).
    // Pencere yok olunca (EVENT_OBJECT_DESTROY) ve her sayımda görülmeyen tutamaçlar için temizlenir.
    // Dolu değer kalıcı önbelleğe girer; boş sonuç kısa süre tutulur (uygulamalar AUMID'i pencere açıldıktan sonra koyabilir).
    private readonly Dictionary<nint, (string? Value, DateTime ReadUtc)> _aumidCache = [];
    private readonly object _aumidGate = new();
    private static readonly TimeSpan EmptyAumidRetry = TimeSpan.FromSeconds(10);

    private string? GetAppUserModelId(HWND hwnd)
    {
        lock (_aumidGate)
        {
            if (_aumidCache.TryGetValue(hwnd.Value, out var cached) &&
                (cached.Value is not null || DateTime.UtcNow - cached.ReadUtc < EmptyAumidRetry))
                return cached.Value;
        }

        var value = AppUserModelIdReader.ForWindow(hwnd);
        lock (_aumidGate)
            _aumidCache[hwnd.Value] = (value, DateTime.UtcNow);
        return value;
    }

    private static unsafe string? GetClassNameString(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[128];
        fixed (char* ptr = buffer)
        {
            var len = PInvoke.GetClassName(hwnd, ptr, buffer.Length);
            return len > 0 ? buffer[..len].ToString() : null;
        }
    }

    private void PruneAumidCache(HashSet<nint> seen)
    {
        lock (_aumidGate)
        {
            if (_aumidCache.Count <= seen.Count)
                return;

            foreach (var key in _aumidCache.Keys.Where(k => !seen.Contains(k)).ToList())
                _aumidCache.Remove(key);
        }
    }

    private static bool IsShellOrDesktop(HWND hwnd)
    {
        if (hwnd.IsNull || hwnd == PInvoke.GetDesktopWindow() || hwnd == PInvoke.GetShellWindow())
            return true;

        unsafe
        {
            Span<char> buffer = stackalloc char[256];
            fixed (char* ptr = buffer)
            {
                var len = PInvoke.GetClassName(hwnd, ptr, 256);
                if (len > 0)
                {
                    var className = buffer[..len];
                    if (className.SequenceEqual("Progman") ||
                        className.SequenceEqual("WorkerW") ||
                        className.SequenceEqual("Shell_TrayWnd") ||
                        className.SequenceEqual("Shell_SecondaryTrayWnd"))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}
