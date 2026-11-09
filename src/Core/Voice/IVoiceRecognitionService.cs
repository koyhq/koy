namespace Koy.Core.Voice;

/// <summary>
/// Konuşma tanıma servisi sözleşmesi. Yalnızca dinleme esnasında mikrofonu aktif tutar.
/// </summary>
public interface IVoiceRecognitionService : IDisposable
{
    bool IsListening { get; }
    Task<bool> StartListeningAsync();
    Task StopListeningAsync();

    event EventHandler<string>? HypothesisReceived;
    event EventHandler<string>? SpeechRecognized;
    event EventHandler<VoiceRecognitionError>? RecognitionFailed;
}
