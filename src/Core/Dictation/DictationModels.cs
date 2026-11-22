namespace Koy.Core.Dictation;

/// <summary>Kayıt durumu. İşlenmekte olan işler ayrıdır (<see cref="DictationPipeline.PendingJobs"/>): konuşmak, öncekinin işlenmesini beklemez.</summary>
public enum DictationState
{
    Idle,
    Recording,
    Paused
}

/// <summary>İşin o an hangi adımda olduğu (gösterge için).</summary>
public enum DictationStage
{
    Transcribing,
    Retrying,
    CleaningUp,
    Pasting
}

/// <summary>Bir yazının sonucu. Metni kullanıcıya gösterecek yazıyı App seçili dilde üretir (Core dil bilmez).</summary>
public enum DictationOutcomeKind
{
    /// <summary>Yazıldı.</summary>
    Done,

    /// <summary>Yazıldı ama bir adım (temizleme) başarısız oldu; ham metin kullanıldı.</summary>
    DoneWithWarning,

    /// <summary>Çeviri başarısız: özgün metin yalnız panoya kondu, pencereye yapıştırılmadı.</summary>
    CopiedOnly,

    /// <summary>Kayıtta konuşma yok; hiçbir şey gönderilmedi.</summary>
    NoSpeech,

    /// <summary>Model kalıp bir cümle uydurdu; atıldı.</summary>
    InventedPhrase,

    /// <summary>Kullanıcı iptal etti.</summary>
    Cancelled,

    /// <summary>Başarısız; ses saklandı, geçmişten yeniden denenebilir.</summary>
    Failed
}

/// <param name="Detail">Çevrilmeyen ham ayrıntı: ölçülen düzey (dB), hata iletisi, atılan cümle.</param>
public sealed record DictationOutcome(
    DictationOutcomeKind Kind,
    string Raw = "",
    string Text = "",
    string Detail = "",
    string Language = "",
    TimeSpan Duration = default,
    string? HistoryId = null);

/// <summary>Kaydedilmiş ses: bellekte WAV, blok başına RMS düzeyleri (0..1) ve süre.</summary>
public sealed record RecordedAudio(byte[] Wav, double Seconds, IReadOnlyList<double> RmsLevels, double ChunkSeconds);

public sealed record TranscriptionRequest(string Language, string Prompt, string? ModelOverride = null);

/// <param name="DetectedLanguage">Sunucu duyduğu dili söylediyse (ör. "tr"), yoksa boş.</param>
public sealed record TranscriptionResult(string Text, string DetectedLanguage = "");

/// <summary>Konuşma tanıma ya da temizleme isteği başarısız. <see cref="Transient"/>: bekleyip yeniden denemeye değer (hız sınırı, ağ).</summary>
public sealed class SpeechApiException(string message, bool transient = false, Exception? inner = null) : Exception(message, inner)
{
    public bool Transient { get; } = transient;
}

public interface IAudioRecorder
{
    /// <summary>Anlık tepe düzeyi (0..1), dalga formu için. Kayıt iş parçacığından çağrılır.</summary>
    event Action<double>? LevelChanged;

    bool IsRecording { get; }

    /// <summary>Kaydı başlatır; mikrofon açılamazsa <see cref="InvalidOperationException"/>.</summary>
    void Start();

    /// <summary>Kaydı bitirir ve sesi verir; kayıt yoksa null.</summary>
    RecordedAudio? Stop();

    /// <summary>Kaydı duraklatır ya da sürdürür (duraklatılan süre kayda girmez).</summary>
    void SetPaused(bool paused);

    /// <summary>Kaydı atar.</summary>
    void Discard();
}

public interface ISpeechToText
{
    Task<TranscriptionResult> TranscribeAsync(byte[] wav, TranscriptionRequest request, CancellationToken cancellationToken);
}

public interface ITextCleaner
{
    /// <summary>Sistem istemi ve (çitlenmiş) kullanıcı metniyle tek tur sohbet; yalnız metin döner, araç yok.</summary>
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken);
}

public interface IPasteService
{
    /// <summary>Metni panoya koyar, yapıştır tuşunu basar, istenirse eski panoyu geri koyar. Üç adım bölünmez.</summary>
    Task PasteAsync(string text, bool restoreClipboard, CancellationToken cancellationToken);

    /// <summary>Yalnız panoya koyar.</summary>
    Task CopyAsync(string text, CancellationToken cancellationToken);
}

public interface IForegroundProgram
{
    /// <summary>Önde olan programın adı (küçük harf, .exe'siz) ve pencere başlığı; bilinmiyorsa boş.</summary>
    (string Program, string Title) Current();
}

/// <summary>Hata durumunda ses dosyası saklanır (yeniden deneme için). Yalnız bu klasördeki dosyalar geçerlidir.</summary>
public interface IRecordingStore
{
    string Save(byte[] wav);

    /// <summary>Yol bu deponun kendi klasöründe değilse ya da dosya yoksa null.</summary>
    byte[]? Load(string path);

    void Delete(string path);
}
