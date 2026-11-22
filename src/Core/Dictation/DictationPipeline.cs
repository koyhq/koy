using System.Diagnostics;
using System.Globalization;
using Koy.Core.Voice;

namespace Koy.Core.Dictation;

/// <summary>
/// Sesle yazma hattı: kayıt → sessizlik denetimi → konuşmadan metne (yeniden deneme + yedek model) → uydurma cümle süzgeci →
/// temizleme/çeviri → pano + yapıştır → geçmiş + sözlük öğrenmesi.
/// <para>Konuşmak öncekinin işlenmesini beklemez: yeni kayıt başlatılabilir, işler sırayla bitirilir ve yapıştırılır.
/// Yapıştırma (panoyu oku, bizimkini koy, tuşa bas, eskisini geri koy) bölünmez: tek kilit.</para>
/// <para>Olaylar kayıt ya da iş iş parçacığından gelir; arayüz kendi iş parçacığına geçirir.</para>
/// </summary>
public sealed class DictationPipeline : IDisposable
{
    private readonly IAudioRecorder _recorder;
    private readonly ISpeechToText _speech;
    private readonly ITextCleaner _cleaner;
    private readonly IPasteService _paste;
    private readonly IForegroundProgram _foreground;
    private readonly IRecordingStore _recordings;
    private readonly DictationHistory _history;
    private readonly DictionaryStore _dictionary;
    private readonly Func<DictationConfig> _config;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<string> _marker;

    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _jobGate = new(1, 1);
    private readonly SemaphoreSlim _pasteGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HashSet<string> _retrying = new(StringComparer.Ordinal);

    private CancellationTokenSource? _limit;
    private CancellationTokenSource? _jobCancel;
    private ProfileChoice _choice = ProfileChoice.Default();
    private int _pending;
    private bool _disposed;

    public DictationPipeline(
        IAudioRecorder recorder,
        ISpeechToText speech,
        ITextCleaner cleaner,
        IPasteService paste,
        IForegroundProgram foreground,
        IRecordingStore recordings,
        DictationHistory history,
        DictionaryStore dictionary,
        Func<DictationConfig> config,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<string>? markerFactory = null)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _cleaner = cleaner ?? throw new ArgumentNullException(nameof(cleaner));
        _paste = paste ?? throw new ArgumentNullException(nameof(paste));
        _foreground = foreground ?? throw new ArgumentNullException(nameof(foreground));
        _recordings = recordings ?? throw new ArgumentNullException(nameof(recordings));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _delay = delay ?? Task.Delay;
        _marker = markerFactory ?? (() => "KOY-" + Guid.NewGuid().ToString("N")[..12]);
        _recorder.LevelChanged += OnLevel;
    }

    /// <summary><see cref="DictationOutcome.Detail"/> değeri: kayıt işlenemeyecek kadar kısa.</summary>
    public const string TooShort = "short";

    public DictationState State { get; private set; } = DictationState.Idle;

    /// <summary>Sırada ya da işlenmekte olan iş sayısı.</summary>
    public int PendingJobs => Volatile.Read(ref _pending);

    public event Action<DictationState>? StateChanged;
    public event Action<DictationStage>? StageChanged;
    public event Action<DictationOutcome>? Finished;
    public event Action<double>? LevelChanged;

    /// <summary>Boştaysa kaydı başlatır, kayıt sürüyorsa bitirip işi başlatır.</summary>
    public async Task ToggleAsync()
    {
        DictationState state;
        lock (_gate)
            state = State;

        if (state == DictationState.Idle)
            await StartAsync().ConfigureAwait(false);
        else
            await StopAsync().ConfigureAwait(false);
    }

    /// <summary>Kaydı başlatır. Kapalıysa ya da mikrofon açılamazsa false (ikincisinde <see cref="Finished"/> hatayı bildirir).</summary>
    public async Task<bool> StartAsync()
    {
        var config = _config();
        lock (_gate)
        {
            if (_disposed || State != DictationState.Idle || !config.Enabled)
                return false;

            // Profil kayıt başladığı an alınır: kuyruktaki iş sonradan değişen ayarlardan ve öne gelen pencereden etkilenmez.
            _choice = ChooseProfile(config);

            // Yer önceden ayrılır: mikrofon açılırken ikinci bir başlatma araya giremesin.
            State = DictationState.Recording;
        }

        try
        {
            await _recorder.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            lock (_gate)
                State = DictationState.Idle;
            Raise(Finished, new DictationOutcome(DictationOutcomeKind.Failed, Detail: ex.Message));
            return false;
        }

        CancellationTokenSource limit;
        lock (_gate)
        {
            _limit?.Cancel();
            _limit?.Dispose();
            limit = _limit = new CancellationTokenSource();
        }

        Raise(StateChanged, DictationState.Recording);
        _ = StopAfterLimitAsync(TimeSpan.FromSeconds(config.MaxSeconds), limit.Token);
        return true;
    }

    /// <summary>Kaydı bitirir ve işi başlatır; iş bitince tamamlanır. Kayıt yoksa hemen tamamlanır.</summary>
    public async Task StopAsync()
    {
        ProfileChoice choice;
        lock (_gate)
        {
            if (State == DictationState.Idle)
                return;

            State = DictationState.Idle;
            _limit?.Cancel();
            choice = _choice;
        }

        RecordedAudio? audio;
        try
        {
            audio = await _recorder.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            Raise(StateChanged, DictationState.Idle);
            Raise(Finished, new DictationOutcome(DictationOutcomeKind.Failed, Detail: ex.Message));
            return;
        }

        Raise(StateChanged, DictationState.Idle);
        if (audio is null)
        {
            // Bir sözcük bile olmayacak kadar kısa: iş açılmaz, ama kullanıcı sessizce yutulmuş sanmasın.
            Raise(Finished, new DictationOutcome(DictationOutcomeKind.NoSpeech, Detail: TooShort));
            return;
        }

        Interlocked.Increment(ref _pending);
        await RunJobAsync(audio.Wav, audio.Seconds, audio.RmsLevels, audio.ChunkSeconds, choice, retry: null).ConfigureAwait(false);
    }

    /// <summary>Kaydı duraklatır (true) ya da sürdürür (false).</summary>
    public async Task SetPausedAsync(bool paused)
    {
        lock (_gate)
        {
            if (State is DictationState.Idle || (paused && State == DictationState.Paused) || (!paused && State == DictationState.Recording))
                return;

            State = paused ? DictationState.Paused : DictationState.Recording;
        }

        try
        {
            await _recorder.SetPausedAsync(paused).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            Raise(Finished, new DictationOutcome(DictationOutcomeKind.Failed, Detail: ex.Message));
            return;
        }

        Raise(StateChanged, paused ? DictationState.Paused : DictationState.Recording);
    }

    /// <summary>Kayıt sürüyorsa atar; değilse çalışan işi iptal eder (sıradakiler sürer).</summary>
    public async Task CancelAsync()
    {
        bool wasRecording;
        lock (_gate)
        {
            wasRecording = State != DictationState.Idle;
            if (wasRecording)
            {
                State = DictationState.Idle;
                _limit?.Cancel();
            }
        }

        if (wasRecording)
        {
            try
            {
                await _recorder.DiscardAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
            {
                Koy.Core.Diagnostics.CrashLog.Write("DictationPipeline.Discard", ex);
            }

            Raise(StateChanged, DictationState.Idle);
            Raise(Finished, new DictationOutcome(DictationOutcomeKind.Cancelled));
            return;
        }

        CancellationTokenSource? job;
        lock (_gate)
            job = _jobCancel;
        job?.Cancel();
    }

    /// <summary>Başarısız bir yazının saklanan sesini yeniden işler. Ses yoksa ya da aynı satır zaten deneniyorsa false.</summary>
    public async Task<bool> RetryAsync(string historyId)
    {
        var entry = _history.Find(historyId);
        if (entry?.RecordingPath is null)
            return false;

        lock (_gate)
        {
            if (_disposed || !_retrying.Add(historyId))
                return false;
        }

        var wav = _recordings.Load(entry.RecordingPath);
        if (wav is null)
        {
            lock (_gate)
                _retrying.Remove(historyId);
            return false;
        }

        // Kaydın profili geçmişte ad olarak tutulur; ayar sonradan değiştiyse güncel profil aranır, yoksa varsayılan.
        var config = _config();
        var choice = ProfileSelector.ByName(config, entry.Profile) is { } profile ? new ProfileChoice(profile, false) : ProfileChoice.Default(config.DefaultProfile);

        Interlocked.Increment(ref _pending);
        try
        {
            await RunJobAsync(wav, entry.DurationSeconds, null, 0, choice, entry).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
                _retrying.Remove(historyId);
        }

        return true;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _recorder.LevelChanged -= OnLevel;
        _limit?.Cancel();
        _shutdown.Cancel();
        if (_recorder.IsRecording)
            _ = DiscardQuietlyAsync();
        _limit?.Dispose();
        _shutdown.Dispose();
        _jobGate.Dispose();
        _pasteGate.Dispose();
    }

    private ProfileChoice ChooseProfile(DictationConfig config)
    {
        try
        {
            var (program, title) = _foreground.Current();
            return ProfileSelector.Choose(config, program, title);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return ProfileChoice.Default(config.DefaultProfile);
        }
    }

    private void OnLevel(double level) => Raise(LevelChanged, level);

    private async Task DiscardQuietlyAsync()
    {
        try
        {
            await _recorder.DiscardAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Koy.Core.Diagnostics.CrashLog.Write("DictationPipeline.Dispose", ex);
        }
    }

    private async Task StopAfterLimitAsync(TimeSpan after, CancellationToken token)
    {
        try
        {
            await Task.Delay(after, token).ConfigureAwait(false);
            await StopAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Kayıt süresinden önce kullanıcı bitirdi ya da iptal etti.
        }
    }

    private async Task RunJobAsync(
        byte[] wav, double seconds, IReadOnlyList<double>? levels, double chunkSeconds, ProfileChoice choice, DictationHistoryEntry? retry)
    {
        DictationOutcome outcome;
        var acquired = false;
        try
        {
            await _jobGate.WaitAsync(_shutdown.Token).ConfigureAwait(false);
            acquired = true;

            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            lock (_gate)
                _jobCancel = cancel;

            try
            {
                outcome = await ProcessAsync(wav, seconds, levels, chunkSeconds, choice, retry, cancel.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                outcome = new DictationOutcome(DictationOutcomeKind.Cancelled);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                outcome = new DictationOutcome(DictationOutcomeKind.Failed, Detail: ex.Message);
            }
            finally
            {
                lock (_gate)
                    _jobCancel = null;
            }
        }
        catch (OperationCanceledException)
        {
            outcome = new DictationOutcome(DictationOutcomeKind.Cancelled);
        }
        catch (ObjectDisposedException)
        {
            outcome = new DictationOutcome(DictationOutcomeKind.Cancelled);
        }
        finally
        {
            if (acquired)
                _jobGate.Release();
            Interlocked.Decrement(ref _pending);
        }

        Raise(Finished, outcome);
    }

    private async Task<DictationOutcome> ProcessAsync(
        byte[] wav, double seconds, IReadOnlyList<double>? levels, double chunkSeconds,
        ProfileChoice choice, DictationHistoryEntry? retry, CancellationToken ct)
    {
        var config = _config();
        var clock = Stopwatch.StartNew();

        // Oda sesi: ne bir istek harca ne de uydurma bir cümleyi davet et. Yeniden denemede düzey yoktur; ses ilk seferde geçmişti.
        if (config.SkipSilent && retry is null && levels is { Count: > 0 })
        {
            var stats = SpeechGate.Analyse(levels, chunkSeconds, config.SpeechMarginDb);
            if (SpeechGate.IsSilent(stats, config.SilenceDb, config.MinVoicedSeconds))
            {
                return new DictationOutcome(DictationOutcomeKind.NoSpeech,
                    Detail: Math.Round(stats.SpeechDb).ToString(CultureInfo.InvariantCulture));
            }
        }

        var dictionary = _dictionary.Load();
        var request = new TranscriptionRequest(config.Language, DictionaryText.Hint(dictionary.Entries));

        Raise(StageChanged, DictationStage.Transcribing);
        TranscriptionResult heard;
        var model = config.TranscribeModel;
        try
        {
            (heard, model) = await TranscribeWithRetryAsync(wav, request, config, ct).ConfigureAwait(false);
        }
        catch (SpeechApiException ex)
        {
            return Fail(config, wav, seconds, retry, choice, model, ex.Message);
        }

        var raw = heard.Text.Trim();
        if (raw.Length == 0)
            return new DictationOutcome(DictationOutcomeKind.NoSpeech, Detail: "empty", Duration: clock.Elapsed);

        if (config.FilterHallucinations && HallucinationFilter.LooksInvented(raw, seconds))
        {
            // Elle istenen yeniden denemede kayıt kullanıcının geri istediği şeydir: kalır, satır sebebi söyler.
            return retry is not null
                ? Fail(config, wav, seconds, retry, choice, model, raw)
                : new DictationOutcome(DictationOutcomeKind.InventedPhrase, Raw: raw, Detail: raw, Duration: clock.Elapsed);
        }

        var profile = choice.Profile;
        var translating = config.TranslationEnabled && !string.IsNullOrWhiteSpace(profile.TranslateTo);
        var text = raw;
        var warning = "";
        var translationFailed = false;

        if (config.CleanupEnabled || translating)
        {
            Raise(StageChanged, DictationStage.CleaningUp);
            try
            {
                var system = CleanupPrompts.Build(config, heard.DetectedLanguage, profile, translating, DictionaryText.Glossary(dictionary.Entries));
                var reply = await _cleaner.CompleteAsync(system, CleanupPrompts.Fence(raw, _marker), ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(reply))
                    text = reply.Trim();
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
            {
                // Ham metin yine de yazılır: söylenen kaybolmaz. Ama hata sessiz geçmez (anahtar reddedildiyse çalışıyor gibi görünürdü).
                text = raw;
                warning = ex.Message;
                translationFailed = translating;
            }
        }

        Raise(StageChanged, DictationStage.Pasting);
        try
        {
            await _pasteGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Çeviri istenmişse ve olmadıysa özgün metin yalnız panoya gider: yanlış dilde sonuç doğru sonuç gibi görünmesin.
                if (config.AutoPaste && !translationFailed)
                    await _paste.PasteAsync(text, config.RestoreClipboard, ct).ConfigureAwait(false);
                else
                    await _paste.CopyAsync(text, ct).ConfigureAwait(false);
            }
            finally
            {
                _pasteGate.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            return Fail(config, wav, seconds, retry, choice, model, ex.Message);
        }

        var historyId = retry?.Id ?? NewId();
        var status = warning.Length == 0 ? DictationHistory.StatusOk : DictationHistory.StatusWarning;
        Record(config, new DictationHistoryEntry(
            historyId, DateTimeOffset.UtcNow, raw, text, heard.DetectedLanguage, model, profile.Name, seconds, status, warning, null));
        if (retry?.RecordingPath is { } kept)
            _recordings.Delete(kept);

        if (config.DictionaryLearning && warning.Length == 0)
            _dictionary.Update(file => DictionaryLearner.Learn(file, text));

        var kind = translationFailed ? DictationOutcomeKind.CopiedOnly
            : warning.Length > 0 ? DictationOutcomeKind.DoneWithWarning
            : DictationOutcomeKind.Done;
        return new DictationOutcome(kind, raw, text, warning, heard.DetectedLanguage, clock.Elapsed, historyId);
    }

    private async Task<(TranscriptionResult Result, string Model)> TranscribeWithRetryAsync(
        byte[] wav, TranscriptionRequest request, DictationConfig config, CancellationToken ct)
    {
        var failed = 0;
        var triedBackup = false;
        var model = config.TranscribeModel;
        while (true)
        {
            try
            {
                return (await _speech.TranscribeAsync(wav, request, ct).ConfigureAwait(false), model);
            }
            catch (SpeechApiException ex) when (ex.Transient)
            {
                failed++;

                // Hız sınırında ilk çare yedek model: beklemeden, ayrı bir kota.
                if (!triedBackup && !string.IsNullOrWhiteSpace(config.BackupModel))
                {
                    triedBackup = true;
                    model = config.BackupModel.Trim();
                    request = request with { ModelOverride = model };
                    continue;
                }

                var wait = RetryLadder.NextDelay(failed);
                if (wait is null)
                    throw;

                Raise(StageChanged, DictationStage.Retrying);
                await _delay(wait.Value, ct).ConfigureAwait(false);
            }
        }
    }

    private DictationOutcome Fail(
        DictationConfig config, byte[] wav, double seconds, DictationHistoryEntry? retry, ProfileChoice choice, string model, string message)
    {
        // Konuşma bir daha söylenemez: ses saklanır, satır geçmişten yeniden denenebilir.
        var path = retry?.RecordingPath;
        if (path is null && config.KeepAudioOnFailure)
            path = _recordings.Save(wav);

        var id = retry?.Id ?? NewId();
        Record(config, new DictationHistoryEntry(
            id, DateTimeOffset.UtcNow, retry?.Raw ?? "", "", "", model, choice.Profile.Name, seconds, DictationHistory.StatusFailed, message, path));
        return new DictationOutcome(DictationOutcomeKind.Failed, Detail: message, HistoryId: id);
    }

    private void Record(DictationConfig config, DictationHistoryEntry entry)
    {
        foreach (var dropped in _history.Add(entry, config.HistoryLimit))
        {
            if (dropped.RecordingPath is { } path && dropped.Id != entry.Id)
                _recordings.Delete(path);
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static void Raise<T>(Action<T>? handler, T value)
    {
        try
        {
            handler?.Invoke(value);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Arayüz dinleyicisinin hatası hattı bozmasın.
            Koy.Core.Diagnostics.CrashLog.Write("DictationPipeline.Event", ex);
        }
    }
}
