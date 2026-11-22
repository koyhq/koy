using System.Globalization;
using System.Media;
using System.Windows;
using Koy.App.Resources;
using Koy.Core.Diagnostics;
using Koy.Core.Dictation;
using Koy.Core.Settings;
using Koy.Core.Shortcuts;
using Koy.Platform.Dictation;

namespace Koy.App.Features.Dictation;

/// <summary>
/// Sesle yazmanın uygulama tarafı: kısayolların gittiği tek yer. Hat (mikrofon, istemciler) <b>ilk kullanımda</b> kurulur;
/// özellik kapalıyken ya da hiç kullanılmadıysa mikrofon, HTTP istemcisi ve dinleyici yoktur (AGENTS #5, #12).
/// Durum çentikte kısa bir kartla gösterilir (çentik kapalıysa kart yok; Windows'un mikrofon göstergesi yine yanar).
/// </summary>
public sealed class DictationService
{
    private static readonly TimeSpan RecordingCard = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StageCard = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ResultCard = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ProblemCard = TimeSpan.FromSeconds(9);

    private readonly Lock _gate = new();
    private DictationConfig? _config;
    private DictationPipeline? _pipeline;
    private MediaCaptureRecorder? _recorder;

    public static DictationService Instance { get; } = new();

    public DictationConfigStore ConfigStore { get; } = new();
    public DictionaryStore Dictionary { get; } = new();
    public DictationHistory History { get; } = new();

    /// <summary>Çentik özelliği kendini buraya bağlar (kart başlığı, alt satır, süre); çentik yoksa boş.</summary>
    public Action<string, string, TimeSpan>? ShowCard { get; set; }

    public Action? HideCard { get; set; }

    /// <summary>Geçmiş değişti (yeni yazı, silme); ayarlar penceresi listeyi tazeler. Arayüz iş parçacığında çağrılır.</summary>
    public event Action? HistoryChanged;

    public DictationConfig Config
    {
        get
        {
            lock (_gate)
                return _config ??= ConfigStore.Load();
        }
    }

    /// <summary>Ayar dosyası değişti (ayarlar penceresi yazdı); hat bir sonraki yazıda yenisini okur.</summary>
    public void Reload()
    {
        lock (_gate)
            _config = ConfigStore.Load();
    }

    public bool IsRecording => _pipeline?.State is DictationState.Recording or DictationState.Paused;

    /// <summary>Kısayol: kapalıysa Koy penceresi açılır (ayarlar orada), açıksa kayıt başlar ya da biter.</summary>
    public async Task ToggleAsync()
    {
        if (!Config.Enabled)
        {
            Koy.App.Features.Settings.KoyWindow.ShowInstance(6);
            return;
        }

        await RunAsync(async pipeline => await pipeline.ToggleAsync().ConfigureAwait(true)).ConfigureAwait(true);
    }

    public Task CancelAsync() => _pipeline is null ? Task.CompletedTask : RunAsync(p => p.CancelAsync());

    public Task TogglePauseAsync()
    {
        var pipeline = _pipeline;
        if (pipeline is null || pipeline.State == DictationState.Idle)
            return Task.CompletedTask;

        var pause = pipeline.State == DictationState.Recording;
        return RunAsync(p => p.SetPausedAsync(pause));
    }

    /// <summary>Başarısız bir yazıyı saklanan sesinden yeniden dener.</summary>
    public async Task<bool> RetryAsync(string historyId)
    {
        if (!Config.Enabled)
            return false;

        var pipeline = EnsurePipeline();
        var started = false;
        try
        {
            Present(StageText(DictationStage.Transcribing));
            started = await pipeline.RetryAsync(historyId).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            CrashLog.Write("DictationService.Retry", ex);
        }

        if (!started)
            HideCard?.Invoke();
        return started;
    }

    private async Task RunAsync(Func<DictationPipeline, Task> action)
    {
        try
        {
            await action(EnsurePipeline()).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Hat kendi hatalarını sonuç olarak bildirir; buraya düşen beklenmeyen bir şeydir.
            CrashLog.Write("DictationService", ex);
            ShowCard?.Invoke(Strings.Get("DictationFailed"), ex.Message, ProblemCard);
        }
    }

    private DictationPipeline EnsurePipeline()
    {
        lock (_gate)
        {
            if (_pipeline is not null)
                return _pipeline;

            _recorder = new MediaCaptureRecorder();
            var pipeline = new DictationPipeline(
                _recorder,
                new OpenAiSpeechToText(() => Config, DictationKeyStore.Load),
                new OpenAiTextCleaner(() => Config, DictationKeyStore.Load),
                new WpfPasteService(),
                new ForegroundProgramService(),
                new RecordingStore(),
                History,
                Dictionary,
                () => Config);
            pipeline.StateChanged += OnStateChanged;
            pipeline.StageChanged += OnStageChanged;
            pipeline.Finished += OnFinished;
            return _pipeline = pipeline;
        }
    }

    private void OnStateChanged(DictationState state) => OnUi(() =>
    {
        switch (state)
        {
            case DictationState.Recording:
                Cue(SystemSounds.Beep);
                Present(Strings.Get("DictationListening"), string.Format(CultureInfo.CurrentCulture, Strings.Get("DictationListeningHint"), ShortcutText()), RecordingCard);
                break;
            case DictationState.Paused:
                Present(Strings.Get("DictationPaused"), "", RecordingCard);
                break;
        }
    });

    private void OnStageChanged(DictationStage stage) => OnUi(() => Present(StageText(stage)));

    private void OnFinished(DictationOutcome outcome) => OnUi(() =>
    {
        switch (outcome.Kind)
        {
            case DictationOutcomeKind.Done:
                Cue(SystemSounds.Asterisk);
                Present(Strings.Get("DictationDone"), Truncate(outcome.Text, 90), ResultCard);
                break;
            case DictationOutcomeKind.DoneWithWarning:
                Present(Strings.Get("DictationDoneWarning"),
                    string.Format(CultureInfo.CurrentCulture, Strings.Get("DictationCleanupFailed"), Truncate(outcome.Detail, 120)), ProblemCard);
                break;
            case DictationOutcomeKind.CopiedOnly:
                Present(Strings.Get("DictationCopiedOnly"), Truncate(outcome.Detail, 120), ProblemCard);
                break;
            case DictationOutcomeKind.NoSpeech:
                Cue(SystemSounds.Hand);
                Present(Strings.Get(outcome.Detail == DictationPipeline.TooShort ? "DictationTooShort" : "DictationNoSpeech"),
                    double.TryParse(outcome.Detail, NumberStyles.Float, CultureInfo.InvariantCulture, out var db)
                        ? string.Format(CultureInfo.CurrentCulture, Strings.Get("DictationNoSpeechLevel"), db)
                        : "",
                    ResultCard);
                break;
            case DictationOutcomeKind.InventedPhrase:
                Present(Strings.Get("DictationInvented"), Truncate(outcome.Detail, 90), ResultCard);
                break;
            case DictationOutcomeKind.Failed:
                Cue(SystemSounds.Hand);
                Present(Strings.Get("DictationFailed"), Truncate(outcome.Detail, 140), ProblemCard);
                break;
            case DictationOutcomeKind.Cancelled:
                HideCard?.Invoke();
                break;
        }

        if (outcome.HistoryId is not null || outcome.Kind is DictationOutcomeKind.Done or DictationOutcomeKind.DoneWithWarning or DictationOutcomeKind.CopiedOnly)
            HistoryChanged?.Invoke();
    });

    private static string StageText(DictationStage stage) => Strings.Get(stage switch
    {
        DictationStage.Transcribing => "DictationTranscribing",
        DictationStage.Retrying => "DictationRetrying",
        DictationStage.CleaningUp => "DictationCleaning",
        _ => "DictationPasting",
    });

    private void Present(string title, string subtitle = "", TimeSpan? lifetime = null) =>
        ShowCard?.Invoke(title, subtitle, lifetime ?? StageCard);

    private void Cue(SystemSound sound)
    {
        if (Config.AudioCues)
            sound.Play();
    }

    private static string ShortcutText()
    {
        var text = ShortcutBindings.GetText(new SettingsStore(SettingsStore.DefaultPath).Load(), ShortcutAction.Dictate);
        return string.IsNullOrWhiteSpace(text) ? Strings.Get("DictationShortcutUnset") : text;
    }

    private static string Truncate(string text, int length)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= length ? line : line[..(length - 1)] + "…";
    }

    // Hat olayları kayıt ve iş iş parçacıklarından gelir; çentik ve ayarlar arayüz iş parçacığında güncellenir.
    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _ = dispatcher.InvokeAsync(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                CrashLog.Write("DictationService.Ui", ex);
            }
        });
    }
}
