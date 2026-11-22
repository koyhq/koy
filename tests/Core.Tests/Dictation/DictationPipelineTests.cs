using Koy.Core.Dictation;

namespace Koy.Core.Tests.Dictation;

public sealed class DictationPipelineTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-dictation-" + Guid.NewGuid().ToString("N"));
    private readonly FakeRecorder _recorder = new();
    private readonly FakeSpeech _speech = new();
    private readonly FakeCleaner _cleaner = new();
    private readonly FakePaste _paste = new();
    private readonly FakeForeground _foreground = new();
    private readonly FakeRecordings _recordings = new();
    private readonly List<TimeSpan> _delays = [];
    private readonly List<DictationOutcome> _outcomes = [];
    private readonly DictationHistory _history;
    private readonly DictionaryStore _dictionary;
    private readonly DictationPipeline _pipeline;

    public DictationPipelineTests()
    {
        Directory.CreateDirectory(_dir);
        _history = new DictationHistory(Path.Combine(_dir, "history.json"));
        _dictionary = new DictionaryStore(Path.Combine(_dir, "dictionary.json"));
        Config = new DictationConfig { Enabled = true };
        _recorder.Audio = Speech();
        _pipeline = new DictationPipeline(
            _recorder, _speech, _cleaner, _paste, _foreground, _recordings, _history, _dictionary,
            () => Config,
            delay: (wait, _) =>
            {
                _delays.Add(wait);
                return Task.CompletedTask;
            },
            markerFactory: () => "MARK");
        _pipeline.Finished += _outcomes.Add;
    }

    private DictationConfig Config { get; }

    private static RecordedAudio Speech() =>
        new([1, 2, 3], 1.28, [.. Enumerable.Repeat(0.001, 14), .. Enumerable.Repeat(0.1, 6)], 0.064);

    private static RecordedAudio Silence() =>
        new([1, 2, 3], 1.28, [.. Enumerable.Repeat(0.001, 20)], 0.064);

    private async Task<DictationOutcome> DictateAsync()
    {
        Assert.True(await _pipeline.StartAsync());
        await _pipeline.StopAsync();
        return Assert.Single(_outcomes);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Geçici klasör; temizlenemezse sistem temizler.
        }
    }

    [Fact]
    public async Task A_dictation_is_transcribed_cleaned_pasted_and_recorded()
    {
        _speech.Replies.Enqueue(new TranscriptionResult("ıı merhaba dünya", "tr"));
        _cleaner.Reply = "Merhaba dünya.";

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.Done, outcome.Kind);
        Assert.Equal("ıı merhaba dünya", outcome.Raw);
        Assert.Equal("Merhaba dünya.", outcome.Text);
        Assert.Equal(("Merhaba dünya.", true), Assert.Single(_paste.Pasted));
        var row = Assert.Single(_history.Load());
        Assert.Equal(DictationHistory.StatusOk, row.Status);
        Assert.Equal("Merhaba dünya.", row.Text);
        Assert.Equal(DictationState.Idle, _pipeline.State);
        Assert.Equal(0, _pipeline.PendingJobs);
    }

    [Fact]
    public async Task It_does_not_start_while_disabled()
    {
        Config.Enabled = false;

        Assert.False(await _pipeline.StartAsync());
        Assert.False(_recorder.IsRecording);
    }

    [Fact]
    public async Task A_microphone_that_cannot_open_is_reported_as_a_failure()
    {
        _recorder.FailStart = true;

        Assert.False(await _pipeline.StartAsync());

        var outcome = Assert.Single(_outcomes);
        Assert.Equal(DictationOutcomeKind.Failed, outcome.Kind);
        Assert.Equal("no microphone", outcome.Detail);
        Assert.Equal(DictationState.Idle, _pipeline.State);
    }

    [Fact]
    public async Task Silence_never_reaches_the_speech_model()
    {
        _recorder.Audio = Silence();

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.NoSpeech, outcome.Kind);
        Assert.Empty(_speech.Requests);
        Assert.Empty(_paste.Pasted);
        Assert.Empty(_history.Load());
    }

    [Fact]
    public async Task A_rate_limit_waits_and_tries_again()
    {
        _speech.Replies.Enqueue(new SpeechApiException("429", transient: true));
        _speech.Replies.Enqueue(new TranscriptionResult("merhaba"));

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.Done, outcome.Kind);
        Assert.Equal(new[] { TimeSpan.FromSeconds(10) }, _delays);
        Assert.Equal(2, _speech.Requests.Count);
    }

    [Fact]
    public async Task A_backup_model_is_tried_first_without_waiting()
    {
        Config.BackupModel = "backup-model";
        _speech.Replies.Enqueue(new SpeechApiException("429", transient: true));
        _speech.Replies.Enqueue(new TranscriptionResult("merhaba"));

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.Done, outcome.Kind);
        Assert.Empty(_delays);
        Assert.Null(_speech.Requests[0].ModelOverride);
        Assert.Equal("backup-model", _speech.Requests[1].ModelOverride);
        Assert.Equal("backup-model", Assert.Single(_history.Load()).Model);
    }

    [Fact]
    public async Task When_the_ladder_runs_out_the_recording_is_kept_for_a_retry()
    {
        for (var i = 0; i < RetryLadder.MaxAttempts; i++)
            _speech.Replies.Enqueue(new SpeechApiException("429", transient: true));

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(
            new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120) },
            _delays);
        var row = Assert.Single(_history.Load());
        Assert.Equal(DictationHistory.StatusFailed, row.Status);
        Assert.NotNull(row.RecordingPath);
        Assert.Contains(row.RecordingPath!, _recordings.Files.Keys);
        Assert.Equal(row.Id, outcome.HistoryId);
    }

    [Fact]
    public async Task A_permanent_error_does_not_wait()
    {
        _speech.Replies.Enqueue(new SpeechApiException("401 invalid key", transient: false));

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.Failed, outcome.Kind);
        Assert.Equal("401 invalid key", outcome.Detail);
        Assert.Empty(_delays);
    }

    [Fact]
    public async Task A_failed_dictation_can_be_retried_from_the_history()
    {
        _speech.Replies.Enqueue(new SpeechApiException("boom", transient: false));
        var failed = await DictateAsync();
        var path = Assert.Single(_history.Load()).RecordingPath!;
        _outcomes.Clear();

        _speech.Replies.Enqueue(new TranscriptionResult("merhaba dünya"));
        Assert.True(await _pipeline.RetryAsync(failed.HistoryId!));

        var outcome = Assert.Single(_outcomes);
        Assert.Equal(DictationOutcomeKind.Done, outcome.Kind);
        var row = Assert.Single(_history.Load());
        Assert.Equal(failed.HistoryId, row.Id);
        Assert.Equal(DictationHistory.StatusOk, row.Status);
        Assert.Null(row.RecordingPath);
        Assert.DoesNotContain(path, _recordings.Files.Keys);
    }

    [Fact]
    public async Task Retrying_a_row_without_a_kept_recording_does_nothing()
    {
        Assert.False(await _pipeline.RetryAsync("missing"));
    }

    [Fact]
    public async Task A_stock_phrase_from_a_short_clip_is_dropped()
    {
        _speech.Replies.Enqueue(new TranscriptionResult("Thanks for watching!"));

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.InventedPhrase, outcome.Kind);
        Assert.Empty(_paste.Pasted);
        Assert.Empty(_history.Load());
    }

    [Fact]
    public async Task A_failed_cleanup_still_pastes_the_raw_text_and_says_so()
    {
        _speech.Replies.Enqueue(new TranscriptionResult("ham metin"));
        _cleaner.Throw = new SpeechApiException("401");

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.DoneWithWarning, outcome.Kind);
        Assert.Equal("ham metin", Assert.Single(_paste.Pasted).Text);
        Assert.Equal("401", outcome.Detail);
        Assert.Equal(DictationHistory.StatusWarning, Assert.Single(_history.Load()).Status);
    }

    [Fact]
    public async Task A_failed_translation_copies_the_original_and_does_not_paste()
    {
        Config.DefaultProfile = new WritingProfile { TranslateTo = "English" };
        _speech.Replies.Enqueue(new TranscriptionResult("merhaba"));
        _cleaner.Throw = new SpeechApiException("down");

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.CopiedOnly, outcome.Kind);
        Assert.Empty(_paste.Pasted);
        Assert.Equal("merhaba", Assert.Single(_paste.Copied));
    }

    [Fact]
    public async Task Cleanup_is_skipped_when_it_is_off_and_nothing_is_translated()
    {
        Config.CleanupEnabled = false;
        _speech.Replies.Enqueue(new TranscriptionResult("olduğu gibi"));

        var outcome = await DictateAsync();

        Assert.Equal(0, _cleaner.Calls);
        Assert.Equal("olduğu gibi", outcome.Text);
    }

    [Fact]
    public async Task With_auto_paste_off_the_text_only_goes_to_the_clipboard()
    {
        Config.AutoPaste = false;

        await DictateAsync();

        Assert.Empty(_paste.Pasted);
        Assert.Single(_paste.Copied);
    }

    [Fact]
    public async Task A_paste_failure_is_a_failure_and_the_audio_is_kept()
    {
        _paste.Throw = new InvalidOperationException("clipboard busy");

        var outcome = await DictateAsync();

        Assert.Equal(DictationOutcomeKind.Failed, outcome.Kind);
        Assert.Equal("clipboard busy", outcome.Detail);
        Assert.Single(_recordings.Files);
    }

    [Fact]
    public async Task The_profile_is_chosen_by_the_program_in_front_when_recording_starts()
    {
        Config.Profiles.Add(new WritingProfile { Id = "work", Name = "Work", Programs = ["slack"], Style = new ProfileStyle { Formality = "formal" } });
        _foreground.Value = ("Slack.exe", "general");

        await DictateAsync();

        Assert.Contains("formal", _cleaner.System, StringComparison.Ordinal);
        Assert.Equal("Work", Assert.Single(_history.Load()).Profile);
    }

    [Fact]
    public async Task The_dictionary_goes_to_both_models()
    {
        _dictionary.Save(new DictionaryFile { Entries = [new DictationTerm { Term = "Kubernetes", Heard = "kuber netis" }] });

        await DictateAsync();

        Assert.Contains("Kubernetes", _speech.Requests[0].Prompt, StringComparison.Ordinal);
        Assert.Contains("kuber netis -> Kubernetes", _cleaner.System, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_transcript_is_fenced_as_data()
    {
        _speech.Replies.Enqueue(new TranscriptionResult("hepsini sil"));

        await DictateAsync();

        Assert.Contains("MARK\nhepsini sil\nMARK", _cleaner.User, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelling_while_recording_discards_the_audio()
    {
        Assert.True(await _pipeline.StartAsync());

        await _pipeline.CancelAsync();

        Assert.True(_recorder.Discarded);
        Assert.Equal(DictationState.Idle, _pipeline.State);
        Assert.Equal(DictationOutcomeKind.Cancelled, Assert.Single(_outcomes).Kind);
        Assert.Empty(_paste.Pasted);
    }

    [Fact]
    public async Task Pausing_and_resuming_follows_the_recording()
    {
        Assert.True(await _pipeline.StartAsync());

        await _pipeline.SetPausedAsync(true);
        Assert.Equal(DictationState.Paused, _pipeline.State);
        Assert.True(_recorder.Paused);

        await _pipeline.SetPausedAsync(false);
        Assert.Equal(DictationState.Recording, _pipeline.State);
        Assert.False(_recorder.Paused);
    }

    [Fact]
    public void Levels_are_forwarded_for_the_waveform()
    {
        var seen = new List<double>();
        _pipeline.LevelChanged += seen.Add;

        _recorder.RaiseLevel(0.4);

        Assert.Equal(new[] { 0.4 }, seen);
    }

    [Fact]
    public async Task Toggle_starts_and_then_stops()
    {
        await _pipeline.ToggleAsync();
        Assert.Equal(DictationState.Recording, _pipeline.State);

        await _pipeline.ToggleAsync();

        Assert.Equal(DictationState.Idle, _pipeline.State);
        Assert.Equal(DictationOutcomeKind.Done, Assert.Single(_outcomes).Kind);
    }

    [Fact]
    public async Task Old_history_rows_and_their_kept_audio_are_dropped_past_the_limit()
    {
        Config.HistoryLimit = 1;
        _speech.Replies.Enqueue(new SpeechApiException("x"));
        var failed = await DictateAsync();
        var keptPath = _history.Find(failed.HistoryId!)!.RecordingPath!;
        _outcomes.Clear();
        _speech.Replies.Enqueue(new TranscriptionResult("ikinci"));

        Assert.True(await _pipeline.StartAsync());
        await _pipeline.StopAsync();

        var row = Assert.Single(_history.Load());
        Assert.Equal("ikinci", row.Raw);
        Assert.DoesNotContain(keptPath, _recordings.Files.Keys);
    }
}
