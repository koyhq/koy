using System.IO;
using System.Text.Json;
using Koy.Core.Shelf;

namespace Koy.Platform.Shelf;

public sealed class ShelfService : IShelfService
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy",
        "shelf.json");

    private readonly string _filePath;
    private readonly List<ShelfItem> _items = [];

    public IReadOnlyList<ShelfItem> Items => _items;

    public event EventHandler? ItemsChanged;

    public ShelfService(string? filePath = null)
    {
        _filePath = filePath ?? DefaultPath;
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize(json, ShelfJsonContext.Default.ListShelfItem);
                if (loaded is not null)
                {
                    _items.Clear();
                    _items.AddRange(loaded);
                }
            }
        }
        catch
        {
            _items.Clear();
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_items, ShelfJsonContext.Default.ListShelfItem);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
        }
    }

    public void AddFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var fullPath = Path.GetFullPath(path);
        var isDir = Directory.Exists(fullPath);
        var name = isDir ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar)) : Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(name))
            name = fullPath;

        var kind = isDir ? ShelfItemKind.Directory : ShelfItemKind.File;
        var item = new ShelfItem(Guid.NewGuid().ToString("N"), name, fullPath, kind, DateTimeOffset.UtcNow);

        // Zaten aynı yol varsa önce eskiyi kaldırıp en başa ekle
        _items.RemoveAll(i => string.Equals(i.PathOrContent, fullPath, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, item);

        Save();
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var displayName = text.Trim();
        if (displayName.Length > 30)
            displayName = displayName[..30] + "...";

        var item = new ShelfItem(Guid.NewGuid().ToString("N"), displayName, text, ShelfItemKind.Text, DateTimeOffset.UtcNow);
        _items.Insert(0, item);

        Save();
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string id)
    {
        if (_items.RemoveAll(i => i.Id == id) > 0)
        {
            Save();
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        if (_items.Count > 0)
        {
            _items.Clear();
            Save();
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Restore(IReadOnlyList<ShelfItem> items)
    {
        var added = false;
        foreach (var item in items)
        {
            if (item is null || _items.Any(i => i.Id == item.Id
                    || (item.Kind != ShelfItemKind.Text && string.Equals(i.PathOrContent, item.PathOrContent, StringComparison.OrdinalIgnoreCase))))
                continue;
            _items.Add(item);
            added = true;
        }

        if (!added)
            return;

        // OrderBy kararlı: eklenme zamanına göre en yeni başta.
        var sorted = _items.OrderByDescending(i => i.AddedAt).ToList();
        _items.Clear();
        _items.AddRange(sorted);
        Save();
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
    }
}
