using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Koy.Core.Diagnostics;
using Koy.Core.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Koy.Platform.Shell;

// Koy'a özel kısım: görev çubuğu gizlenince gözcü süreci başlatılır ve RunOnce yedeği yazılır; temiz geri yüklemede
// gözcü durdurulur ve yedek silinir. Bu dosya Koy.Watchdog projesine bağlanmaz (orada kısmi yöntemler boştur).
public static partial class TaskbarService
{
    static partial void SendAppBarMessageOverride(uint message, ref APPBARDATA data, ref nuint result, ref bool handled)
    {
        var copy = data;
        result = TrayHostService.WithExplorerTrayFirst(() => PInvoke.SHAppBarMessage(message, ref copy));
        data = copy;
        handled = true;
    }

    static partial void OnHidden()
    {
        WatchdogLauncher.EnsureRunning();
        WatchdogLauncher.RegisterRunOnce();
    }

    static partial void OnRestoring() => WatchdogLauncher.Stop();
}

/// <summary>
/// Koy.Watchdog.exe'yi yönetir: Koy görev çubuğunu gizlediği sürece tek bir gözcü çalışır. Görev Yöneticisi
/// TerminateProcess ile Koy'u öldürünce hiçbir Koy kodu çalışmaz; gözcü sürecin kapandığını görüp görev çubuğunu
/// geri getirir. Gözcü exe'si yoksa (geliştirme çalıştırması) sessizce atlanır.
/// Gözcü, "Süreç ağacını sonlandır" / uygulama grubunu "Görevi bitir" ile Koy'la birlikte ölmesin diye ebeveyni
/// Koy değil explorer.exe olacak şekilde başlatılır (PROC_THREAD_ATTRIBUTE_PARENT_PROCESS); olmazsa Process.Start.
/// </summary>
internal static class WatchdogLauncher
{
    public const string ExeName = "Koy.Watchdog.exe";

    // PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = ProcThreadAttributeValue(0, FALSE, TRUE, FALSE)
    private const nuint ParentProcessAttribute = 0x00020000;

    // Başlatma başarısızsa ya da gözcü hemen çıkıyorsa (AV, AppLocker, ikinci gözcü) her pencere olayında yeniden denenmez; Stop sıfırlar.
    private const long RetryAfterFailureMs = 10_000;

    private static readonly object Gate = new();
    private static Process? _process;
    private static EventWaitHandle? _stopEvent;
    private static long _lastAttemptMs = long.MinValue / 2;

    private static string ExePath => Path.Combine(AppContext.BaseDirectory, ExeName);

    public static void RegisterRunOnce()
    {
        if (File.Exists(ExePath))
            TaskbarRunOnce.Register(ExePath);
    }

    public static void EnsureRunning()
    {
        lock (Gate)
        {
            try
            {
                if (_process is { HasExited: false })
                    return;

                var path = ExePath;
                if (!File.Exists(path))
                    return;

                var now = Environment.TickCount64;
                if (now - _lastAttemptMs < RetryAfterFailureMs)
                    return;

                _lastAttemptMs = now;
                ReleaseHandles();

                // Olay gözcüden önce kurulur ve Koy tutar: gözcü açtığında temiz çıkış işareti kaçmaz.
                _stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset,
                    WatchdogPolicy.StopEventName(Environment.ProcessId));
                _stopEvent.Reset();

                var arguments = WatchdogPolicy.BuildArguments(Environment.ProcessId);
                _process = StartWithExplorerParent(path, arguments) ?? StartAsChild(path, arguments);
                if (_process is null)
                    ReleaseHandles();
            }
            catch (Exception ex)
            {
                // Gözcü başlatılamazsa dock çalışmaya devam eder; açılıştaki Restore() korumaları ve RunOnce yedeği yerinde.
                CrashLog.Write("Gözcü başlatılamadı", ex);
                ReleaseHandles();
            }
        }
    }

    /// <summary>Temiz çıkış: gözcü görev çubuğuna dokunmadan kapanır.</summary>
    public static void Stop()
    {
        lock (Gate)
        {
            _lastAttemptMs = long.MinValue / 2;
            try
            {
                if (_process is null)
                    return;

                _stopEvent?.Set();

                // Kısa bekleme: gözcü olayı alıp çıksın ki Koy'un kapanışında yetim kalmasın.
                _process.WaitForExit(300);
            }
            catch
            {
                // Gözcü zaten kapanmış olabilir.
            }
            finally
            {
                ReleaseHandles();
            }
        }
    }

    private static Process? StartAsChild(string path, string arguments)
        => Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

    /// <summary>
    /// Gözcüyü ebeveyni explorer.exe olacak şekilde başlatır. Başarısızlıkta (Explorer yok, erişim reddi) null döner;
    /// çağıran Koy'un çocuğu olarak başlatır. Koy yükseltilmişse (jeton ebeveynden gelir; yükseltilmemiş gözcü Koy'u
    /// bekleyemez) denenmez.
    /// </summary>
    private static unsafe Process? StartWithExplorerParent(string path, string arguments)
    {
        if (Environment.IsPrivilegedProcess)
            return null;

        var tray = ShellTrayLocator.FindExplorerTray();
        if (tray == HWND.Null)
            return null;

        uint explorerPid = 0;
        _ = PInvoke.GetWindowThreadProcessId(tray, &explorerPid);
        if (explorerPid == 0)
            return null;

        var parent = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_CREATE_PROCESS, false, explorerPid);
        if (parent.IsNull)
            return null;

        nuint size = 0;
        nint attributeList = 0;
        var listInitialized = false;
        try
        {
            // İlk çağrı gereken boyutu verir (ERROR_INSUFFICIENT_BUFFER, dönüş değeri yok sayılır).
            _ = PInvoke.InitializeProcThreadAttributeList(default, 1, ref size);
            if (size == 0)
                return null;

            attributeList = Marshal.AllocHGlobal((nint)size);
            var list = new LPPROC_THREAD_ATTRIBUTE_LIST((void*)attributeList);
            if (!PInvoke.InitializeProcThreadAttributeList(list, 1, ref size))
                return null;
            listInitialized = true;

            var parentHandle = parent.Value;
            if (!PInvoke.UpdateProcThreadAttribute(list, 0, ParentProcessAttribute, &parentHandle, (nuint)sizeof(nint), null, (nuint*)null))
                return null;

            var startup = new STARTUPINFOEXW
            {
                StartupInfo = new STARTUPINFOW
                {
                    cb = (uint)sizeof(STARTUPINFOEXW),
                    dwFlags = STARTUPINFOW_FLAGS.STARTF_USESHOWWINDOW,
                    wShowWindow = (ushort)SHOW_WINDOW_CMD.SW_HIDE
                },
                lpAttributeList = list
            };

            // CreateProcessW komut satırı yazılabilir arabellek ister.
            var commandLine = ("\"" + path + "\" " + arguments + "\0").ToCharArray();
            Span<char> commandSpan = commandLine;
            var ok = PInvoke.CreateProcess(
                null,
                ref commandSpan,
                null,
                null,
                false,
                PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT | PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW,
                null,
                Path.GetDirectoryName(path),
                in startup.StartupInfo,
                out var info);
            if (!ok)
                return null;

            PInvoke.CloseHandle(info.hThread);
            PInvoke.CloseHandle(info.hProcess);
            try
            {
                return Process.GetProcessById((int)info.dwProcessId);
            }
            catch (ArgumentException)
            {
                // Gözcü hemen çıktı (başka bir gözcü zaten var); izlenecek bir şey yok.
                return null;
            }
        }
        finally
        {
            if (listInitialized)
                PInvoke.DeleteProcThreadAttributeList(new LPPROC_THREAD_ATTRIBUTE_LIST((void*)attributeList));
            if (attributeList != 0)
                Marshal.FreeHGlobal(attributeList);
            PInvoke.CloseHandle(parent);
        }
    }

    private static void ReleaseHandles()
    {
        _process?.Dispose();
        _process = null;
        _stopEvent?.Dispose();
        _stopEvent = null;
    }
}
