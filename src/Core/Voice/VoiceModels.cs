namespace Koy.Core.Voice;

public enum VoiceState
{
    Idle,

    /// <summary>Çentikte komut kutusu açık: Windows sesle yazma (Win+H) metni yazıyor ya da kullanıcı kendisi yazıyor.</summary>
    Editing,
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
/// Claude'un izin modu. Sesli komut <c>claude -p</c> ile başsız çalışır: izin sorusu çıkamaz,
/// bu yüzden <see cref="Default"/> kipinde Claude yalnız okur; dosya oluşturma ve düzenleme reddedilir.
/// </summary>
public enum VoiceClaudePermission
{
    /// <summary>Ek bayrak yok: yalnız okuma.</summary>
    Default,

    /// <summary>Claude'un otomatik modu (<c>--permission-mode auto</c>): sınıflandırıcı güvenli işleri onaylar, riskliyi reddeder.</summary>
    Auto,

    /// <summary>Dosya düzenleme ve oluşturma (<c>--permission-mode acceptEdits</c>) onaysız yapılır.</summary>
    AcceptEdits
}

/// <summary>
/// Komutun neden başarısız olduğu. Kullanıcıya görünen metni App, seçili dile göre bu koddan üretir
/// (Core ve Platform dil bilmez).
/// </summary>
public enum VoiceFailure
{
    None,
    InvalidCommand,
    /// <summary>Ayardaki model adı geçersiz; ayrıntı modelin adı.</summary>
    InvalidModel,
    ScriptNotSupported,
    CommandNotFound,
    Timeout,
    ExitCode,
    StartFailed,
    Unexpected
}

public sealed record ResolvedCommand(string Executable, IReadOnlyList<string> Arguments);

public sealed record HarnessExecutionRequest(
    VoiceHarnessType HarnessType,
    string Prompt,
    string WorkingDirectory,
    int TimeoutSeconds = 60,
    string? CustomExecutable = null,
    string? CustomArgumentsTemplate = null,
    VoiceClaudePermission ClaudePermission = VoiceClaudePermission.Default,
    string? Model = null);

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
    int TimeoutSeconds = 60,
    string? CustomExecutable = null,
    string? CustomArgumentsTemplate = null,
    VoiceClaudePermission ClaudePermission = VoiceClaudePermission.Default,
    string? Model = null);

/// <summary>Özel harness komutu geçersiz (boş, kabuk yorumlayıcısı ya da kabuk metakarakteri içeriyor).</summary>
/// <summary>Komut çözülemedi. <see cref="Failure"/> ve <see cref="Detail"/> kullanıcı metnini App'te seçer; ileti yalnız
/// geliştirici içindir.</summary>
public sealed class VoiceCommandException(string message, VoiceFailure failure = VoiceFailure.InvalidCommand, string detail = "")
    : InvalidOperationException(message)
{
    public VoiceFailure Failure { get; } = failure;
    public string Detail { get; } = detail;
}
