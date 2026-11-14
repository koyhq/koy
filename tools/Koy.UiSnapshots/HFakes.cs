using Koy.Core.Clipboard;
using Koy.Core.Shell;
using Koy.Core.SystemStats;

namespace Koy.UiSnapshots;

// Pano, Sistem, Ana ve Odak sahneleri için sahte servisler: gerçek pano dinleyicisi, CPU örnekleyici ya da başlatıcı çalışmaz.

internal sealed class FakeSystemStats : ISystemStatsService
{
    public bool IsRunning => true;

    /// <summary>instantOnly: servis yeni kurulmuş gibi yalnız anlık değerler (RAM...) var, CPU / hız henüz yok.</summary>
    public FakeSystemStats(bool instantOnly = false)
    {
        Current = new SystemSnapshot(
            CpuPercent: instantOnly ? 0 : 23, TopProcessName: instantOnly ? null : "Chrome", TopProcessCpuPercent: instantOnly ? 0 : 11,
            RamUsedBytes: (long)(9.2 * 1024 * 1024 * 1024), RamTotalBytes: 16L * 1024 * 1024 * 1024, RamPercent: 58,
            NetDownBytesPerSec: instantOnly ? 0 : (long)(2.5 * 1024 * 1024), NetUpBytesPerSec: instantOnly ? 0 : 360 * 1024, NetworkName: Sample.T("Ev Ağı", "Home"),
            KoyMemoryBytes: 64L * 1024 * 1024, RatesReady: !instantOnly);
    }

    public SystemSnapshot Current { get; }

    public event EventHandler<SystemSnapshot>? Updated { add { } remove { } }
    public void Start() { }
    public void Stop() { }
    public void Dispose() { }
}

internal sealed class FakeShellActions : IShellActionService
{
    public void SendWinKey() { }
    public void ToggleSystemTray() { }
    public void OpenQuickSettings() { }
    public void OpenNotificationCenter() { }
    public void Launch(string path, string? arguments = null) { }
}

internal sealed class FakeClipboard : IClipboardHistoryService
{
    private readonly List<ClipboardItem> _items;

    public FakeClipboard()
    {
        var now = DateTimeOffset.Now;
        ClipboardItem Make(string text, bool pinned, TimeSpan age) =>
            new(Guid.NewGuid(), text, ClipboardClassifier.Classify(text), pinned, now - age);

        _items =
        [
            Make(Sample.T("https://ornek.com/sayfa/baslik", "https://example.com/page/title"), true, TimeSpan.FromHours(3)),
            Make("#3B82F6", false, TimeSpan.FromMinutes(2)),
            Make(Sample.T("Toplantı notları:\nBütçe onaylandı, tasarım teslimi cuma.\nSonraki görüşme salı.", "Meeting notes:\nBudget approved, design due Friday.\nNext call Tuesday."), false, TimeSpan.FromSeconds(10)),
            Make(Sample.T("var sonuc = liste.Where(x => x.Aktif).ToList();", "var result = items.Where(x => x.Active).ToList();"), false, TimeSpan.FromMinutes(40)),
            Make("rgb(250, 204, 21)", false, TimeSpan.FromHours(1)),
            Make(Sample.T("https://docs.ornek.com/rehber", "https://docs.example.com/guide"), false, TimeSpan.FromHours(5)),
            Make(Sample.T("Yarın 10:00 müşteri görüşmesi", "Client call tomorrow at 10:00"), false, TimeSpan.FromDays(1))
        ];
    }

    public bool IsRunning => true;
    public event EventHandler? Changed { add { } remove { } }
    public void Start() { }
    public void Stop() { }

    public IReadOnlyList<ClipboardItem> Query(ClipboardFilter filter = ClipboardFilter.All, string? search = null) =>
        _items.Where(i => filter switch
        {
            ClipboardFilter.Pinned => i.Pinned,
            ClipboardFilter.Link => i.Kind == ClipboardKind.Link,
            ClipboardFilter.Color => i.Kind == ClipboardKind.Color,
            ClipboardFilter.Code => i.Kind == ClipboardKind.Code,
            _ => true
        }).Where(i => string.IsNullOrEmpty(search) || i.Text.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToList();

    public bool SetPinned(Guid id, bool pinned) => true;
    public bool Remove(Guid id) => true;
    public void Clear() { }
    public bool CopyBack(Guid id) => true;
    public void Dispose() { }
}
