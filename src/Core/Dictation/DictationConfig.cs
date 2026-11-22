using Koy.Core.Settings;
using Koy.Core.Voice;

namespace Koy.Core.Dictation;

/// <summary>
/// Sesle yazma ayarları. Bilerek settings.json'ın dışında (<c>dictation.json</c>, kural dosyası gibi): ayar şemasını ve
/// profil içe/dışa aktarmayı etkilemez. Anahtar burada değil, Windows DPAPI ile ayrı saklanır.
/// </summary>
public sealed record DictationConfig
{
    /// <summary>Varsayılan kapalı: ses bilgisayardan çıkabilir, açmak kullanıcının kararı.</summary>
    public bool Enabled { get; set; }

    /// <summary>OpenAI uyumlu uç nokta (…/v1). Yerelde çalışan bir sunucu da olabilir; ses o zaman bilgisayardan çıkmaz.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string TranscribeModel { get; set; } = "gpt-4o-transcribe";

    /// <summary>Hız sınırında denenecek yedek model; boş = yok.</summary>
    public string BackupModel { get; set; } = "";

    /// <summary>"auto" ya da dil kodu (tr, en…).</summary>
    public string Language { get; set; } = "auto";

    public bool CleanupEnabled { get; set; } = true;

    /// <summary>Boşsa <see cref="BaseUrl"/>.</summary>
    public string CleanupBaseUrl { get; set; } = "";

    public string CleanupModel { get; set; } = "gpt-4o-mini";

    /// <summary>Boş = dile göre yerleşik istem.</summary>
    public string CleanupPrompt { get; set; } = "";

    public bool AutoPaste { get; set; } = true;
    public bool RestoreClipboard { get; set; } = true;

    public bool SkipSilent { get; set; } = true;
    public double SilenceDb { get; set; } = SpeechGate.DefaultFloorDb;
    public double SpeechMarginDb { get; set; } = SpeechGate.DefaultMarginDb;
    public double MinVoicedSeconds { get; set; } = SpeechGate.DefaultMinVoicedSeconds;
    public bool FilterHallucinations { get; set; } = true;

    public int MaxSeconds { get; set; } = 300;
    public int HistoryLimit { get; set; } = 200;
    public bool KeepAudioOnFailure { get; set; } = true;
    public bool AudioCues { get; set; }

    public bool DictionaryLearning { get; set; }

    public bool ProfileByApp { get; set; } = true;
    public bool TranslationEnabled { get; set; } = true;

    /// <summary>Elle seçilen profil (kimlik ya da ad); boş = programa göre.</summary>
    public string ProfileOverride { get; set; } = "";

    public WritingProfile DefaultProfile { get; set; } = new() { Id = "", Name = "" };
    public ValueList<WritingProfile> Profiles { get; set; } = [];
}

/// <summary>
/// Yazım profili: ton tercihleri, isteğe bağlı çeviri dili ve hangi programlarda / sitelerde kullanılacağı.
/// Varsayılan profilin kimliği boştur.
/// </summary>
public sealed record WritingProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ProfileStyle Style { get; set; } = new();

    /// <summary>Çeviri dili (ör. "English", "Türkçe" ya da kod); boş = çeviri yok.</summary>
    public string TranslateTo { get; set; } = "";

    /// <summary>Süreç adları (küçük harf, .exe'siz).</summary>
    public ValueList<string> Programs { get; set; } = [];

    /// <summary>Tarayıcı sekme başlığında aranacak site sözcükleri.</summary>
    public ValueList<string> Sites { get; set; } = [];
}

/// <summary>Ton tercihleri; yalnız bilinen değerler kullanılır, boş = istek yok.</summary>
public sealed record ProfileStyle
{
    /// <summary>formal | casual</summary>
    public string Formality { get; set; } = "";

    /// <summary>short | long</summary>
    public string SentenceLength { get; set; } = "";

    /// <summary>minimal | full</summary>
    public string Punctuation { get; set; } = "";

    /// <summary>brief | detailed</summary>
    public string Brevity { get; set; } = "";

    /// <summary>Kullanıcının kendi satırı.</summary>
    public string Custom { get; set; } = "";
}

/// <summary>Sözlük kaydı. <see cref="Heard"/> doluysa "duyulan → doğru yazım" düzeltmesidir.</summary>
public sealed record DictationTerm
{
    public string Term { get; set; } = "";
    public string Heard { get; set; } = "";

    /// <summary>Kendiliğinden öğrenildi (elle yazılandan ayırt edilir).</summary>
    public bool Auto { get; set; }
}

/// <summary>Sözlük dosyası: kayıtlar ve henüz sayılmakta olan aday adlar.</summary>
public sealed record DictionaryFile
{
    public ValueList<DictationTerm> Entries { get; set; } = [];
    public ValueDictionary<int> Pending { get; set; } = [];
}
