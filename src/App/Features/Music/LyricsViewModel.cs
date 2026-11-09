using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.Core.Lyrics;
using Koy.Core.Media;

namespace Koy.App.Features.Music;

public sealed partial class LyricLineItem(LyricLine line) : ObservableObject
{
    public TimeSpan Time { get; } = line.Time;
    public string Text { get; } = line.Text.Length == 0 ? "♪" : line.Text;

    [ObservableProperty]
    private bool _isActive;
}

/// <summary>
/// Ana sayfadaki müzik kartının sözleri. Yükleme ve zamanlayıcı yalnız Ana sayfa görünürken çalışır (AGENTS #5).
/// Zamanlayıcı saniyede bir yoklamaz, bir sonraki satırın zamanına kurulur.
/// </summary>
public sealed partial class LyricsViewModel : ObservableObject, IDisposable
{
    // Oynatıcı parça bilgisini parça parça bildirir; hepsi gelmeden aramak yanlış sorgu olurdu.
    private static readonly TimeSpan LoadDelay = TimeSpan.FromMilliseconds(350);

    // Parça değişiminden bu kadar eski zaman çizelgesi önceki parçanındır, süresi sorguya katılmaz.
    private static readonly TimeSpan FreshTimelineWindow = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan MinLineDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan SeekSettle = TimeSpan.FromSeconds(1.5);

    private readonly IMediaService _media;
    private readonly Dispatcher _dispatcher;
    private readonly LyricsSession? _session;
    private readonly DispatcherTimer _loadTimer;
    private readonly DispatcherTimer _lineTimer;

    private bool _panelActive;
    private bool _running;
    private bool _disposed;
    private string? _trackIdentity;
    private DateTimeOffset _trackChangedAt = DateTimeOffset.MinValue;
    private LyricLineItem? _activeItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    private bool _isAvailable;

    [ObservableProperty]
    private IReadOnlyList<LyricLineItem> _lines = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    private LyricsState _state;

    [ObservableProperty]
    private LyricLineItem? _activeLine;

    /// <summary>Kartta sözler mi var. Yüklenirken ya da söz yoksa kartta ses çubukları kalır.</summary>
    public bool IsShown => IsAvailable && State == LyricsState.Ready;

    public LyricsViewModel(IMediaService media, ILyricsProvider? provider, Dispatcher dispatcher)
    {
        _media = media;
        _dispatcher = dispatcher;
        _loadTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = LoadDelay };
        _loadTimer.Tick += OnLoadTick;
        _lineTimer = new DispatcherTimer(DispatcherPriority.Render, dispatcher);
        _lineTimer.Tick += OnLineTick;

        if (provider is not null)
        {
            _session = new LyricsSession(provider);
            _session.Changed += OnSessionChanged;
        }
    }

    /// <summary>Çentik ayarındaki "şarkı sözleri" seçeneği.</summary>
    public void SetAllowed(bool allowed)
    {
        IsAvailable = allowed && _session is not null;
        UpdateRunning();
    }

    /// <summary>Çentik açık ve Ana sekmesi görünür mü.</summary>
    public void SetPanelActive(bool active)
    {
        _panelActive = active;
        UpdateRunning();
    }

    [RelayCommand]
    private async Task SeekToLine(LyricLineItem? item)
    {
        if (item is null || _session is null)
            return;

        _session.UpdatePosition(item.Time);
        ApplyActiveLine();

        // Oynatıcının yeni konumu bildirmesi gecikebilir: eski konumla vurgu geri sıçramasın, bildirim hiç gelmezse de akış dursun.
        _lineTimer.Stop();
        _lineTimer.Interval = SeekSettle;
        _lineTimer.Start();

        await _media.SeekAsync((_media.Timeline?.StartTime ?? TimeSpan.Zero) + item.Time);
    }

    private void UpdateRunning()
    {
        var want = !_disposed && IsAvailable && _panelActive;
        if (want == _running)
            return;

        _running = want;
        if (want)
        {
            _media.Changed += OnMediaChanged;
            _media.TimelineChanged += OnTimelineChanged;
            _trackIdentity = Identity(_media.Current);
            _trackChangedAt = DateTimeOffset.MinValue;
            _ = _session!.LoadAsync(BuildQuery());
            Sync();
        }
        else
        {
            _media.Changed -= OnMediaChanged;
            _media.TimelineChanged -= OnTimelineChanged;
            _loadTimer.Stop();
            _lineTimer.Stop();
            _session?.Suspend();
        }
    }

    private void OnMediaChanged(object? sender, MediaInfo? info) => _dispatcher.InvokeAsync(() =>
    {
        if (!_running)
            return;

        var identity = Identity(info);
        if (identity != _trackIdentity)
        {
            _trackIdentity = identity;
            _trackChangedAt = DateTimeOffset.Now;
            _lineTimer.Stop();
            _loadTimer.Stop();
            _loadTimer.Start();
            return;
        }

        Sync();
    });

    private void OnTimelineChanged(object? sender, PlaybackTimeline? timeline) => _dispatcher.InvokeAsync(() =>
    {
        // Parça yeni değiştiyse konum eski sözlere uygulanmasın.
        if (_running && !_loadTimer.IsEnabled)
            Sync();
    });

    private void OnLoadTick(object? sender, EventArgs e)
    {
        _loadTimer.Stop();
        if (_running)
            _ = _session!.LoadAsync(BuildQuery());
    }

    private void OnLineTick(object? sender, EventArgs e)
    {
        _lineTimer.Stop();
        Sync();
    }

    private void OnSessionChanged()
    {
        _activeItem = null;
        ActiveLine = null;
        Lines = _session!.Lines.Select(line => new LyricLineItem(line)).ToArray();
        State = _session.State;
        Sync();
    }

    private LyricsQuery? BuildQuery()
    {
        if (_media.Current is not { } info)
            return null;

        var timeline = _media.Timeline;
        var duration = timeline is not null && _trackChangedAt - timeline.LastUpdated <= FreshTimelineWindow
            ? PlaybackClock.Duration(timeline)
            : TimeSpan.Zero;

        return LyricsQuery.Create(info.Title, info.Artist, duration);
    }

    private void Sync()
    {
        _lineTimer.Stop();
        if (!_running || _session is not { State: LyricsState.Ready } session || _media.Timeline is not { } timeline)
            return;

        var isPlaying = _media.Current?.IsPlaying == true;
        var position = PlaybackClock.PositionAt(timeline, isPlaying, DateTimeOffset.Now);
        if (session.UpdatePosition(position))
            ApplyActiveLine();

        if (!isPlaying || LyricsTimeline.UntilNextChange(session.Lines, position) is not { } delay)
            return;

        _lineTimer.Interval = delay < MinLineDelay ? MinLineDelay : delay;
        _lineTimer.Start();
    }

    private void ApplyActiveLine()
    {
        var index = _session!.ActiveIndex;
        var next = index >= 0 && index < Lines.Count ? Lines[index] : null;
        if (ReferenceEquals(next, _activeItem))
            return;

        if (_activeItem is not null)
            _activeItem.IsActive = false;
        if (next is not null)
            next.IsActive = true;

        _activeItem = next;
        ActiveLine = next;
    }

    private static string? Identity(MediaInfo? info) => info is null ? null : $"{info.Title}\n{info.Artist}";

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        UpdateRunning();
        _loadTimer.Tick -= OnLoadTick;
        _lineTimer.Tick -= OnLineTick;
        if (_session is not null)
            _session.Changed -= OnSessionChanged;
    }
}
