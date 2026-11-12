using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Timers;

namespace Koy.App.Features.Focus;

/// <summary>Odak sayfasının üstündeki alt sekmelerden biri (Odak, Mola, Sayaç, Kronometre).</summary>
public sealed partial class FocusTabViewModel(FocusKind kind, string label) : ObservableObject
{
    public FocusKind Kind { get; } = kind;

    public string Label { get; } = label;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Sayacın hazır süre düğmesi ("5 dk").</summary>
public sealed record FocusPreset(int Minutes, string Label);

/// <summary>Kronometre tur satırı.</summary>
public sealed record FocusLapRow(string Label, string Total, string Split);

/// <summary>
/// Odak sayfası ve çalışan zamanlayıcının özetleri (kapalı çentikteki kalan süre, Ana'daki "Odak · 18 dk kaldı").
/// Süre ve durum <see cref="FocusTimers"/> içindedir (görünümden bağımsız); bu sınıf yalnız ekran metnini üretir.
/// Boşta CPU: tazeleme zamanlayıcısı yalnız bir zamanlayıcı çalışırken ve çentik görünürken, saniyede bir kurulur.
/// </summary>
public sealed partial class FocusViewModel : ObservableObject, IDisposable
{
    private readonly FocusTimers _timers;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _displayTimer;
    private bool _panelVisible;
    private bool _summaryVisible;
    private bool _disposed;

    public ObservableCollection<FocusTabViewModel> Tabs { get; }

    /// <summary>Odak / Mola sekmelerinin sağ kartı: görev listesi.</summary>
    public TasksViewModel? Tasks { get; }

    public IReadOnlyList<FocusPreset> Presets { get; } =
        CountdownPresets.Minutes.Select(m => new FocusPreset(m, string.Format(CultureInfo.CurrentCulture, Strings.Get("TimerPresetMinutes"), m))).ToList();

    public ObservableCollection<FocusLapRow> Laps { get; } = [];

    public bool HasLaps => Laps.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPomodoroTab), nameof(IsCountdownTab), nameof(IsStopwatchTab), nameof(CanSkip))]
    private FocusKind _selected = FocusKind.Focus;

    public bool IsPomodoroTab => Selected is FocusKind.Focus or FocusKind.Break;
    public bool IsCountdownTab => Selected == FocusKind.Countdown;
    public bool IsStopwatchTab => Selected == FocusKind.Stopwatch;

    /// <summary>"Sonraki aşamaya geç" yalnız Pomodoro sekmelerinde anlamlı.</summary>
    public bool CanSkip => IsPomodoroTab;

    /// <summary>Halkanın ortasındaki büyük süre.</summary>
    [ObservableProperty]
    private string _timeText = "";

    /// <summary>Süre metninin altındaki küçük satır ("2 / 4 tur", "Geçen süre"…).</summary>
    [ObservableProperty]
    private string _subText = "";

    /// <summary>Halka doluluğu, 0-1.</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>Seçili sekmenin zamanlayıcısı çalışıyor.</summary>
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>Sayaç süresi yalnız çalışmıyorken değiştirilir.</summary>
    [ObservableProperty]
    private bool _canEditCountdown = true;

    [ObservableProperty]
    private string _sessionsText = "0";

    /// <summary>Sayacın ayarlı süresi ("05:00").</summary>
    [ObservableProperty]
    private string _countdownDurationText = "";

    public string DurationsText { get; }

    // --- Çalışan zamanlayıcının özeti ---

    /// <summary>Herhangi bir zamanlayıcı çalışıyor.</summary>
    [ObservableProperty]
    private bool _hasRunning;

    /// <summary>Kapalı çentikte pil ve bağlantı simgelerinin yerine yazılan süre ("18:24").</summary>
    [ObservableProperty]
    private string _runningText = "";

    /// <summary>Ana'daki satır ("Odak · 18 dk kaldı").</summary>
    [ObservableProperty]
    private string _homeLine = "";

    /// <summary>Pomodoro oturumu ya da sayaç bitti: çentik kısa süre Peek'e geçer ve titrer.</summary>
    public event Action? TimerFinished;

    public FocusViewModel(FocusTimers timers, Core.Settings.AppSettings settings, Dispatcher? dispatcher = null, TasksViewModel? tasks = null)
    {
        Tasks = tasks;
        _timers = timers;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

        DurationsText = string.Format(CultureInfo.CurrentCulture, 
            Strings.Get("FocusDurations"), settings.Focus.PomodoroWorkMinutes, settings.Focus.PomodoroShortBreakMinutes, settings.Focus.PomodoroLongBreakMinutes);

        Tabs =
        [
            new FocusTabViewModel(FocusKind.Focus, Strings.Get("FocusTabFocus")),
            new FocusTabViewModel(FocusKind.Break, Strings.Get("FocusTabBreak")),
            new FocusTabViewModel(FocusKind.Countdown, Strings.Get("FocusTabTimer")),
            new FocusTabViewModel(FocusKind.Stopwatch, Strings.Get("FocusTabStopwatch"))
        ];

        _displayTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
        _displayTimer.Tick += OnDisplayTick;

        _timers.Changed += OnTimersChanged;
        _timers.Finished += OnTimersFinished;

        Refresh(structural: true);
    }

    /// <param name="panelVisible">Odak sayfası açık çentikte görünüyor.</param>
    /// <param name="summaryVisible">Çentik görünür (kapalı çentikteki süre ve Ana satırı için).</param>
    public void SetViewing(bool panelVisible, bool summaryVisible)
    {
        _panelVisible = panelVisible;
        _summaryVisible = summaryVisible;
        if (panelVisible || summaryVisible)
            Refresh(structural: true);

        RestartDisplayTimer();
    }

    private void OnTimersChanged(object? sender, EventArgs e) => _dispatcher.InvokeAsync(() =>
    {
        if (_disposed)
            return;

        Refresh(structural: true);
        RestartDisplayTimer();
    });

    private void OnTimersFinished(object? sender, FocusKind kind) => _dispatcher.InvokeAsync(() =>
    {
        if (!_disposed)
            TimerFinished?.Invoke();
    });

    private void OnDisplayTick(object? sender, EventArgs e)
    {
        Refresh(structural: false);
        RestartDisplayTimer();
    }

    // Çalışan bir şey yoksa ya da hiçbir şey görünmüyorsa tik yok; çalışırken bir sonraki saniye başına tek atımlık.
    private void RestartDisplayTimer()
    {
        _displayTimer.Stop();
        if (_disposed || !(_panelVisible || _summaryVisible) || _timers.Running is null)
            return;

        _displayTimer.Interval = TimeSpan.FromMilliseconds(1000 - DateTime.Now.Millisecond + 20);
        _displayTimer.Start();
    }

    /// <param name="structural">Durum değişti (başlat, duraklat, kip, tur); yalnız saat ilerlediyse false.</param>
    private void Refresh(bool structural)
    {
        // Oturum bitip mola başlayınca ya da Pomodoro dışarıdan kip değiştirince Odak / Mola sekmesi onu izler.
        if (Selected is FocusKind.Focus or FocusKind.Break)
        {
            var follow = _timers.Pomodoro.Mode == PomodoroMode.Work ? FocusKind.Focus : FocusKind.Break;
            if (Selected != follow)
                Selected = follow;
        }

        foreach (var tab in Tabs)
            tab.IsSelected = tab.Kind == Selected;

        var reading = _timers.Read(Selected);
        TimeText = FormatReading(reading);
        IsRunning = _timers.IsRunning(Selected);
        CanEditCountdown = _timers.Countdown.State != CountdownState.Running;
        SubText = BuildSubText();
        Progress = BuildProgress(reading);
        SessionsText = _timers.Pomodoro.CompletedWorkSessions.ToString(CultureInfo.CurrentCulture);
        CountdownDurationText = DurationFormat.Countdown(_timers.Countdown.Duration);

        var running = _timers.Running;
        HasRunning = running is not null;
        RunningText = running is { } r ? FormatReading(r) : "";
        HomeLine = running is { } h ? BuildHomeLine(h) : "";

        if (structural)
            RefreshLaps();
    }

    private static string FormatReading(FocusReading reading) =>
        reading.Kind == FocusKind.Stopwatch
            ? DurationFormat.Stopwatch(reading.Value)[..^2]
            : DurationFormat.Countdown(reading.Value);

    private string BuildSubText()
    {
        var pomodoro = _timers.Pomodoro;
        return Selected switch
        {
            FocusKind.Focus => string.Format(CultureInfo.CurrentCulture, Strings.Get("FocusRound"), pomodoro.CurrentRound, pomodoro.Config.SessionsBeforeLongBreak),
            FocusKind.Break => pomodoro.Mode == PomodoroMode.LongBreak ? Strings.LongBreak : Strings.ShortBreak,
            FocusKind.Countdown => _timers.Countdown.State == CountdownState.Finished ? Strings.Get("FocusTimeUp") : Strings.Get("FocusCountdown"),
            _ => Strings.Get("FocusElapsed")
        };
    }

    private double BuildProgress(FocusReading reading)
    {
        switch (Selected)
        {
            case FocusKind.Focus or FocusKind.Break:
            {
                var total = _timers.Pomodoro.GetDurationForMode(_timers.Pomodoro.Mode).TotalSeconds;
                return total > 0 ? Math.Clamp(1 - reading.Value.TotalSeconds / total, 0, 1) : 0;
            }
            case FocusKind.Countdown:
            {
                var total = _timers.Countdown.Duration.TotalSeconds;
                return _timers.Countdown.State == CountdownState.Finished
                    ? 1
                    : total > 0 ? Math.Clamp(1 - reading.Value.TotalSeconds / total, 0, 1) : 0;
            }
            default:
                // Kronometre: halka dakikayı süpürür.
                return reading.Value.TotalSeconds % 60 / 60;
        }
    }

    private static string BuildHomeLine(FocusReading running)
    {
        var label = Strings.Get(running.Kind switch
        {
            FocusKind.Focus => "FocusTabFocus",
            FocusKind.Break => "FocusTabBreak",
            FocusKind.Countdown => "FocusTabTimer",
            _ => "FocusTabStopwatch"
        });

        var minutes = running.WholeMinutes;
        var amount = minutes > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.Get("HomeMinutes"), minutes)
            : running.CountsDown ? Strings.Get("HomeUnderMinute") : string.Format(CultureInfo.CurrentCulture, Strings.Get("HomeMinutes"), 0);

        return string.Format(CultureInfo.CurrentCulture, Strings.Get(running.CountsDown ? "HomeLeft" : "HomeElapsed"), label, amount);
    }

    private void RefreshLaps()
    {
        var laps = _timers.Stopwatch.Laps.Take(20).ToList();
        Laps.Clear();
        foreach (var lap in laps)
        {
            Laps.Add(new FocusLapRow(
                string.Format(CultureInfo.CurrentCulture, Strings.Get("TimerLapRow"), lap.Number),
                DurationFormat.Stopwatch(lap.Total),
                "+" + DurationFormat.Stopwatch(lap.Split)));
        }

        OnPropertyChanged(nameof(HasLaps));
    }

    // ---------------------------------------------------------------------------------------------

    [RelayCommand]
    private void SelectTab(FocusKind kind)
    {
        _timers.SelectPomodoroKind(kind);
        Selected = kind;
        Refresh(structural: true);
        RestartDisplayTimer();
    }

    [RelayCommand]
    private void Toggle() => _timers.Toggle(Selected);

    [RelayCommand]
    private void Reset() => _timers.Reset(Selected);

    [RelayCommand]
    private void Skip() => _timers.Pomodoro.Skip();

    [RelayCommand]
    private void Lap() => _timers.Stopwatch.Lap();

    [RelayCommand]
    private void IncreaseCountdown() =>
        _timers.Countdown.SetDuration(CountdownPresets.Step(_timers.Countdown.Duration, true));

    [RelayCommand]
    private void DecreaseCountdown() =>
        _timers.Countdown.SetDuration(CountdownPresets.Step(_timers.Countdown.Duration, false));

    [RelayCommand]
    private void SetCountdownMinutes(int minutes) =>
        _timers.Countdown.SetDuration(TimeSpan.FromMinutes(minutes));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _displayTimer.Stop();
        _displayTimer.Tick -= OnDisplayTick;
        _timers.Changed -= OnTimersChanged;
        _timers.Finished -= OnTimersFinished;
        Tasks?.Dispose();
    }
}
