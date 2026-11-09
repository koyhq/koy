using Koy.Core.Diagnostics;
using Koy.Core.Voice;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;

namespace Koy.Platform.Voice;

/// <summary>
/// Windows.Media.SpeechRecognition (WinRT) tabanlı konuşma tanıma servisi. Ek NuGet paketi içermez.
/// Dikte konusu (SpeechRecognitionScenario.Dictation) Microsoft'un çevrimiçi konuşma hizmetini kullanır:
/// ses Microsoft'a gider ve Windows'ta "Çevrimiçi konuşma tanıma" açık olmalıdır. Yerel değildir.
/// Mikrofon yalnızca dinleme anında aktiftir, işlem bittiğinde nesne temizlenir (AGENTS.md #5).
/// </summary>
public sealed class WinRtSpeechRecognitionService : IVoiceRecognitionService
{
    private readonly Lock _gate = new();
    private SpeechRecognizer? _recognizer;
    private bool _isListening;
    private bool _stopRequested;
    private bool _resultGenerated;
    private bool _disposed;

    public bool IsListening
    {
        get
        {
            lock (_gate)
                return _isListening;
        }
    }

    public event EventHandler<string>? HypothesisReceived;
    public event EventHandler<string>? SpeechRecognized;
    public event EventHandler<VoiceRecognitionError>? RecognitionFailed;

    // Windows "Çevrimiçi konuşma tanıma" kapalıyken dikte başlatılamaz (SPERR_SPEECH_PRIVACY_POLICY_NOT_ACCEPTED).
    private const int SpeechPrivacyPolicyNotAccepted = unchecked((int)0x80045509);

    public async Task<bool> StartListeningAsync()
    {
        lock (_gate)
        {
            if (_disposed || _isListening)
                return false;
            _stopRequested = false;
            _resultGenerated = false;
        }

        try
        {
            await CleanupRecognizerAsync();

            // Sistemdeki konuşma dilini kontrol et; gerekirse desteklenen dille başlat
            var recognizer = CreateRecognizerWithFallback();

            // Dikte konusu kısıtı ekle
            var dictationConstraint = new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation,
                "Dictation");
            recognizer.Constraints.Add(dictationConstraint);

            var compilation = await recognizer.CompileConstraintsAsync();
            if (compilation.Status != SpeechRecognitionResultStatus.Success)
            {
                recognizer.Dispose();
                RecognitionFailed?.Invoke(this, new VoiceRecognitionError(VoiceFailure.RecognitionFailed, compilation.Status.ToString()));
                return false;
            }

            recognizer.HypothesisGenerated += OnHypothesisGenerated;
            recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
            recognizer.ContinuousRecognitionSession.Completed += OnSessionCompleted;

            await recognizer.ContinuousRecognitionSession.StartAsync(SpeechContinuousRecognitionMode.Default);

            lock (_gate)
            {
                if (_disposed || _stopRequested)
                {
                    _ = CleanupRecognizerInternalAsync(recognizer);
                    return false;
                }

                _recognizer = recognizer;
                _isListening = true;
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            // Windows Gizlilik ayarlarında mikrofon izni kapalı
            CrashLog.Write("WinRtSpeechRecognitionService.StartListening (İzin hatası)", ex);
            RecognitionFailed?.Invoke(this, new VoiceRecognitionError(VoiceFailure.MicrophonePermission));
            return false;
        }
        catch (Exception ex) when (ex.HResult == SpeechPrivacyPolicyNotAccepted)
        {
            RecognitionFailed?.Invoke(this, new VoiceRecognitionError(VoiceFailure.OnlineSpeechDisabled));
            return false;
        }
        catch (Exception ex)
        {
            CrashLog.Write("WinRtSpeechRecognitionService.StartListening", ex);
            RecognitionFailed?.Invoke(this, new VoiceRecognitionError(VoiceFailure.MicrophoneUnavailable, ex.Message));
            return false;
        }
    }

    private static SpeechRecognizer CreateRecognizerWithFallback()
    {
        try
        {
            var supported = SpeechRecognizer.SupportedTopicLanguages;
            var systemLang = SpeechRecognizer.SystemSpeechLanguage;

            var matched = supported.FirstOrDefault(l => string.Equals(l.LanguageTag, systemLang.LanguageTag, StringComparison.OrdinalIgnoreCase))
                ?? supported.FirstOrDefault();

            return matched is not null ? new SpeechRecognizer(matched) : new SpeechRecognizer();
        }
        catch
        {
            return new SpeechRecognizer();
        }
    }

    public async Task StopListeningAsync()
    {
        SpeechRecognizer? recognizer;
        lock (_gate)
        {
            _stopRequested = true;
            if (!_isListening || _recognizer is null)
                return;

            _isListening = false;
            recognizer = _recognizer;
        }

        try
        {
            await recognizer.ContinuousRecognitionSession.StopAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("WinRtSpeechRecognitionService.StopListening", ex);
        }
    }

    private void OnHypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
    {
        var text = args.Hypothesis?.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            HypothesisReceived?.Invoke(this, text);
        }
    }

    private void OnResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (args.Result.Status == SpeechRecognitionResultStatus.Success)
        {
            _resultGenerated = true;
            var text = args.Result.Text;
            SpeechRecognized?.Invoke(this, text ?? string.Empty);
        }
        else
        {
            RecognitionFailed?.Invoke(this, new VoiceRecognitionError(VoiceFailure.RecognitionFailed, args.Result.Status.ToString()));
        }
    }

    private void OnSessionCompleted(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
    {
        lock (_gate)
        {
            _isListening = false;
        }

        if (!_resultGenerated)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success &&
                args.Status != SpeechRecognitionResultStatus.UserCanceled &&
                args.Status != SpeechRecognitionResultStatus.TimeoutExceeded)
            {
                RecognitionFailed?.Invoke(this, new VoiceRecognitionError(VoiceFailure.RecognitionFailed, args.Status.ToString()));
            }
            else
            {
                SpeechRecognized?.Invoke(this, string.Empty);
            }
        }

        _ = CleanupRecognizerAsync();
    }

    private async Task CleanupRecognizerAsync()
    {
        SpeechRecognizer? old;
        lock (_gate)
        {
            old = _recognizer;
            _recognizer = null;
            _isListening = false;
        }

        if (old is null)
            return;

        await CleanupRecognizerInternalAsync(old);
    }

    private async Task CleanupRecognizerInternalAsync(SpeechRecognizer old)
    {
        try
        {
            old.HypothesisGenerated -= OnHypothesisGenerated;
            old.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
            old.ContinuousRecognitionSession.Completed -= OnSessionCompleted;

            try
            {
                await old.ContinuousRecognitionSession.StopAsync();
            }
            catch { }

            old.Dispose();
        }
        catch (Exception ex)
        {
            CrashLog.Write("WinRtSpeechRecognitionService.Cleanup", ex);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _ = CleanupRecognizerAsync();
    }
}
