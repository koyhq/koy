using System.Diagnostics;
using System.IO;
using Koy.Core.Diagnostics;
using Koy.Core.Search;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Koy.Platform.Search;

/// <summary>Kilitle, uyku, oturum ve güç komutları.</summary>
public sealed class SystemCommandService : ISystemCommandService
{
    private const uint CurrentSession = 0xFFFFFFFF;

    public void Execute(SystemCommand command)
    {
        switch (command)
        {
            case SystemCommand.Lock:
                PInvoke.LockWorkStation();
                break;
            case SystemCommand.Sleep:
                PInvoke.SetSuspendState(false, false, false);
                break;
            case SystemCommand.SwitchUser:
                PInvoke.WTSDisconnectSession(HANDLE.Null, CurrentSession, false);
                break;
            case SystemCommand.SignOut:
                RunShutdown("/l");
                break;
            case SystemCommand.Restart:
                RunShutdown("/r /t 0");
                break;
            case SystemCommand.Shutdown:
                RunShutdown("/s /t 0");
                break;
        }
    }

    private static void RunShutdown(string arguments)
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(system, "shutdown.exe"), arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }
}

/// <summary>Arama sonuçlarının dosya sistemi ve süreç işleri. Pano WPF'e bağlı olduğu için dışarıdan verilir.</summary>
public sealed class SearchShell(Action<string> copyText) : ISearchShell
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public void Open(string target) => Start(new ProcessStartInfo(target) { UseShellExecute = true });

    public void Run(string executable, string? arguments) =>
        Start(new ProcessStartInfo(executable, arguments ?? "") { UseShellExecute = true });

    public void RunInTerminal(string commandLine) =>
        Start(new ProcessStartInfo("cmd.exe", "/k " + SearchCommandSafety.ToSingleLine(commandLine)) { UseShellExecute = true });

    public void OpenContainingFolder(string path) =>
        Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

    public void CopyText(string text) => copyText(text);

    private static void Start(ProcessStartInfo info)
    {
        try
        {
            Process.Start(info)?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // Hedef silinmiş ya da kullanıcı yetki istemini iptal etmiş.
            CrashLog.Write("Arama sonucu açma", ex);
        }
    }
}

/// <summary>Çalışan süreçler ve kapatma isteği.</summary>
public static class SearchProcesses
{
    public static IReadOnlyList<RunningProcess> List()
    {
        var result = new List<RunningProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id > 4)
                    result.Add(new RunningProcess(process.Id, process.ProcessName, StartTicks(process)));
            }
        }

        return result;
    }

    public static void RequestClose(RunningProcess target)
    {
        try
        {
            using var process = Process.GetProcessById(target.Id);
            if (IsSame(process, target))
                process.CloseMainWindow();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Süreç bu arada kapanmış.
        }
    }

    /// <summary>Zorla sonlandırır; kimlik başka bir sürece geçmişse dokunmaz.</summary>
    public static void Terminate(RunningProcess target, Action<int> kill)
    {
        try
        {
            using var process = Process.GetProcessById(target.Id);
            if (IsSame(process, target))
                kill(target.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Süreç bu arada kapanmış.
        }
    }

    // Liste alındığından beri kimlik başka sürece verilmiş olabilir: ad ve başlangıç zamanı aynı olmalı.
    private static bool IsSame(Process process, RunningProcess target) =>
        string.Equals(process.ProcessName, target.Name, StringComparison.OrdinalIgnoreCase)
        && StartTicks(process) == target.StartTicks;

    private static long StartTicks(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime().Ticks;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Korumalı süreç: başlangıç zamanı okunamaz, yalnız ad karşılaştırılır.
            return 0;
        }
    }
}

/// <summary>Chrome ailesi tarayıcıların yer imi dosyaları.</summary>
public static class SearchBookmarks
{
    private const long MaxFileBytes = 16 * 1024 * 1024;

    private static readonly FileStampCache<IReadOnlyList<BrowserBookmark>> Cache = new();

    private static readonly string[] Browsers =
    [
        @"Google\Chrome\User Data",
        @"Microsoft\Edge\User Data",
        @"BraveSoftware\Brave-Browser\User Data"
    ];

    public static IReadOnlyList<BrowserBookmark> Load()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var bookmarks = new List<BrowserBookmark>();
        foreach (var browser in Browsers)
        {
            var directory = Path.Combine(root, browser);
            if (!Directory.Exists(directory))
                continue;

            foreach (var profile in Directory.EnumerateDirectories(directory).Where(IsProfile))
                bookmarks.AddRange(ReadFile(Path.Combine(profile, "Bookmarks")));
        }

        return bookmarks.DistinctBy(b => b.Url, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsProfile(string directory)
    {
        var name = Path.GetFileName(directory);
        return name == "Default" || name.StartsWith("Profile ", StringComparison.Ordinal);
    }

    private static IReadOnlyList<BrowserBookmark> ReadFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                Cache.Remove(path);
                return [];
            }

            // Dosya değişmediyse (son yazma zamanı + boyut) ayrıştırılmış liste yeniden kullanılır.
            return Cache.GetOrLoad(path, info.LastWriteTimeUtc.Ticks, info.Length, () =>
            {
                // Tarayıcı dosyayı yazıyor olabilir.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return ChromiumBookmarks.Parse(reader.ReadToEnd());
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

/// <summary>Bilinen klasörler ve yerel sürücüler.</summary>
public static class SearchPlaces
{
    public static IReadOnlyList<SearchPlace> Load(Func<string, string> t)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var places = new List<SearchPlace>
        {
            new(t("SearchPlaceHome"), home),
            new(t("SearchPlaceDesktop"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            new(t("SearchPlaceDocuments"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            new(t("SearchPlaceDownloads"), Path.Combine(home, "Downloads")),
            new(t("SearchPlaceMusic"), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
            new(t("SearchPlacePictures"), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
            new(t("SearchPlaceVideos"), Environment.GetFolderPath(Environment.SpecialFolder.MyVideos))
        };

        var devices = t("SearchPlaceDevices");
        try
        {
            // Ağ ve optik sürücüler yoklanmaz: yanıt vermezlerse panel açılışı bekler.
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable))
                places.Add(new SearchPlace(drive.Name.TrimEnd('\\'), drive.Name, devices));
        }
        catch (IOException)
        {
            // Sürücü listesi alınamadı; klasörler yeter.
        }

        return places.Where(p => !string.IsNullOrEmpty(p.Path)).ToList();
    }
}
