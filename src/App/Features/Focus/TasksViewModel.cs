using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.App.Features.Notch;
using Koy.Core.Notch;
using Koy.Core.Tasks;
using Koy.Core.Undo;

namespace Koy.App.Features.Focus;

/// <summary>Görev listesinin bir satırı (değişmez görünüm; tamamlama ve silme komutları üst modelde).</summary>
public sealed class TaskRowViewModel
{
    public TaskRowViewModel(TaskItem item, bool isNext, string dayText, DateOnly today, TimeOnly now)
    {
        Source = item;
        Id = item.Id;
        Title = item.Title;
        Done = item.Done;
        var (isNow, timeText) = TaskViews.Label(item, isNext, now, today);
        IsNext = isNext && !item.Done;
        IsNow = isNow;
        TimeText = timeText;
        DayText = dayText;
    }

    /// <summary>Silinirse "Geri al" için kayıt.</summary>
    public TaskItem Source { get; }

    public Guid Id { get; }
    public string Title { get; }
    public bool Done { get; }

    /// <summary>Listedeki ilk tamamlanmamış görev: vurgulu çizilir.</summary>
    public bool IsNext { get; }

    /// <summary>Sıradaki görevin saati geldi ya da saati yok: sağda "şimdi" yazar.</summary>
    public bool IsNow { get; }

    /// <summary>"16:00" ya da boş.</summary>
    public string TimeText { get; }

    /// <summary>Hafta / Ay kapsamında "Pzt 2"; Gün kapsamında boş.</summary>
    public string DayText { get; }

    public bool HasTime => TimeText.Length > 0;
    public bool HasDay => DayText.Length > 0;
}

/// <summary>
/// Odak sayfasındaki görev listesi (Bugün kartı): ekle (başlık + isteğe bağlı saat), tamamla, sil ve Gün · Hafta · Ay
/// kapsamı. Mantık <see cref="ITaskService"/> ve <see cref="TaskViews"/> içinde; burası yalnız bağlar.
/// Servise yalnız sayfa görünürken abone olunur; hiçbir zamanlayıcı yoktur.
/// </summary>
public sealed partial class TasksViewModel : ObservableObject, IDisposable
{
    private readonly ITaskService _tasks;
    private readonly Dispatcher _dispatcher;
    private readonly Func<DateTime> _now;
    private bool _active;
    private bool _disposed;

    public TasksViewModel(ITaskService tasks, Dispatcher? dispatcher = null, Func<DateTime>? now = null)
    {
        _tasks = tasks;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _now = now ?? (() => DateTime.Now);
        Title = Strings.Get("TaskToday");
    }

    public ObservableCollection<TaskRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private TaskScope _scope = TaskScope.Day;

    public bool IsDayScope => Scope == TaskScope.Day;
    public bool IsWeekScope => Scope == TaskScope.Week;
    public bool IsMonthScope => Scope == TaskScope.Month;

    /// <summary>Kartın başlığı: Bugün / Bu hafta / Bu ay.</summary>
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _newTitle = "";

    [ObservableProperty]
    private string _newTime = "";

    /// <summary>Saat alanındaki yazı geçersiz (alan kırmızı yazılır, görev eklenmez).</summary>
    [ObservableProperty]
    private bool _newTimeInvalid;

    partial void OnNewTimeChanged(string value)
    {
        NewTimeInvalid = false;
        FormError = "";
    }

    partial void OnScopeChanged(TaskScope value)
    {
        Title = Strings.Get(value switch
        {
            TaskScope.Week => "TaskThisWeek",
            TaskScope.Month => "TaskThisMonth",
            _ => "TaskToday"
        });
        OnPropertyChanged(nameof(IsDayScope));
        OnPropertyChanged(nameof(IsWeekScope));
        OnPropertyChanged(nameof(IsMonthScope));
        Reload();
    }

    /// <summary>Sayfa görünürken true: servis olayına abone olur ve listeyi tazeler (gün değişmiş olabilir).</summary>
    public void SetActive(bool active)
    {
        if (_disposed || active == _active)
            return;

        _active = active;
        if (active)
        {
            _tasks.Changed += OnTasksChanged;
            Reload();
        }
        else
        {
            _tasks.Changed -= OnTasksChanged;
        }
    }

    private void OnTasksChanged(object? sender, EventArgs e)
    {
        if (_dispatcher.CheckAccess())
        {
            if (!_disposed && _active)
                Reload();
            return;
        }

        _dispatcher.InvokeAsync(() =>
        {
            if (!_disposed && _active)
                Reload();
        });
    }

    private void Reload()
    {
        var now = _now();
        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);
        var items = TaskViews.Load(_tasks, Scope, today);
        var nextId = TaskViews.NextId(items);
        var culture = CultureInfo.CurrentUICulture;

        Rows.Clear();
        foreach (var item in items)
        {
            var dayText = Scope == TaskScope.Day ? "" : item.Day.ToDateTime(TimeOnly.MinValue).ToString("ddd d", culture);
            Rows.Add(new TaskRowViewModel(item, item.Id == nextId, dayText, today, time));
        }

        IsEmpty = Rows.Count == 0;
    }

    [RelayCommand]
    private void SetScope(TaskScope scope) => Scope = scope;

    /// <summary>Başlık boşsa ya da saat geçersizse eklenmez; saat boşsa görev saatsizdir. Hep bugüne eklenir.</summary>
    [RelayCommand]
    private void Add()
    {
        var error = QuickAddValidation.ForTask(NewTitle, NewTime);
        NewTimeInvalid = error == QuickAddError.TimeInvalid;
        FormError = error switch
        {
            QuickAddError.TitleEmpty => Strings.Get("FormTitleEmpty"),
            QuickAddError.TimeInvalid => Strings.Get("TaskTimeInvalid"),
            _ => ""
        };
        if (error != QuickAddError.None)
            return;

        // Doğrulama geçti: saat (boş olabilir) çözülür.
        _ = TaskViews.TryParseTime(NewTime, out var time);
        if (_tasks.Add(NewTitle, time) is null)
            return;

        NewTitle = "";
        NewTime = "";
    }

    [RelayCommand]
    private void Toggle(TaskRowViewModel? row)
    {
        if (row is not null)
            _tasks.SetDone(row.Id, !row.Done);
    }

    [RelayCommand]
    private void Remove(TaskRowViewModel? row)
    {
        if (row is null || !_tasks.Remove(row.Id))
            return;

        // Onay sormaz: görev hemen silinir, birkaç saniye "Geri al" çıkar (Burhan kararı, 2026-11-21).
        var source = row.Source;
        Undo?.Offer(UndoKind.TaskDeleted, 1, () => _tasks.Restore(source));
    }

    /// <summary>Geri al şeridi (NotchFeature verir).</summary>
    public UndoToastViewModel? Undo { get; set; }

    /// <summary>Başlık boşsa ya da saat geçersizse görünen hata; yoksa boş.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFormError))]
    private string _formError = "";

    public bool HasFormError => FormError.Length > 0;

    partial void OnNewTitleChanged(string value) => FormError = "";

    public void Dispose()
    {
        if (_disposed)
            return;

        SetActive(false);
        _disposed = true;
    }
}
