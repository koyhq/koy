namespace Koy.Core.Voice;

public enum VoiceState
{
    Idle,
    Listening,
    Transcribing,
    AwaitingConfirmation,
    Running,
    Completed,
    Failed
}

public enum VoiceHarnessType
{
    Claude,
    Codex,
    /// <summary>Antigravity CLI (<c>agy</c>).</summary>
    Agy,
    Custom
}

/// <summary>
/// Sesli komutun neden başarısız olduğu. Kullanıcıya görünen metni App, seçili dile göre bu koddan üretir
/// (Core ve Platform dil bilmez).
/// </summary>
public enum VoiceFailure
{
    None,
    MicrophoneUnavailable,
    MicrophonePermission,
    /// <summary>Windows'ta "Çevrimiçi konuşma tanıma" kapalı; dikte bu olmadan çalışmaz.</summary>
    OnlineSpeechDisabled,
    RecognitionFailed,
    NoSpeech,
    InvalidCommand,
    ScriptNotSupported,
    CommandNotFound,
    Timeout,
    ExitCode,
    StartFailed,
    Unexpected
}

/// <param name="Detail">Mesaja eklenecek ham ayrıntı (durum adı, istisna metni); çevrilmez.</param>
public sealed record VoiceRecognitionError(VoiceFailure Failure, string Detail = "");

public sealed record ResolvedCommand(string Executable, IReadOnlyList<string> Arguments);

public sealed record HarnessExecutionRequest(
    VoiceHarnessType HarnessType,
    string Prompt,
    string WorkingDirectory,
    int TimeoutSeconds = 60,
    string? CustomExecutable = null,
    string? CustomArgumentsTemplate = null);

/// <param name="Error">Aracın kendi stderr çıktısı.</param>
/// <param name="Failure">Başarısızlık kodu; başarıda <see cref="VoiceFailure.None"/>.</param>
/// <param name="Detail">Yerelleştirilmiş mesajdaki {0} yerine konacak değer (dosya yolu, saniye).</param>
public sealed record HarnessExecutionResult(
    bool Success,
    int ExitCode,
    string Output,
    string Error,
    TimeSpan Duration,
    VoiceFailure Failure = VoiceFailure.None,
    string Detail = "");

public sealed record VoiceSessionOptions(
    VoiceHarnessType HarnessType = VoiceHarnessType.Claude,
    string WorkingDirectory = "",
    bool AutoConfirm = false,
    int TimeoutSeconds = 60,
    string? CustomExecutable = null,
    string? CustomArgumentsTemplate = null);

/// <summary>Özel harness komutu geçersiz (boş, kabuk yorumlayıcısı ya da kabuk metakarakteri içeriyor).</summary>
public sealed class VoiceCommandException(string message) : InvalidOperationException(message);
