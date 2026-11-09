using Koy.Core.QuickLaunch;
using Koy.Core.Shell;

namespace Koy.Core.Tests;

[Collection("CrashLog.LogPath")]
public class QuickLaunchTests
{
    private sealed class FakeShell : IShellActionService
    {
        public List<string> Launched { get; } = [];
        public bool Throw { get; set; }
        public void SendWinKey() { }
        public void ToggleSystemTray() { }
        public void OpenQuickSettings() { }
        public void OpenNotificationCenter() { }
        public void Launch(string path, string? arguments = null)
        {
            if (Throw) throw new InvalidOperationException("başlatılamadı");
            Launched.Add(path);
        }
    }

    private static (QuickLaunchService Service, List<IReadOnlyList<string>> Saves, FakeShell Shell) New(params string[] initial)
    {
        var saves = new List<IReadOnlyList<string>>();
        var shell = new FakeShell();
        return (new QuickLaunchService(initial, saves.Add, shell, _ => true), saves, shell);
    }

    [Fact]
    public void Add_keeps_order_and_saves()
    {
        var (s, saves, _) = New();
        Assert.True(s.Add(@"C:\a.exe"));
        Assert.True(s.Add(@"C:\b.exe"));

        Assert.Equal([@"C:\a.exe", @"C:\b.exe"], s.Items);
        Assert.Equal([@"C:\a.exe", @"C:\b.exe"], saves[^1]);
    }

    [Fact]
    public void Add_rejects_duplicates_ignoring_case_and_trailing_slash()
    {
        var (s, _, _) = New(@"C:\Klasor");
        Assert.False(s.Add(@"c:\klasor\"));
        Assert.Single(s.Items);
    }

    [Fact]
    public void Add_strips_quotes_and_rejects_blank()
    {
        var (s, _, _) = New();
        Assert.True(s.Add("  \"C:\\a.exe\" "));
        Assert.Equal(@"C:\a.exe", s.Items[0]);
        Assert.False(s.Add("  "));
    }

    [Fact]
    public void Add_rejects_missing_path()
    {
        var s = new QuickLaunchService([], _ => { }, new FakeShell(), _ => false);
        Assert.False(s.Add(@"C:\yok.exe"));
    }

    [Fact]
    public void Add_stops_at_eight()
    {
        var (s, _, _) = New();
        for (var i = 0; i < 8; i++)
            Assert.True(s.Add($@"C:\{i}.exe"));
        Assert.False(s.Add(@"C:\9.exe"));
        Assert.Equal(8, s.Items.Count);
    }

    [Fact]
    public void Initial_list_is_cleaned_deduped_and_capped()
    {
        var initial = new List<string> { @"C:\a", "", @"c:\A" };
        for (var i = 0; i < 12; i++)
            initial.Add($@"C:\{i}");
        var (s, _, _) = New([.. initial]);

        Assert.Equal(8, s.Items.Count);
        Assert.Equal(@"C:\a", s.Items[0]);
    }

    [Fact]
    public void Remove_and_move()
    {
        var (s, saves, _) = New(@"C:\a", @"C:\b", @"C:\c");

        Assert.True(s.Move(0, 2));
        Assert.Equal([@"C:\b", @"C:\c", @"C:\a"], s.Items);
        Assert.False(s.Move(5, 0));
        Assert.True(s.Remove(@"C:\C"));
        Assert.False(s.Remove(@"C:\yok"));
        Assert.Equal([@"C:\b", @"C:\a"], saves[^1]);
    }

    [Fact]
    public void Launch_goes_through_shell_and_swallows_failure()
    {
        var (s, _, shell) = New(@"C:\a");
        Assert.True(s.Launch(@"C:\a"));
        Assert.Equal([@"C:\a"], shell.Launched);

        shell.Throw = true;
        var logPath = Path.Combine(Path.GetTempPath(), "koy-ql-" + Guid.NewGuid() + ".log");
        Koy.Core.Diagnostics.CrashLog.LogPath = logPath;
        try
        {
            Assert.False(s.Launch(@"C:\a"));
        }
        finally
        {
            Koy.Core.Diagnostics.CrashLog.LogPath = TestLogRedirect.TestLogPath;
            if (File.Exists(logPath)) File.Delete(logPath);
        }
    }

    [Fact]
    public void Add_picked_shortcut_is_saved_and_raises_changed_once()
    {
        // Dosya seçiciden gelen yol: .lnk çözülmeden (DereferenceLinks=false) olduğu gibi eklenir ve kaydedilir.
        var (s, saves, _) = New(@"C:\a.exe");
        var changed = 0;
        s.Changed += (_, _) => changed++;

        Assert.True(s.Add(@"C:\Users\x\Masaüstü\Uygulama.lnk"));

        Assert.Equal([@"C:\a.exe", @"C:\Users\x\Masaüstü\Uygulama.lnk"], s.Items);
        Assert.Equal(s.Items, saves[^1]);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Add_folder_is_saved_and_displayed()
    {
        var (s, saves, shell) = New();
        var changed = 0;
        s.Changed += (_, _) => changed++;

        const string folder = @"C:\Users\User\Projects";

        Assert.True(s.Add(folder));
        Assert.Single(s.Items);
        Assert.Equal(folder, s.Items[0]);
        Assert.Equal([folder], saves[^1]);
        Assert.Equal(1, changed);
        Assert.Equal("Projects", QuickLaunchService.DisplayName(folder));

        Assert.True(s.Launch(folder));
        Assert.Equal([folder], shell.Launched);
    }

    [Fact]
    public void Add_folder_with_trailing_slash_is_normalized_and_prevents_duplicate()
    {
        var (s, saves, _) = New();
        Assert.True(s.Add(@"C:\Projects\"));

        // Tekrar eden yollar (kuyruk çizgisi ve harf büyüklüğü fark etmeksizin) reddedilir
        Assert.False(s.Add(@"c:\projects"));
        Assert.False(s.Add(@"C:\Projects\"));
        Assert.Single(s.Items);

        // Kuyruk çizgisi olmadan silme işlemi de aynı klasörü bulup siler
        Assert.True(s.Remove(@"c:\projects"));
        Assert.Empty(s.Items);
        Assert.Empty(saves[^1]);
    }

    [Fact]
    public void Add_folder_respects_directory_exists_predicate()
    {
        var existsMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\ExistingFolder"] = true,
            [@"C:\MissingFolder"] = false
        };
        var s = new QuickLaunchService([], _ => { }, new FakeShell(), p => existsMap.GetValueOrDefault(p, false));

        Assert.True(s.Add(@"C:\ExistingFolder"));
        Assert.False(s.Add(@"C:\MissingFolder"));
        Assert.Single(s.Items);
    }

    [Fact]
    public void Add_network_share_folder_and_deduplicates()
    {
        var (s, _, _) = New();
        Assert.True(s.Add(@"\\server\share"));

        // Büyük/küçük harf ve kuyruk çizgisi farkı olan ağ paylaşımları reddedilir
        Assert.False(s.Add(@"\\SERVER\share\"));
        Assert.Single(s.Items);
    }

    [Theory]
    [InlineData(@"C:\Program Files\App\App.exe", "App")]
    [InlineData(@"C:\Users\x\Belgeler\", "Belgeler")]
    [InlineData(@"C:\Users\x\Belgeler", "Belgeler")]
    [InlineData(@"C:\", @"C:")]
    [InlineData(@"C:", @"C:")]
    [InlineData(@"D:\Projects\Koy", "Koy")]
    [InlineData(@"D:\Projects\Koy\", "Koy")]
    [InlineData(@"\\server\share", @"\\server\share")]
    [InlineData(@"\\server\share\", @"\\server\share")]
    [InlineData(@"\\server\share\belgeler", "belgeler")]
    [InlineData(@"\\server\share\belgeler\", "belgeler")]
    public void DisplayName(string path, string expected) => Assert.Equal(expected, QuickLaunchService.DisplayName(path));
}
