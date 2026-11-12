using Koy.Core.Settings;

namespace Koy.Core.Clipboard;

/// <summary>Yalnız sabitlenen pano öğelerinin dosyası (%AppData%\Koy\clipboard-pinned.json).</summary>
public sealed class ClipboardPinnedStore
{
    private readonly JsonListFile<ClipboardItem> _file;

    public ClipboardPinnedStore(string? path = null, Action<string, Exception>? onError = null) =>
        _file = new JsonListFile<ClipboardItem>(
            path ?? JsonListFile.InSettingsFolder("clipboard-pinned.json"),
            DataJsonContext.Default.ListClipboardItem,
            onError);

    public string Path => _file.Path;

    public List<ClipboardItem> Load() => _file.Load();

    public bool Save(IEnumerable<ClipboardItem> pinned) => _file.Save(pinned.Where(i => i.Pinned).ToList());
}
