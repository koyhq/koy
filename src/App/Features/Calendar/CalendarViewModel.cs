using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.Core.Calendar;
using Koy.Core.Clock;
using Koy.Core.Settings;

namespace Koy.App.Features.Calendar;

/// <summary>Ay ızgarasının bir hücresi.</summary>
public sealed record CalendarCell(DateOnly Date, bool IsCurrentMonth, bool IsToday, bool IsSelected, bool HasEvents)
{
    public int Day => Date.Day;
}

/// <summary>Hafta görünümünün bir satırı.</summary>
public sealed record WeekDayRow(DateOnly Date, string Label, bool IsToday, bool IsSelected, string Summary, bool HasEvents);

/// <summary>Seçili günün bir etkinlik satırı.</summary>
public sealed record EventRow(string Id, string TimeText, string Title);

/// <summary>
/// Takvim sayfası (H): ay / hafta / gün görünümü ve yerel etkinlikler. Mantık Core/Calendar'da (MonthGrid,
/// EventAgenda, EventBook); bu sınıf yalnız bağlar. Zamanlayıcı yoktur: etkinlik defterine yalnız sayfa görünürken
/// abone olunur, boşta iş yapılmaz. Pomodoro ve sayaçlar Odak sayfasındadır.
/// </summary>
public sealed partial class CalendarViewModel : ObservableObject, IDisposable
{
    private readonly SharedEvents _events;
    private readonly Dispatcher _dispatcher;
    private readonly CultureInfo _culture;
    private readonly Func<DateOnly> _today;
    private readonly string _timePattern;
    private readonly DayOfWeek _firstDay;
    private readonly bool _showWeekNumber;
    private bool _active;
    private bool _disposed;

    public CalendarViewModel(AppSettings settings, SharedEvents? events = null, Dispatcher? dispatcher = null, Func<DateOnly>? today = null)
    {
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _events = events ?? new SharedEvents();
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Today));
        _culture = new CultureInfo(settings.General.Language == "en" ? "en-US" : "tr-TR");
        // Saat ve hafta ayarları (issue #77); ayar değişince çentik yeniden kurulur, burada bir kez okunur.
        _timePattern = ClockFormatPolicy.ResolveTimePattern(settings.Notch.ClockHourFormat, showSeconds: false, _culture);
        _firstDay = ClockFormatPolicy.ResolveFirstDayOfWeek(settings.Notch.ClockFirstDayOfWeek, _culture.DateTimeFormat.FirstDayOfWeek);
        _showWeekNumber = settings.Notch.ClockShowWeekNumber;
        // Pazar = 0 sırasıyla iki harfli başlıklar; ızgaranın ilk gününe göre döndürülür.
        string[] byDay = _culture.Name.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
            ? ["Pz", "Pt", "Sa", "Ça", "Pe", "Cu", "Ct"]
            : ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
        WeekdayHeaders = MonthGrid.WeekdayOrder(_firstDay).Select(d => byDay[(int)d]).ToArray();
        _anchor = _today();
        // Defter burada okunmaz (disk işi sayfa görününce yapılır); yalnız başlık kurulur.
        UpdateTitles();
    }

    public IReadOnlyList<string> WeekdayHeaders { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonthView), nameof(IsWeekView), nameof(IsDayView), nameof(ShowGrid))]
    private CalendarViewMode _viewMode = CalendarViewMode.Month;

    public bool IsMonthView => ViewMode == CalendarViewMode.Month;
    public bool IsWeekView => ViewMode == CalendarViewMode.Week;
    public bool IsDayView => ViewMode == CalendarViewMode.Day;

    /// <summary>Gün görünümünde sol kart yoktur, gündem tam genişliktedir.</summary>
    public bool ShowGrid => !IsDayView;

    /// <summary>Seçili gün: ay/hafta/gün görünümünün ve sağdaki gündemin dayanağı.</summary>
    [ObservableProperty]
    private DateOnly _anchor;

    [ObservableProperty]
    private string _headerTitle = "";

    /// <summary>Sağ kartın başlığı ("6 Kasım Perşembe").</summary>
    [ObservableProperty]
    private string _dayTitle = "";

    [ObservableProperty]
    private IReadOnlyList<CalendarCell> _cells = [];

    [ObservableProperty]
    private IReadOnlyList<WeekDayRow> _weekDays = [];

    public ObservableCollection<EventRow> DayEvents { get; } = [];

    [ObservableProperty]
    private bool _hasDayEvents;

    [ObservableProperty]
    private string _newEventTitle = "";

    [ObservableProperty]
    private string _newEventTime = "09:00";

    /// <summary>Girilen saat geçersiz (alan çerçevesi kırmızı).</summary>
    [ObservableProperty]
    private bool _newEventTimeInvalid;

    partial void OnNewEventTimeChanged(string value) => NewEventTimeInvalid = false;

    partial void OnViewModeChanged(CalendarViewMode value) => Refresh();

    /// <summary>Sayfa görünürken true: defter olayına abone olur, bugünü yeniler; false: abonelik kesilir.</summary>
    public void SetActive(bool active)
    {
        if (_disposed || active == _active)
            return;

        _active = active;
        if (active)
        {
            _events.Changed += OnEventsChanged;
            // Uygulama gece boyunca açık kalmış olabilir.
            Anchor = _today();
            Refresh();
        }
        else
        {
            _events.Changed -= OnEventsChanged;
        }
    }

    private void OnEventsChanged(object? sender, EventArgs e) => _dispatcher.InvokeAsync(() =>
    {
        if (!_disposed && _active)
            Refresh();
    });

    private void UpdateTitles()
    {
        var date = Anchor.ToDateTime(TimeOnly.MinValue);
        DayTitle = date.ToString("d MMMM dddd", _culture);
        if (_showWeekNumber)
            DayTitle += " · " + string.Format(_culture, Koy.App.Resources.Strings.Get("CalendarWeekNumber"), ClockFormatPolicy.WeekOfYear(date, _firstDay));
        switch (ViewMode)
        {
            case CalendarViewMode.Month:
                HeaderTitle = date.ToString("MMMM yyyy", _culture);
                break;
            case CalendarViewMode.Week:
                var first = EventAgenda.WeekStart(Anchor, _firstDay).ToDateTime(TimeOnly.MinValue);
                var last = first.AddDays(6);
                HeaderTitle = first.Month == last.Month
                    ? $"{first.Day} – {last.ToString("d MMM", _culture)}"
                    : $"{first.ToString("d MMM", _culture)} – {last.ToString("d MMM", _culture)}";
                break;
            default:
                HeaderTitle = date.ToString("MMMM yyyy", _culture);
                break;
        }
    }

    private void Refresh()
    {
        UpdateTitles();
        var today = _today();
        var all = _active ? _events.Book.Events : [];
        var withEvents = all.Select(e => e.Date).ToHashSet();

        if (ViewMode == CalendarViewMode.Month)
        {
            Cells = MonthGrid.Build(Anchor.Year, Anchor.Month, today, _firstDay)
                .Select(d => new CalendarCell(d.Date, d.IsCurrentMonth, d.IsToday, d.Date == Anchor, withEvents.Contains(d.Date)))
                .ToList();
        }
        else if (ViewMode == CalendarViewMode.Week)
        {
            WeekDays = EventAgenda.ForWeek(all, Anchor, today, _firstDay).Select(BuildWeekRow).ToList();
        }

        DayEvents.Clear();
        foreach (var e in EventAgenda.ForDay(all, Anchor))
            DayEvents.Add(new EventRow(e.Id, e.Start.ToString(_timePattern, _culture), e.Title));
        HasDayEvents = DayEvents.Count > 0;
    }

    private WeekDayRow BuildWeekRow(DayAgenda day)
    {
        var label = day.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd d", _culture);
        var summary = "";
        if (day.Events.Count > 0)
        {
            var first = day.Events[0];
            summary = $"{first.Start.ToString(_timePattern, _culture)} {first.Title}";
            if (day.Events.Count > 1)
                summary += $"  +{day.Events.Count - 1}";
        }

        return new WeekDayRow(day.Date, label, day.IsToday, day.Date == Anchor, summary, day.Events.Count > 0);
    }

    [RelayCommand]
    private void SetView(CalendarViewMode mode) => ViewMode = mode;

    [RelayCommand]
    private void Previous() => Move(-1);

    [RelayCommand]
    private void Next() => Move(1);

    private void Move(int direction)
    {
        Anchor = ViewMode switch
        {
            CalendarViewMode.Month => Anchor.AddMonths(direction),
            CalendarViewMode.Week => Anchor.AddDays(7 * direction),
            _ => Anchor.AddDays(direction)
        };
        Refresh();
    }

    [RelayCommand]
    private void GoToday()
    {
        Anchor = _today();
        Refresh();
    }

    /// <summary>Ay hücresine ya da hafta satırına tıklanınca o gün seçilir; sağdaki gündem onu gösterir.</summary>
    [RelayCommand]
    private void SelectDay(DateOnly date)
    {
        Anchor = date;
        Refresh();
    }

    [RelayCommand]
    private void AddEvent()
    {
        NewEventTimeInvalid = !EventAgenda.TryParseTime(NewEventTime, out var time);
        if (NewEventTimeInvalid)
            return;

        if (_events.Book.Add(NewEventTitle, Anchor, time) is null)
            return;

        NewEventTitle = "";
    }

    [RelayCommand]
    private void DeleteEvent(string id) => _events.Book.Remove(id);

    public void Dispose()
    {
        if (_disposed)
            return;

        SetActive(false);
        _disposed = true;
    }
}
