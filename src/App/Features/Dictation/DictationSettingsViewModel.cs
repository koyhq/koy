using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Dictation;
using Koy.Core.Settings;
using Koy.Platform.Dictation;

namespace Koy.App.Features.Dictation;

/// <summary>Bir yazım profilinin düzenleme satırı. Değişiklik <see cref="Changed"/> ile sahibine bildirilir.</summary>
public sealed partial class ProfileRowViewModel : ObservableObject
{
    public ProfileRowViewModel(WritingProfile profile)
    {
        Id = profile.Id;
        Name = profile.Name;
        TranslateTo = profile.TranslateTo;
        Programs = string.Join(", ", profile.Programs);
        Sites = string.Join(", ", profile.Sites);
        Formality = profile.Style.Formality;
        SentenceLength = profile.Style.SentenceLength;
        Punctuation = profile.Style.Punctuation;
        Brevity = profile.Style.Brevity;
        Custom = profile.Style.Custom;
    }

    public string Id { get; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _translateTo = "";

    [ObservableProperty]
    private string _programs = "";

    [ObservableProperty]
    private string _sites = "";

    [ObservableProperty]
    private string _formality = "";

    [ObservableProperty]
    private string _sentenceLength = "";

    [ObservableProperty]
    private string _punctuation = "";

    [ObservableProperty]
    private string _brevity = "";

    [ObservableProperty]
    private string _custom = "";

    public event Action? Changed;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Changed?.Invoke();
    }

    public WritingProfile ToProfile() => new()
    {
        Id = Id,
        Name = Name.Trim(),
        TranslateTo = TranslateTo.Trim(),
        Programs = [.. Split(Programs).Select(ProfileSelector.ProgramKey).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal)],
        Sites = [.. Split(Sites)],
        Style = new ProfileStyle
        {
            Formality = Formality,
            SentenceLength = SentenceLength,
            Punctuation = Punctuation,
            Brevity = Brevity,
            Custom = Custom.Trim(),
        },
    };

    private static IEnumerable<string> Split(string text) =>
        text.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed record ProfileChoiceItem(string Id, string Label);

public sealed partial class TermRowViewModel(DictationTerm term) : ObservableObject
{
    public string Term { get; } = term.Term;
    public string Heard { get; } = term.Heard;
    public bool Auto { get; } = term.Auto;
    public bool HasHeard => !string.IsNullOrEmpty(Heard);
    public DictationTerm ToTerm() => new() { Term = Term, Heard = Heard, Auto = Auto };
}

public sealed class HistoryRowViewModel(DictationHistoryEntry entry)
{
    public string Id { get; } = entry.Id;
    public string TimeText { get; } = entry.Time.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string Text { get; } = string.IsNullOrEmpty(entry.Text) ? entry.Raw : entry.Text;
    public string Message { get; } = entry.Message;
    public bool IsFailed { get; } = entry.Status == DictationHistory.StatusFailed;
    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool CanRetry { get; } = entry.Status == DictationHistory.StatusFailed && entry.RecordingPath is not null;
    public string? RecordingPath { get; } = entry.RecordingPath;
    public string Meta { get; } = string.Join(" · ", new[] { entry.Time.ToLocalTime().ToString("g", CultureInfo.CurrentCulture), entry.Profile, entry.Model }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>
/// Dikte sayfasındaki "Sesle yazma" bölümü. Kendi dosyasına (<c>dictation.json</c>) yazar, settings.json'a dokunmaz;
/// her değişiklik hemen kaydedilir ve hat bir sonraki yazıda yeni ayarı okur. API anahtarı ayrı, DPAPI ile şifreli dosyadadır.
/// </summary>
public sealed partial class DictationSettingsViewModel : ObservableObject
{
    private readonly DictationService _service = DictationService.Instance;
    private readonly DictationConfig _base;
    private bool _loading = true;

    public DictationSettingsViewModel()
    {
        _base = _service.ConfigStore.Load();

        Enabled = _base.Enabled;
        BaseUrl = _base.BaseUrl;
        TranscribeModel = _base.TranscribeModel;
        BackupModel = _base.BackupModel;
        Language = _base.Language;
        CleanupEnabled = _base.CleanupEnabled;
        CleanupBaseUrl = _base.CleanupBaseUrl;
        CleanupModel = _base.CleanupModel;
        CleanupPrompt = _base.CleanupPrompt;
        AutoPaste = _base.AutoPaste;
        RestoreClipboard = _base.RestoreClipboard;
        AudioCues = _base.AudioCues;
        MaxSeconds = _base.MaxSeconds;
        HistoryLimit = _base.HistoryLimit;
        SkipSilent = _base.SkipSilent;
        FilterHallucinations = _base.FilterHallucinations;
        ProfileByApp = _base.ProfileByApp;
        TranslationEnabled = _base.TranslationEnabled;
        ProfileOverride = _base.ProfileOverride;
        DictionaryLearning = _base.DictionaryLearning;
        ApiKey = DictationKeyStore.Load();

        DefaultProfile = new ProfileRowViewModel(_base.DefaultProfile);
        DefaultProfile.Changed += Save;
        foreach (var profile in _base.Profiles)
            AddRow(profile);

        RebuildProfileChoices();
        LoadTerms();
        RefreshHistory();
        _service.HistoryChanged += RefreshHistory;
        _loading = false;
    }

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _baseUrl = "";

    [ObservableProperty]
    private string _apiKey = "";

    [ObservableProperty]
    private string _transcribeModel = "";

    [ObservableProperty]
    private string _backupModel = "";

    [ObservableProperty]
    private string _language = "";

    [ObservableProperty]
    private bool _cleanupEnabled;

    [ObservableProperty]
    private string _cleanupBaseUrl = "";

    [ObservableProperty]
    private string _cleanupModel = "";

    [ObservableProperty]
    private string _cleanupPrompt = "";

    [ObservableProperty]
    private bool _autoPaste;

    [ObservableProperty]
    private bool _restoreClipboard;

    [ObservableProperty]
    private bool _audioCues;

    [ObservableProperty]
    private int _maxSeconds;

    [ObservableProperty]
    private int _historyLimit;

    [ObservableProperty]
    private bool _skipSilent;

    [ObservableProperty]
    private bool _filterHallucinations;

    [ObservableProperty]
    private bool _profileByApp;

    [ObservableProperty]
    private bool _translationEnabled;

    [ObservableProperty]
    private string _profileOverride = "";

    [ObservableProperty]
    private bool _dictionaryLearning;

    [ObservableProperty]
    private ProfileRowViewModel? _selectedProfile;

    [ObservableProperty]
    private string _newTerm = "";

    [ObservableProperty]
    private string _newHeard = "";

    public ProfileRowViewModel DefaultProfile { get; }
    public ObservableCollection<ProfileRowViewModel> Profiles { get; } = [];
    public ObservableCollection<ProfileChoiceItem> ProfileChoices { get; } = [];
    public ObservableCollection<TermRowViewModel> Terms { get; } = [];
    public ObservableCollection<HistoryRowViewModel> History { get; } = [];

    public bool HasProfile => SelectedProfile is not null;
    public bool HasNoHistory => History.Count == 0;
    public bool HasNoTerms => Terms.Count == 0;

    partial void OnSelectedProfileChanged(ProfileRowViewModel? value) => OnPropertyChanged(nameof(HasProfile));

    partial void OnApiKeyChanged(string value)
    {
        if (!_loading)
            DictationKeyStore.Save(value);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loading || e.PropertyName is null || NotSaved.Contains(e.PropertyName))
            return;

        Save();
    }

    // Kaydedilmeyen özellikler: anahtar ayrı dosyada, öbürleri görünüm durumu.
    private static readonly HashSet<string> NotSaved = new(StringComparer.Ordinal)
    {
        nameof(ApiKey), nameof(SelectedProfile), nameof(NewTerm), nameof(NewHeard), nameof(HasProfile), nameof(HasNoHistory), nameof(HasNoTerms),
    };

    private void Save()
    {
        if (_loading)
            return;

        var config = _base with
        {
            Enabled = Enabled,
            BaseUrl = BaseUrl,
            TranscribeModel = TranscribeModel,
            BackupModel = BackupModel,
            Language = Language,
            CleanupEnabled = CleanupEnabled,
            CleanupBaseUrl = CleanupBaseUrl,
            CleanupModel = CleanupModel,
            CleanupPrompt = CleanupPrompt,
            AutoPaste = AutoPaste,
            RestoreClipboard = RestoreClipboard,
            AudioCues = AudioCues,
            MaxSeconds = MaxSeconds,
            HistoryLimit = HistoryLimit,
            SkipSilent = SkipSilent,
            FilterHallucinations = FilterHallucinations,
            ProfileByApp = ProfileByApp,
            TranslationEnabled = TranslationEnabled,
            ProfileOverride = ProfileOverride,
            DictionaryLearning = DictionaryLearning,
            DefaultProfile = DefaultProfile.ToProfile(),
            Profiles = [.. Profiles.Select(p => p.ToProfile())],
        };

        if (_service.ConfigStore.Save(config))
            _service.Reload();
    }

    [RelayCommand]
    private void AddProfile()
    {
        var profile = new WritingProfile { Id = Guid.NewGuid().ToString("N")[..8], Name = Strings.Get("DictNewProfileName") };
        var row = AddRow(profile);
        SelectedProfile = row;
        RebuildProfileChoices();
        Save();
    }

    [RelayCommand]
    private void RemoveProfile()
    {
        if (SelectedProfile is not { } row)
            return;

        Profiles.Remove(row);
        if (ProfileOverride == row.Id)
            ProfileOverride = "";
        SelectedProfile = Profiles.FirstOrDefault();
        RebuildProfileChoices();
        Save();
    }

    [RelayCommand]
    private void AddTerm()
    {
        var term = NewTerm.Trim();
        if (term.Length == 0 || DictionaryText.Contains(Terms.Select(t => t.ToTerm()), term))
            return;

        Terms.Add(new TermRowViewModel(new DictationTerm { Term = term, Heard = NewHeard.Trim() }));
        NewTerm = "";
        NewHeard = "";
        SaveTerms();
    }

    [RelayCommand]
    private void RemoveTerm(TermRowViewModel? row)
    {
        if (row is null || !Terms.Remove(row))
            return;

        SaveTerms();
    }

    [RelayCommand]
    private void ForgetLearned()
    {
        foreach (var row in Terms.Where(t => t.Auto).ToList())
            Terms.Remove(row);
        SaveTerms();
    }

    [RelayCommand]
    private void CopyHistory(HistoryRowViewModel? row)
    {
        if (row is null || row.Text.Length == 0)
            return;

        try
        {
            Clipboard.SetText(row.Text);
        }
        catch (COMException)
        {
            // Pano başka bir programda açık; kullanıcı yeniden dener.
        }
    }

    [RelayCommand]
    private async Task RetryHistoryAsync(HistoryRowViewModel? row)
    {
        if (row is null || !row.CanRetry)
            return;

        await _service.RetryAsync(row.Id);
        RefreshHistory();
    }

    [RelayCommand]
    private void DeleteHistory(HistoryRowViewModel? row)
    {
        if (row is null)
            return;

        DeleteRecordings(_service.History.Remove([row.Id]));
        RefreshHistory();
    }

    [RelayCommand]
    private void ClearHistory()
    {
        DeleteRecordings(_service.History.Clear());
        RefreshHistory();
    }

    private static void DeleteRecordings(IEnumerable<DictationHistoryEntry> removed)
    {
        var store = new RecordingStore();
        foreach (var path in removed.Select(e => e.RecordingPath).OfType<string>())
            store.Delete(path);
    }

    private ProfileRowViewModel AddRow(WritingProfile profile)
    {
        var row = new ProfileRowViewModel(profile);
        row.Changed += () =>
        {
            RebuildProfileChoices();
            Save();
        };
        Profiles.Add(row);
        return row;
    }

    // Seçim listesi profil adlarıyla birlikte tazelenir; ComboBox seçili kimliği kaybetmesin diye değer korunur.
    private void RebuildProfileChoices()
    {
        var keep = ProfileOverride;
        var wasLoading = _loading;
        _loading = true;
        ProfileChoices.Clear();
        ProfileChoices.Add(new ProfileChoiceItem("", Strings.Get("DictProfileAutomatic")));
        foreach (var row in Profiles)
            ProfileChoices.Add(new ProfileChoiceItem(row.Id, string.IsNullOrWhiteSpace(row.Name) ? row.Id : row.Name));
        ProfileOverride = keep;
        _loading = wasLoading;
    }

    private void LoadTerms()
    {
        Terms.Clear();
        foreach (var term in _service.Dictionary.Load().Entries)
            Terms.Add(new TermRowViewModel(term));
        OnPropertyChanged(nameof(HasNoTerms));
    }

    private void SaveTerms()
    {
        _service.Dictionary.Update(file => file.Entries = [.. Terms.Select(t => t.ToTerm())]);
        OnPropertyChanged(nameof(HasNoTerms));
    }

    private void RefreshHistory()
    {
        History.Clear();
        foreach (var entry in _service.History.Load().Reverse())
            History.Add(new HistoryRowViewModel(entry));
        OnPropertyChanged(nameof(HasNoHistory));
    }
}
