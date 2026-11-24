using System.IO;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Koy.Core.Media;

namespace Koy.Platform.Media;

/// <summary>
/// Windows 11 Global System Media Transport Controls (GSMTC) servis uygulaması.
/// Spotify, Chrome, Edge ve diğer medya oynatıcılarını dinler ve yönetir.
/// </summary>
public sealed class MediaService : IMediaService
{
    private readonly object _sessionLock = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private string? _selectedSessionId;
    private bool _disposed;

    public event EventHandler<MediaInfo?>? Changed;
    public event EventHandler<PlaybackTimeline?>? TimelineChanged;
    public event EventHandler<IReadOnlyList<MediaSessionInfo>>? SessionsChanged;

    public MediaInfo? Current { get; private set; }
    public PlaybackTimeline? Timeline { get; private set; }
    public IReadOnlyList<MediaSessionInfo> Sessions { get; private set; } = [];

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_manager is null)
                return;

            _manager.CurrentSessionChanged += OnSessionManagerCurrentSessionChanged;
            _manager.SessionsChanged += OnSessionManagerSessionsChanged;

            await RefreshSessionListAsync();
            await BindToSessionAsync(_manager.GetCurrentSession());
        }
        catch
        {
            // Windows GSMTC kullanılamıyorsa uygulama çökmemeli.
        }
    }

    public void Stop() => Dispose();

    public async Task PlayPauseAsync()
    {
        var session = _currentSession ?? _manager?.GetCurrentSession();
        if (session is not null)
        {
            try { await session.TryTogglePlayPauseAsync(); }
            catch { /* Hata yutulur */ }
        }
    }

    public async Task NextAsync()
    {
        var session = _currentSession ?? _manager?.GetCurrentSession();
        if (session is not null)
        {
            try { await session.TrySkipNextAsync(); }
            catch { /* Hata yutulur */ }
        }
    }

    public async Task PreviousAsync()
    {
        var session = _currentSession ?? _manager?.GetCurrentSession();
        if (session is not null)
        {
            try { await session.TrySkipPreviousAsync(); }
            catch { /* Hata yutulur */ }
        }
    }

    public async Task SeekAsync(TimeSpan position)
    {
        var session = _currentSession ?? _manager?.GetCurrentSession();
        if (session is not null)
        {
            try
            {
                await session.TryChangePlaybackPositionAsync(position.Ticks);
            }
            catch { /* Hata yutulur */ }
        }
    }

    public async Task SelectSessionAsync(string sessionId)
    {
        if (_manager is null)
            return;

        // Halihazırda seçili oturuma tekrar tıklanırsa otomatik moda dön
        if (_selectedSessionId == sessionId)
        {
            _selectedSessionId = null;
            await BindToSessionAsync(_manager.GetCurrentSession());
            await RefreshSessionListAsync();
            return;
        }

        _selectedSessionId = sessionId;
        var all = _manager.GetSessions();
        var target = all.FirstOrDefault(s => s.SourceAppUserModelId == sessionId);
        await BindToSessionAsync(target);
        await RefreshSessionListAsync();
    }

    private async void OnSessionManagerCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        if (_disposed)
            return;

        try
        {
            if (_selectedSessionId is null)
            {
                await BindToSessionAsync(sender.GetCurrentSession());
                await RefreshSessionListAsync();
            }
        }
        catch
        {
            // Oturum değişimi sırasında WinRT nesnesi düşebilir; süreç çökmemeli.
        }
    }

    private async void OnSessionManagerSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        if (_disposed)
            return;

        try
        {
            await RefreshSessionListAsync();
        }
        catch
        {
            // Oturum listesi okunamazsa süreç çökmemeli.
        }
    }

    private async Task RefreshSessionListAsync()
    {
        GlobalSystemMediaTransportControlsSessionManager? manager;
        GlobalSystemMediaTransportControlsSession? currentSession;
        lock (_sessionLock)
        {
            if (_disposed)
                return;

            manager = _manager;
            currentSession = _currentSession;
        }

        if (manager is null)
        {
            Sessions = [];
            SessionsChanged?.Invoke(this, Sessions);
            return;
        }

        try
        {
            var rawSessions = manager.GetSessions();
            var list = new List<MediaSessionInfo>();
            var hasSelected = false;

            foreach (var s in rawSessions)
            {
                var appId = s.SourceAppUserModelId ?? string.Empty;
                var isCurrent = currentSession is not null && currentSession.SourceAppUserModelId == appId;
                var title = CleanSessionTitle(appId);
                list.Add(new MediaSessionInfo(appId, appId, title, isCurrent));

                if (_selectedSessionId is not null && appId == _selectedSessionId)
                    hasSelected = true;
            }

            // Seçilen oturum kapanmışsa otomatik takibe dön
            if (_selectedSessionId is not null && !hasSelected)
            {
                _selectedSessionId = null;
                await BindToSessionAsync(manager.GetCurrentSession());
            }

            Sessions = list;
            SessionsChanged?.Invoke(this, Sessions);
        }
        catch
        {
            // Oturum okuma başarısız olursa sessizce geç.
        }
    }

    private static string CleanSessionTitle(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return "Medya";

        var lower = appId.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("edge") || lower.Contains("msedge")) return "Edge";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("brave")) return "Brave";
        if (lower.Contains("opera")) return "Opera";
        if (lower.Contains("vlc")) return "VLC";
        if (lower.Contains("musicbee")) return "MusicBee";
        if (lower.Contains("foobar")) return "foobar2000";
        if (lower.Contains("apple") || lower.Contains("itunes")) return "Apple Music";
        if (lower.Contains("wmplayer") || lower.Contains("mediaplayer")) return "Media Player";

        var rawName = Path.GetFileNameWithoutExtension(appId.Split('!').Last());
        return string.IsNullOrWhiteSpace(rawName) ? "Medya" : rawName;
    }

    private async Task BindToSessionAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        bool sameSession;
        lock (_sessionLock)
        {
            if (_disposed)
                return;

            sameSession = ReferenceEquals(_currentSession, session);
            if (!sameSession)
            {
                if (_currentSession is not null)
                    DetachSessionEvents(_currentSession);

                _currentSession = session;

                if (session is not null && !TryAttachSessionEvents(session))
                {
                    _currentSession = null;
                    return;
                }
            }
        }

        if (session is not null)
        {
            await UpdateAllAsync();
        }
        else if (!sameSession)
        {
            Current = null;
            Timeline = null;
            Changed?.Invoke(this, null);
            TimelineChanged?.Invoke(this, null);
        }
    }

    private void DetachSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }
        catch
        {
            // Oturum zaten kapanmış olabilir.
        }
    }

    private bool TryAttachSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            return true;
        }
        catch
        {
            DetachSessionEvents(session);
            return false;
        }
    }

    private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        if (_disposed)
            return;

        try
        {
            await UpdateMediaPropertiesAsync();
        }
        catch
        {
            // Medya özellikleri alınırken oturum kapanabilir; süreç çökmemeli.
        }
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        if (_disposed)
            return;

        UpdatePlaybackInfo();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        if (_disposed)
            return;

        UpdateTimelineInfo();
    }

    private async Task UpdateAllAsync()
    {
        await UpdateMediaPropertiesAsync();
        UpdatePlaybackInfo();
        UpdateTimelineInfo();
    }

    private async Task UpdateMediaPropertiesAsync()
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_sessionLock)
        {
            if (_disposed)
                return;

            session = _currentSession;
        }

        if (session is null)
        {
            Current = null;
            Changed?.Invoke(this, null);
            return;
        }

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            byte[]? thumb = null;
            if (props?.Thumbnail is not null)
            {
                try
                {
                    var readTask = Task.Run(async () =>
                    {
                        using var streamRef = await props.Thumbnail.OpenReadAsync();
                        using var netStream = streamRef.AsStreamForRead();
                        using var ms = new MemoryStream();
                        await netStream.CopyToAsync(ms);
                        return ms.ToArray();
                    });

                    if (await Task.WhenAny(readTask, Task.Delay(2000)) == readTask)
                        thumb = await readTask;
                }
                catch
                {
                    thumb = null;
                }
            }

            Current = new MediaInfo(
                props?.Title ?? string.Empty,
                props?.Artist ?? string.Empty,
                session.SourceAppUserModelId ?? string.Empty,
                isPlaying,
                thumb);

            Changed?.Invoke(this, Current);
        }
        catch
        {
            // Okuma sırasında oturum kapanmış olabilir.
        }
    }

    private void UpdatePlaybackInfo()
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_sessionLock)
        {
            if (_disposed)
                return;

            session = _currentSession;
        }

        if (session is null || Current is null)
            return;

        try
        {
            var playback = session.GetPlaybackInfo();
            var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (Current.IsPlaying != isPlaying)
            {
                Current = Current with { IsPlaying = isPlaying };
                Changed?.Invoke(this, Current);
            }
        }
        catch
        {
            // Oturum geçersizse sessiz geç.
        }
    }

    private void UpdateTimelineInfo()
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_sessionLock)
        {
            if (_disposed)
                return;

            session = _currentSession;
        }

        if (session is null)
        {
            Timeline = null;
            TimelineChanged?.Invoke(this, null);
            return;
        }

        try
        {
            var timeline = session.GetTimelineProperties();
            if (timeline is not null)
            {
                Timeline = new PlaybackTimeline(
                    timeline.Position,
                    timeline.StartTime,
                    timeline.EndTime,
                    timeline.LastUpdatedTime);
                TimelineChanged?.Invoke(this, Timeline);
            }
        }
        catch
        {
            // Zaman çizelgesi okunamazsa sessiz geç.
        }
    }

    public void Dispose()
    {
        GlobalSystemMediaTransportControlsSession? session;
        GlobalSystemMediaTransportControlsSessionManager? manager;
        lock (_sessionLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            session = _currentSession;
            _currentSession = null;
            manager = _manager;
            _manager = null;
        }

        if (session is not null)
            DetachSessionEvents(session);

        if (manager is not null)
        {
            try
            {
                manager.CurrentSessionChanged -= OnSessionManagerCurrentSessionChanged;
                manager.SessionsChanged -= OnSessionManagerSessionsChanged;
            }
            catch
            {
                // Yönetici kapanmış olabilir.
            }
        }
    }
}
