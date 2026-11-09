namespace Koy.Core.Voice;

/// <summary>
/// Çentikteki sesli komut oturumu: Boşta -> Yazılıyor -> Çalışıyor -> Bitti / Hata.
/// Konuşmayı Koy tanımaz; Windows sesle yazma (Win+H) metni çentikteki kutuya yazar, kullanıcı isterse kendisi de yazar.
/// Böylece Windows'un desteklediği her dilde (Türkçe, İngilizce…) çalışır ve ayrıca konuşma dili paketi gerekmez.
/// Kullanıcıya metin üretmez: hata <see cref="Failure"/> koduyla bildirilir, metni App seçili dilde yazar.
/// </summary>
public sealed class VoiceSessionController(IHarnessRunnerService runner, Func<VoiceSessionOptions> optionsProvider) : IDisposable
{
    private readonly IHarnessRunnerService _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly Func<VoiceSessionOptions> _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
    private readonly Lock _gate = new();

    private CancellationTokenSource? _runningCts;
    private bool _disposed;

    public VoiceState State { get; private set; } = VoiceState.Idle;

    /// <summary>Kutudaki komut metni (sesle yazılan ya da elle yazılan).</summary>
    public string Prompt { get; private set; } = string.Empty;

    /// <summary>Son başarısızlığın kodu; başarı ya da iptalde <see cref="VoiceFailure.None"/>.</summary>
    public VoiceFailure Failure { get; private set; }

    /// <summary>Başarısızlık mesajına eklenecek ham ayrıntı (yol, saniye); çevrilmez.</summary>
    public string FailureDetail { get; private set; } = string.Empty;

    public HarnessExecutionResult? LastResult { get; private set; }

    public event Action<VoiceState>? StateChanged;
    public event Action<HarnessExecutionResult>? ExecutionFinished;

    /// <summary>
    /// Kısayol: boştaysa kutuyu açar; kutu açıkken metin varsa çalıştırır, yoksa kapatır; çalışıyorsa iptal eder.
    /// </summary>
    public async Task TriggerShortcutAsync()
    {
        VoiceState current;
        bool hasPrompt;
        lock (_gate)
        {
            if (_disposed)
                return;
            current = State;
            hasPrompt = !string.IsNullOrWhiteSpace(Prompt);
        }

        switch (current)
        {
            case VoiceState.Idle:
            case VoiceState.Completed:
            case VoiceState.Failed:
                Start();
                break;

            case VoiceState.Editing when hasPrompt:
                await ConfirmAsync();
                break;

            case VoiceState.Editing:
            case VoiceState.Running:
                Cancel();
                break;
        }
    }

    /// <summary>Komut kutusunu boş açar.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || State == VoiceState.Running)
                return;

            Prompt = string.Empty;
            LastResult = null;
            SetFailure(VoiceFailure.None);
            SetState(VoiceState.Editing);
        }
    }

    /// <summary>Kutudaki metin değişti (sesle yazma ya da klavye). Yalnız kutu açıkken kabul edilir.</summary>
    public void UpdatePrompt(string text)
    {
        lock (_gate)
        {
            if (!_disposed && State == VoiceState.Editing)
                Prompt = text ?? string.Empty;
        }
    }

    /// <summary>Kutudaki metni çalıştırır. Metin boşsa bir şey yapmaz (kutu açık kalır).</summary>
    public async Task ConfirmAsync()
    {
        VoiceSessionOptions options;
        string prompt;
        CancellationTokenSource cts;

        lock (_gate)
        {
            if (_disposed || State != VoiceState.Editing || string.IsNullOrWhiteSpace(Prompt))
                return;

            options = _optionsProvider();
            prompt = Prompt.Trim();

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
                options.CustomArgumentsTemplate,
                options.ClaudePermission,
                options.Model);

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

            if (State == VoiceState.Running)
                _runningCts?.Cancel();

            Prompt = string.Empty;
            SetFailure(VoiceFailure.None);
            SetState(VoiceState.Idle);
        }
    }

    private void Fail(VoiceFailure failure, string detail)
    {
        lock (_gate)
        {
            if (_disposed || State != VoiceState.Running)
                return;

            SetFailure(failure, detail);
            SetState(VoiceState.Failed);
        }
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
    }
}
