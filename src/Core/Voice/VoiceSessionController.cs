namespace Koy.Core.Voice;

/// <summary>
/// Çentik içi sesli komut ve harness oturumunu yöneten durum makinesi.
/// Boşta -> Dinleniyor -> Çevriliyor -> Onay Bekliyor -> Çalışıyor -> Bitti / Hata geçişlerini koordine eder.
/// Kullanıcıya metin üretmez: hata <see cref="Failure"/> koduyla bildirilir, metni App seçili dilde yazar.
/// </summary>
public sealed class VoiceSessionController : IDisposable
{
    private readonly IVoiceRecognitionService _recognition;
    private readonly IHarnessRunnerService _runner;
    private readonly Func<VoiceSessionOptions> _optionsProvider;
    private readonly Lock _gate = new();

    private CancellationTokenSource? _runningCts;
    private bool _disposed;

    public VoiceState State { get; private set; } = VoiceState.Idle;
    public string CurrentHypothesis { get; private set; } = string.Empty;
    public string RecognizedPrompt { get; private set; } = string.Empty;

    /// <summary>Son başarısızlığın kodu; başarı ya da iptalde <see cref="VoiceFailure.None"/>.</summary>
    public VoiceFailure Failure { get; private set; }

    /// <summary>Başarısızlık mesajına eklenecek ham ayrıntı (yol, saniye, durum adı); çevrilmez.</summary>
    public string FailureDetail { get; private set; } = string.Empty;

    public HarnessExecutionResult? LastResult { get; private set; }

    public event Action<VoiceState>? StateChanged;
    public event Action<string>? HypothesisChanged;
    public event Action<string>? PromptChanged;
    public event Action<HarnessExecutionResult>? ExecutionFinished;

    public VoiceSessionController(
        IVoiceRecognitionService recognition,
        IHarnessRunnerService runner,
        Func<VoiceSessionOptions> optionsProvider)
    {
        _recognition = recognition ?? throw new ArgumentNullException(nameof(recognition));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));

        _recognition.HypothesisReceived += OnHypothesisReceived;
        _recognition.SpeechRecognized += OnSpeechRecognized;
        _recognition.RecognitionFailed += OnRecognitionFailed;
    }

    /// <summary>
    /// Kısayol basıldığında çağrılır:
    /// - Boştaysa dinlemeye başlar.
    /// - Dinleniyorsa konuşmayı erken sonlandırıp transkripsiyona geçer.
    /// - Onay bekliyorsa çalıştırır.
    /// - Çevriliyor ya da çalışıyorsa iptal eder.
    /// </summary>
    public async Task TriggerShortcutAsync()
    {
        VoiceState current;
        lock (_gate)
        {
            if (_disposed)
                return;
            current = State;
        }

        switch (current)
        {
            case VoiceState.Idle:
            case VoiceState.Completed:
            case VoiceState.Failed:
                await StartListeningAsync();
                break;

            case VoiceState.Listening:
                await StopListeningAsync();
                break;

            case VoiceState.Transcribing:
                Cancel();
                break;

            case VoiceState.AwaitingConfirmation:
                await ConfirmAsync();
                break;

            case VoiceState.Running:
                Cancel();
                break;
        }
    }

    public async Task StartListeningAsync()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            CurrentHypothesis = string.Empty;
            RecognizedPrompt = string.Empty;
            LastResult = null;
            SetFailure(VoiceFailure.None);
            SetState(VoiceState.Listening);
        }

        var started = await _recognition.StartListeningAsync();
        if (!started)
        {
            lock (_gate)
            {
                // Servis nedenini (ör. izin yok) olayla zaten bildirdiyse o korunur.
                if (_disposed || State == VoiceState.Failed)
                    return;

                SetFailure(VoiceFailure.MicrophoneUnavailable);
                SetState(VoiceState.Failed);
            }
        }
    }

    public async Task StopListeningAsync()
    {
        lock (_gate)
        {
            if (_disposed || State != VoiceState.Listening)
                return;

            SetState(VoiceState.Transcribing);
        }

        await _recognition.StopListeningAsync();
    }

    public async Task ConfirmAsync()
    {
        VoiceSessionOptions options;
        string prompt;
        CancellationTokenSource cts;

        lock (_gate)
        {
            if (_disposed || State != VoiceState.AwaitingConfirmation)
                return;

            options = _optionsProvider();
            prompt = RecognizedPrompt;

            _runningCts?.Cancel();
            _runningCts?.Dispose();
            _runningCts = new CancellationTokenSource();
            cts = _runningCts;

            SetFailure(VoiceFailure.None);
            SetState(VoiceState.Running);
        }

        try
        {
            var request = new HarnessExecutionRequest(
                options.HarnessType,
                prompt,
                options.WorkingDirectory,
                options.TimeoutSeconds,
                options.CustomExecutable,
                options.CustomArgumentsTemplate);

            var result = await _runner.ExecuteAsync(request, cts.Token);

            lock (_gate)
            {
                // İptal edildiyse (Cancel durumu zaten Idle yaptı) geç gelen sonuç durumu değiştirmez.
                if (_disposed || State != VoiceState.Running)
                    return;

                LastResult = result;
                if (result.Success)
                {
                    SetFailure(VoiceFailure.None);
                    SetState(VoiceState.Completed);
                }
                else
                {
                    var failure = result.Failure == VoiceFailure.None ? VoiceFailure.ExitCode : result.Failure;
                    SetFailure(failure, result.Detail);
                    SetState(VoiceState.Failed);
                }
            }

            ExecutionFinished?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                if (_disposed || State != VoiceState.Running)
                    return;

                SetFailure(VoiceFailure.None);
                SetState(VoiceState.Idle);
            }
        }
        catch (VoiceCommandException ex)
        {
            Fail(VoiceFailure.InvalidCommand, ex.Message);
        }
        catch (Exception ex)
        {
            Fail(VoiceFailure.Unexpected, ex.Message);
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (State == VoiceState.Listening || State == VoiceState.Transcribing)
            {
                _ = _recognition.StopListeningAsync();
            }

            if (State == VoiceState.Running)
            {
                _runningCts?.Cancel();
            }

            CurrentHypothesis = string.Empty;
            RecognizedPrompt = string.Empty;
            SetFailure(VoiceFailure.None);
            SetState(VoiceState.Idle);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            Cancel();
            LastResult = null;
        }
    }

    private void Fail(VoiceFailure failure, string detail)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            SetFailure(failure, detail);
            SetState(VoiceState.Failed);
        }
    }

    private void OnHypothesisReceived(object? sender, string hypothesis)
    {
        lock (_gate)
        {
            if (_disposed || (State != VoiceState.Listening && State != VoiceState.Transcribing))
                return;

            CurrentHypothesis = hypothesis;
        }

        HypothesisChanged?.Invoke(hypothesis);
    }

    private void OnSpeechRecognized(object? sender, string prompt)
    {
        bool autoConfirm;
        lock (_gate)
        {
            if (_disposed || (State != VoiceState.Listening && State != VoiceState.Transcribing))
                return;

            _ = _recognition.StopListeningAsync();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                SetFailure(VoiceFailure.NoSpeech);
                SetState(VoiceState.Idle);
                return;
            }

            RecognizedPrompt = prompt;
            CurrentHypothesis = string.Empty;
            SetState(VoiceState.AwaitingConfirmation);
            autoConfirm = _optionsProvider().AutoConfirm;
        }

        PromptChanged?.Invoke(prompt);

        if (autoConfirm)
        {
            _ = ConfirmAsync();
        }
    }

    private void OnRecognitionFailed(object? sender, VoiceRecognitionError error)
    {
        Fail(error.Failure, error.Detail);
    }

    private void SetFailure(VoiceFailure failure, string detail = "")
    {
        Failure = failure;
        FailureDetail = detail ?? string.Empty;
    }

    private void SetState(VoiceState newState)
    {
        State = newState;
        StateChanged?.Invoke(newState);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _runningCts?.Cancel();
            _runningCts?.Dispose();
            _runningCts = null;
        }

        _recognition.HypothesisReceived -= OnHypothesisReceived;
        _recognition.SpeechRecognized -= OnSpeechRecognized;
        _recognition.RecognitionFailed -= OnRecognitionFailed;
        _recognition.Dispose();
    }
}
