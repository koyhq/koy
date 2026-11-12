using Koy.Core.Settings;

namespace Koy.Core.Tasks;

/// <summary><see cref="ITaskService"/>'in dosya destekli uygulaması. Dosya yoksa/bozuksa boş liste ile devam eder.</summary>
public sealed class TaskService : ITaskService
{
    private readonly object _gate = new();
    private readonly JsonListFile<TaskItem> _file;
    private readonly Func<DateTimeOffset> _now;
    private readonly List<TaskItem> _items;

    public TaskService(string? path = null, Func<DateTimeOffset>? now = null, Action<string, Exception>? onError = null)
    {
        _now = now ?? (() => DateTimeOffset.Now);
        _file = new JsonListFile<TaskItem>(
            path ?? JsonListFile.InSettingsFolder("tasks.json"),
            DataJsonContext.Default.ListTaskItem,
            onError);
        _items = _file.Load().Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList();
        if (Prune())
            _file.Save(_items);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<TaskItem> GetToday()
    {
        var today = Today();
        lock (_gate)
        {
            return _items
                .Where(t => t.Day == today || (t.Day < today && !t.Done))
                .OrderBy(t => t.Done)
                .ThenBy(t => t.Day)
                .ThenBy(t => t.Time is null)
                .ThenBy(t => t.Time)
                .ThenBy(t => t.CreatedAt)
                .ToList();
        }
    }

    public IReadOnlyList<TaskItem> GetDay(DateOnly day) => GetRange(day, day);

    public IReadOnlyList<TaskItem> GetRange(DateOnly from, DateOnly to)
    {
        lock (_gate)
        {
            return _items
                .Where(t => t.Day >= from && t.Day <= to)
                .OrderBy(t => t.Day)
                .ThenBy(t => t.Done)
                .ThenBy(t => t.Time is null)
                .ThenBy(t => t.Time)
                .ThenBy(t => t.CreatedAt)
                .ToList();
        }
    }

    public TaskItem? Add(string title, TimeOnly? time = null, DateOnly? day = null)
    {
        title = title?.Trim() ?? "";
        if (title.Length == 0)
            return null;

        TaskItem item;
        lock (_gate)
        {
            item = new TaskItem(Guid.NewGuid(), title, day ?? Today(), time, false, _now());
            _items.Add(item);
            Prune();
            _file.Save(_items);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    public bool Update(Guid id, string title, TimeOnly? time, DateOnly? day = null)
    {
        title = title?.Trim() ?? "";
        if (title.Length == 0)
            return false;

        lock (_gate)
        {
            var index = _items.FindIndex(t => t.Id == id);
            if (index < 0)
                return false;
            _items[index] = _items[index] with { Title = title, Time = time, Day = day ?? _items[index].Day };
            _file.Save(_items);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool SetDone(Guid id, bool done)
    {
        lock (_gate)
        {
            var index = _items.FindIndex(t => t.Id == id);
            if (index < 0 || _items[index].Done == done)
                return false;
            _items[index] = _items[index] with { Done = done };
            _file.Save(_items);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Remove(Guid id)
    {
        lock (_gate)
        {
            if (_items.RemoveAll(t => t.Id == id) == 0)
                return false;
            _file.Save(_items);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private DateOnly Today() => DateOnly.FromDateTime(_now().LocalDateTime);

    /// <summary>
    /// 200'ü aşanı budar: önce tamamlananların en eskisi, tamamlanan kalmadıysa en eski kayıt.
    /// Bugünün görevleri tamamlanmamışken en son budanır.
    /// </summary>
    private bool Prune()
    {
        var removed = false;
        while (_items.Count > ITaskService.MaxTasks)
        {
            var victim = _items
                .OrderByDescending(t => t.Done)
                .ThenBy(t => t.Day)
                .ThenBy(t => t.CreatedAt)
                .First();
            _items.Remove(victim);
            removed = true;
        }

        return removed;
    }
}
