using System.Runtime.InteropServices;
using Koy.Core.Clipboard;
using Koy.Core.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Clipboard;

/// <summary>
/// Win32 pano dinleyicisi (AddClipboardFormatListener, WM_CLIPBOARDUPDATE). Yoklama yok: pano değişmedikçe iş
/// parçacığı GetMessage'da uyur (CPU %0). Gizli mesaj penceresi kendi iş parçacığında çalışır; arayüz iş
/// parçacığı pano sahibi uygulamayı beklemez. Dinleyici yalnız <see cref="Start"/> ile kurulur, <see cref="Stop"/>
/// ile pencere ve iş parçacığı kalkar. <see cref="Changed"/> ARKA PLAN iş parçacığından gelebilir.
/// </summary>
public sealed class ClipboardHistoryService : IClipboardHistoryService
{
    private const string ClassName = "KoyClipboardListener";
    private const uint WmClipboardUpdate = 0x031D;
    private const uint WmCopyBack = 0x8000 + 1; // WM_APP + 1
    private const uint WmClose = 0x0010;
    private const uint WmDestroy = 0x0002;
    private const uint CfUnicodeText = 13;
    private const int OpenRetries = 8;

    private readonly ClipboardHistory _history = new();
    private readonly ClipboardPinnedStore _store;
    private readonly object _gate = new();
    private readonly WNDPROC _wndProc;
    private readonly uint _fmtExclude;
    private readonly uint _fmtCanInclude;
    private readonly uint _fmtViewerIgnore;

    private Thread? _thread;
    private HWND _hwnd;
    private bool _pinnedLoaded;
    private bool _disposed;
    private Guid _pendingCopy;

    // Kendi yazdığımız değişikliğin pano sıra numarası aralığı: bu aralıktaki güncellemeler geçmişe eklenmez.
    private long _ownFrom = -1;
    private long _ownTo = -1;

    public ClipboardHistoryService(ClipboardPinnedStore? store = null)
    {
        _store = store ?? new ClipboardPinnedStore();
        _wndProc = WndProc;
        _fmtExclude = PInvoke.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        _fmtCanInclude = PInvoke.RegisterClipboardFormat("CanIncludeInClipboardHistory");
        _fmtViewerIgnore = PInvoke.RegisterClipboardFormat("Clipboard Viewer Ignore");
    }

    public bool IsRunning
    {
        get { lock (_gate) return _thread is { IsAlive: true } && !_hwnd.IsNull; }
    }

    public event EventHandler? Changed;

    public void Start()
    {
        Thread? previous;
        lock (_gate)
        {
            if (_disposed || _thread is { IsAlive: true } && !_hwnd.IsNull)
                return;
            previous = _thread;
        }

        // Önceki pencere hâlâ kapanıyorsa kısa bekle (sınıf adı çakışmasın).
        previous?.Join(TimeSpan.FromSeconds(1));

        if (!_pinnedLoaded)
        {
            _history.LoadPinned(_store.Load());
            _pinnedLoaded = true;
        }

        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Koy pano" };
        lock (_gate)
            _thread = thread;
        thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    public void Stop()
    {
        HWND hwnd;
        lock (_gate)
        {
            hwnd = _hwnd;
            _hwnd = HWND.Null;
        }

        // Arayüz iş parçacığını bekletme: pencere kendi iş parçacığında kapanır.
        if (!hwnd.IsNull)
            PInvoke.PostMessage(hwnd, WmClose, 0, 0);
    }

    public IReadOnlyList<ClipboardItem> Query(ClipboardFilter filter = ClipboardFilter.All, string? search = null) =>
        _history.Query(filter, search);

    public bool SetPinned(Guid id, bool pinned)
    {
        if (!_history.SetPinned(id, pinned))
            return false;
        _store.Save(_history.PinnedSnapshot());
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Remove(Guid id)
    {
        var wasPinned = _history.Get(id)?.Pinned == true;
        if (!_history.Remove(id))
            return false;
        if (wasPinned)
            _store.Save(_history.PinnedSnapshot());
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        if (_history.Clear() > 0)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Restore(IReadOnlyList<ClipboardItem> items)
    {
        if (_history.Restore(items) == 0)
            return;

        if (items.Any(i => i.Pinned))
            _store.Save(_history.PinnedSnapshot());
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool CopyBack(Guid id)
    {
        HWND hwnd;
        lock (_gate)
        {
            hwnd = _hwnd;
            _pendingCopy = id;
        }

        // Pano yazımı bir pencere sahibi ister (OpenClipboard(NULL) ile SetClipboardData başarısız olur);
        // bu yüzden iş dinleyici penceresinin iş parçacığında yapılır.
        if (hwnd.IsNull)
            return false;

        unsafe
        {
            nuint result = 0;
            var ok = PInvoke.SendMessageTimeout(hwnd, WmCopyBack, 0, 0, SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG, 2000, &result);
            return ok.Value != 0 && result == 1;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        Stop();
    }

    private unsafe void Run(ManualResetEventSlim ready)
    {
        HINSTANCE instance = default;
        try
        {
            instance = new HINSTANCE(PInvoke.GetModuleHandle(default(PCWSTR)).Value);
            HWND hwnd;
            fixed (char* className = ClassName)
            {
                var wc = new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = _wndProc,
                    hInstance = instance,
                    lpszClassName = className
                };
                PInvoke.RegisterClassEx(wc);

                // HWND_MESSAGE (-3): yalnız ileti alan, görünmez, Z-sırasında olmayan pencere.
                hwnd = PInvoke.CreateWindowEx(0, className, null, 0, 0, 0, 0, 0, new HWND(-3), HMENU.Null, instance, null);
            }

            if (hwnd.IsNull || !PInvoke.AddClipboardFormatListener(hwnd))
            {
                if (!hwnd.IsNull)
                    PInvoke.DestroyWindow(hwnd);
                return;
            }

            lock (_gate)
                _hwnd = hwnd;
        }
        catch (Exception ex)
        {
            CrashLog.Write("ClipboardHistoryService.Run", ex);
            return;
        }
        finally
        {
            ready.Set();
        }

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        fixed (char* className = ClassName)
            PInvoke.UnregisterClass(className, instance);
    }

    private LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            switch (msg)
            {
                case WmClipboardUpdate:
                    CrashLog.Guard("ClipboardHistoryService.Update", OnClipboardUpdate);
                    return new LRESULT(0);

                case WmCopyBack:
                    return new LRESULT(WriteBack(hwnd) ? 1 : 0);

                case WmClose:
                    PInvoke.RemoveClipboardFormatListener(hwnd);
                    PInvoke.DestroyWindow(hwnd);
                    return new LRESULT(0);

                case WmDestroy:
                    PInvoke.PostQuitMessage(0);
                    return new LRESULT(0);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("ClipboardHistoryService.WndProc", ex);
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void OnClipboardUpdate()
    {
        var seq = PInvoke.GetClipboardSequenceNumber();
        var from = Interlocked.Read(ref _ownFrom);
        var to = Interlocked.Read(ref _ownTo);
        if (from >= 0 && seq >= from && seq <= to)
            return; // kendi yazdığımız değişiklik

        var text = ReadText();
        if (text is null)
            return;

        if (_history.Add(text) is not null)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Metni okur; metin yoksa ya da parola yöneticisi dışlama işareti varsa null.</summary>
    private unsafe string? ReadText()
    {
        if (!TryOpen(_hwnd))
            return null;

        try
        {
            if (!PInvoke.IsClipboardFormatAvailable(CfUnicodeText))
                return null;

            // Parola yöneticileri bu biçimlerle "izleme/geçmiş dışı" der.
            if (PInvoke.IsClipboardFormatAvailable(_fmtExclude) || PInvoke.IsClipboardFormatAvailable(_fmtViewerIgnore))
                return null;
            if (PInvoke.IsClipboardFormatAvailable(_fmtCanInclude) && ReadDword(_fmtCanInclude) == 0)
                return null;

            var handle = PInvoke.GetClipboardData(CfUnicodeText);
            if (handle.IsNull)
                return null;

            var hGlobal = new HGLOBAL((void*)handle.Value);
            var size = (int)PInvoke.GlobalSize(hGlobal);
            var ptr = (char*)PInvoke.GlobalLock(hGlobal);
            if (ptr is null)
                return null;

            try
            {
                var max = size / sizeof(char);
                var len = 0;
                while (len < max && ptr[len] != '\0')
                    len++;
                return new string(ptr, 0, len);
            }
            finally
            {
                PInvoke.GlobalUnlock(hGlobal);
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    private unsafe uint? ReadDword(uint format)
    {
        var handle = PInvoke.GetClipboardData(format);
        if (handle.IsNull)
            return null;
        var hGlobal = new HGLOBAL((void*)handle.Value);
        if (PInvoke.GlobalSize(hGlobal) < sizeof(uint))
            return null;
        var ptr = (uint*)PInvoke.GlobalLock(hGlobal);
        if (ptr is null)
            return null;
        try
        {
            return *ptr;
        }
        finally
        {
            PInvoke.GlobalUnlock(hGlobal);
        }
    }

    private unsafe bool WriteBack(HWND hwnd)
    {
        Guid id;
        lock (_gate)
            id = _pendingCopy;

        var item = _history.Get(id);
        if (item is null)
            return false;

        var before = PInvoke.GetClipboardSequenceNumber();
        if (!TryOpen(hwnd))
            return false;

        var success = false;
        try
        {
            PInvoke.EmptyClipboard();
            var bytes = (nuint)((item.Text.Length + 1) * sizeof(char));
            var hMem = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, bytes);
            if (hMem.Value is null)
                return false;

            var ptr = (char*)PInvoke.GlobalLock(hMem);
            if (ptr is null)
            {
                PInvoke.GlobalFree(hMem);
                return false;
            }

            item.Text.AsSpan().CopyTo(new Span<char>(ptr, item.Text.Length));
            ptr[item.Text.Length] = '\0';
            PInvoke.GlobalUnlock(hMem);

            // Başarılı olursa bellek panoya geçer; başarısızsa biz serbest bırakırız.
            if (PInvoke.SetClipboardData(CfUnicodeText, new HANDLE((nint)hMem.Value)).IsNull)
            {
                PInvoke.GlobalFree(hMem);
                return false;
            }

            success = true;
            return true;
        }
        finally
        {
            PInvoke.CloseClipboard();
            if (success)
            {
                // Aralık: yazmadan önceki numaranın sonrasından kapattıktan sonraki numaraya kadar.
                Interlocked.Exchange(ref _ownFrom, (long)before + 1);
                Interlocked.Exchange(ref _ownTo, PInvoke.GetClipboardSequenceNumber());
            }
        }
    }

    // Pano başka uygulamada kilitliyse kısa aralıklarla birkaç kez dener (dinleyici iş parçacığında, arayüzü tutmaz).
    private static bool TryOpen(HWND hwnd)
    {
        for (var i = 0; i < OpenRetries; i++)
        {
            if (PInvoke.OpenClipboard(hwnd))
                return true;
            Thread.Sleep(10);
        }

        return false;
    }
}
