using Koy.Core.Lyrics;

namespace Koy.Core.Tests;

public sealed class LyricsSessionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-lyrics-" + Guid.NewGuid().ToString("N"));

    private const string Lrc = "[00:01.00]line";
    private static readonly LyricsQuery A = new("A", "Artist", null);
    private static readonly LyricsQuery B = new("B", "Artist", null);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private sealed class FakeSource(params LyricsLookup[] results) : ILyricsSource
    {
        private int _next;
        public int Calls { get; private set; }

        public Task<LyricsLookup> FindAsync(LyricsQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(results[Math.Min(_next++, results.Length - 1)]);
        }
    }

    private sealed class ManualProvider : ILyricsProvider
    {
        public Dictionary<string, TaskCompletionSource<LyricsLoadResult>> Pending { get; } = [];
        public int Calls { get; private set; }

        public TaskCompletionSource<LyricsLoadResult> For(LyricsQuery query)
        {
            lock (Pending)
            {
                if (!Pending.TryGetValue(query.Key, out var source))
                    Pending[query.Key] = source = new TaskCompletionSource<LyricsLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                return source;
            }
        }

        public Task<LyricsLoadResult> LoadAsync(LyricsQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return For(query).Task;
        }
    }

    private static LyricsLoadResult Found(string text) =>
        new(LyricsLoadStatus.Found, [new LyricLine(TimeSpan.FromSeconds(1), text)]);

    private LyricsProvider Provider(ILyricsSource local, ILyricsSource online) => new(local, new LyricsCache(_dir), online);

    [Fact]
    public async Task Provider_prefers_local_and_stays_offline()
    {
        var online = new FakeSource(LyricsLookup.Found(Lrc));

        var result = await Provider(new FakeSource(LyricsLookup.Found(Lrc)), online).LoadAsync(A, default);

        Assert.Equal(LyricsLoadStatus.Found, result.Status);
        Assert.Equal(0, online.Calls);
    }

    [Fact]
    public async Task Provider_downloads_once_then_uses_cache()
    {
        var online = new FakeSource(LyricsLookup.Found(Lrc));
        var provider = Provider(new FakeSource(LyricsLookup.NotFound), online);

        await provider.LoadAsync(A, default);
        var second = await provider.LoadAsync(A, default);

        Assert.Equal(LyricsLoadStatus.Found, second.Status);
        Assert.Equal(1, online.Calls);
    }

    [Fact]
    public async Task Provider_remembers_tracks_without_lyrics()
    {
        var online = new FakeSource(LyricsLookup.NotFound);
        var provider = Provider(new FakeSource(LyricsLookup.NotFound), online);

        await provider.LoadAsync(A, default);
        var second = await provider.LoadAsync(A, default);

        Assert.Equal(LyricsLoadStatus.NotFound, second.Status);
        Assert.Equal(1, online.Calls);
    }

    [Fact]
    public async Task Provider_does_not_cache_temporary_failures()
    {
        var online = new FakeSource(LyricsLookup.Failed, LyricsLookup.Found(Lrc));
        var provider = Provider(new FakeSource(LyricsLookup.NotFound), online);

        Assert.Equal(LyricsLoadStatus.Failed, (await provider.LoadAsync(A, default)).Status);
        Assert.Equal(LyricsLoadStatus.Found, (await provider.LoadAsync(A, default)).Status);
    }

    [Fact]
    public async Task Provider_treats_unusable_lrc_as_not_found()
    {
        var provider = Provider(new FakeSource(LyricsLookup.NotFound), new FakeSource(LyricsLookup.Found("no stamps")));

        Assert.Equal(LyricsLoadStatus.NotFound, (await provider.LoadAsync(A, default)).Status);
    }

    [Fact]
    public async Task Session_goes_from_loading_to_ready()
    {
        var provider = new ManualProvider();
        var session = new LyricsSession(provider);

        var load = session.LoadAsync(A);
        Assert.Equal(LyricsState.Loading, session.State);

        provider.For(A).SetResult(Found("a"));
        await load;

        Assert.Equal(LyricsState.Ready, session.State);
        Assert.Equal("a", Assert.Single(session.Lines).Text);
    }

    [Fact]
    public async Task Late_result_of_previous_track_is_ignored()
    {
        var provider = new ManualProvider();
        var session = new LyricsSession(provider);

        var first = session.LoadAsync(A);
        var second = session.LoadAsync(B);

        provider.For(B).SetResult(Found("b"));
        await second;
        provider.For(A).SetResult(Found("a"));
        await first;

        Assert.Equal("b", Assert.Single(session.Lines).Text);
        Assert.Equal(LyricsState.Ready, session.State);
    }

    [Fact]
    public async Task Same_track_is_not_loaded_twice()
    {
        var provider = new ManualProvider();
        var session = new LyricsSession(provider);
        provider.For(A).SetResult(Found("a"));

        await session.LoadAsync(A);
        await session.LoadAsync(A with { Duration = TimeSpan.FromMinutes(3) });

        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task Failed_track_is_retried()
    {
        var provider = new ManualProvider();
        var session = new LyricsSession(provider);
        provider.For(A).SetResult(LyricsLoadResult.Failed);
        await session.LoadAsync(A);
        Assert.Equal(LyricsState.Failed, session.State);

        provider.Pending.Clear();
        provider.For(A).SetResult(Found("a"));
        await session.LoadAsync(A);

        Assert.Equal(LyricsState.Ready, session.State);
    }

    [Fact]
    public async Task Provider_exception_becomes_failed_state()
    {
        var provider = new ManualProvider();
        Exception? logged = null;
        var session = new LyricsSession(provider, (_, ex) => logged = ex);
        var load = session.LoadAsync(A);

        provider.For(A).SetException(new InvalidOperationException("boom"));
        await load;

        Assert.Equal(LyricsState.Failed, session.State);
        Assert.IsType<InvalidOperationException>(logged);
    }

    [Fact]
    public async Task Suspend_cancels_loading_but_keeps_finished_lyrics()
    {
        var provider = new ManualProvider();
        var session = new LyricsSession(provider);

        _ = session.LoadAsync(A);
        session.Suspend();
        Assert.Equal(LyricsState.Idle, session.State);

        provider.For(B).SetResult(Found("b"));
        await session.LoadAsync(B);
        session.Suspend();

        Assert.Equal(LyricsState.Ready, session.State);
    }

    [Fact]
    public async Task Null_query_clears()
    {
        var provider = new ManualProvider();
        var session = new LyricsSession(provider);
        provider.For(A).SetResult(Found("a"));
        await session.LoadAsync(A);

        await session.LoadAsync(null);

        Assert.Equal(LyricsState.Idle, session.State);
        Assert.Empty(session.Lines);
    }

    [Fact]
    public async Task Update_position_reports_only_real_changes()
    {
        var provider = new ManualProvider();
        var session = new LyricsSession(provider);
        provider.For(A).SetResult(Found("a"));
        await session.LoadAsync(A);

        Assert.True(session.UpdatePosition(TimeSpan.FromSeconds(2)));
        Assert.False(session.UpdatePosition(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, session.ActiveIndex);
    }
}
