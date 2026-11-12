using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Settings.Profiles;

namespace Koy.App.Features.Settings;

/// <summary>Profil kaydederken / dışa aktarırken işaretlenen bölüm.</summary>
public sealed partial class ProfileSectionOption(ProfileSection section) : ObservableObject
{
    public ProfileSection Section { get; } = section;

    public string Label => Strings.Get("ProfileSection" + Section);

    [ObservableProperty]
    private bool _isSelected = true;

    public void RefreshLabel() => OnPropertyChanged(nameof(Label));
}

/// <summary>Kayıtlı profil listesinde bir satır. Düğmeler sahibi olan <see cref="ProfilesViewModel"/>'e iner.</summary>
public sealed partial class ProfileItemViewModel(ProfilesViewModel owner, SavedProfile profile) : ObservableObject
{
    public string Id { get; } = profile.Id;

    public string Name { get; } = profile.Name;

    public string SectionsText { get; } = ProfilesViewModel.DescribeSections(ProfileLibrary.SectionsOf(profile));

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _editName = profile.Name;

    [ObservableProperty]
    private bool _isDeleteConfirming;

    public bool IsIdle => !IsRenaming && !IsDeleteConfirming;

    partial void OnIsRenamingChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    partial void OnIsDeleteConfirmingChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    [RelayCommand]
    private void Apply() => owner.Apply(this);

    [RelayCommand]
    private void Export() => owner.Export(this);

    [RelayCommand]
    private void Duplicate() => owner.Duplicate(this);

    [RelayCommand]
    private void BeginRename()
    {
        EditName = Name;
        IsRenaming = true;
    }

    [RelayCommand]
    private void CommitRename() => owner.Rename(this);

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;

    [RelayCommand]
    private void RequestDelete() => IsDeleteConfirming = true;

    [RelayCommand]
    private void ConfirmDelete() => owner.Delete(this);

    [RelayCommand]
    private void CancelDelete() => IsDeleteConfirming = false;
}

/// <summary>
/// Profiller bölümü (Genel sayfası): geçerli ayarlardan profil kaydet, kayıtlı profili uygula / yeniden adlandır / çoğalt /
/// sil, dosyaya dışa aktar, dosyadan içe aktar (önizleme ve onayla). Karar mantığı Core'dadır
/// (<see cref="ProfileLibrary"/>, <see cref="SettingsProfileCodec"/>); bu sınıf yalnız dosya pencereleri, metin ve durum.
/// İçe aktarılan dosya güvenilmez girdidir: doğrulama Core'da, hata metni burada, uygulama çökmez.
/// </summary>
public sealed partial class ProfilesViewModel : ObservableObject
{
    private const string FileExtension = ".koyprofile.json";

    private readonly ProfileService _service;
    private ImportedProfile? _pending;

    public ProfilesViewModel(ProfileService service)
    {
        _service = service;
        foreach (var section in Enum.GetValues<ProfileSection>())
            Sections.Add(new ProfileSectionOption(section));
        Refresh();
    }

    public ObservableCollection<ProfileItemViewModel> Items { get; } = [];

    public ObservableCollection<ProfileSectionOption> Sections { get; } = [];

    public bool HasItems => Items.Count > 0;

    [ObservableProperty]
    private string _newName = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _statusIsError;

    public bool HasStatus => StatusText.Length > 0;

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    // İçe aktarma önizlemesi
    [ObservableProperty]
    private bool _hasPending;

    [ObservableProperty]
    private string _pendingName = "";

    [ObservableProperty]
    private string _pendingSectionsText = "";

    [ObservableProperty]
    private string _pendingChangesText = "";

    /// <summary>Dil değişince bölüm adları yeniden okunur (metinler Loc'a bağlı değil, burada hesaplanıyor).</summary>
    public void RefreshLanguage()
    {
        foreach (var option in Sections)
            option.RefreshLabel();
        Refresh();
    }

    /// <summary>Liste yeniden okundu (kaydet / sil / yeniden adlandır / içe aktar sonrası). Profil seçen başka bölümler yenilenir.</summary>
    public event Action? ListChanged;

    public void Refresh()
    {
        Items.Clear();
        foreach (var profile in _service.List())
            Items.Add(new ProfileItemViewModel(this, profile));
        OnPropertyChanged(nameof(HasItems));
        ListChanged?.Invoke();
    }

    public static string DescribeSections(IEnumerable<ProfileSection> sections)
    {
        var names = sections.Select(s => Strings.Get("ProfileSection" + s)).ToList();
        return names.Count == 0 ? Strings.Get("ProfileCorrupt") : string.Join(", ", names);
    }

    private List<ProfileSection> SelectedSections() =>
        Sections.Where(s => s.IsSelected).Select(s => s.Section).ToList();

    private void Report(string key, bool error = false, params object[] args)
    {
        var format = Strings.Get(key);
        StatusText = args.Length == 0 ? format : string.Format(System.Globalization.CultureInfo.CurrentCulture, format, args);
        StatusIsError = error;
    }

    private void ReportLibraryError(ProfileLibraryError error) => Report("ProfileError" + error, error: true);

    [RelayCommand]
    private void SaveCurrent()
    {
        var result = _service.CreateFromCurrent(NewName, SelectedSections());
        if (!result.IsSuccess)
        {
            ReportLibraryError(result.Error);
            return;
        }

        NewName = "";
        Refresh();
        Report("ProfileSaved", false, result.Profile!.Name);
    }

    [RelayCommand]
    private void ExportCurrent()
    {
        var name = NewName.Trim();
        if (!SettingsProfileCodec.IsValidName(name))
        {
            ReportLibraryError(ProfileLibraryError.InvalidName);
            return;
        }

        var sections = SelectedSections();
        if (sections.Count == 0)
        {
            ReportLibraryError(ProfileLibraryError.NoSections);
            return;
        }

        string text;
        try
        {
            // Dosya, geçerli (kaydedilmiş) ayarlardan üretilir; kütüphaneye eklenmez.
            text = SettingsProfileCodec.Export(_service.CurrentSettings(), name, sections);
        }
        catch (ArgumentException)
        {
            ReportLibraryError(ProfileLibraryError.InvalidName);
            return;
        }

        WriteExportFile(name, text);
    }

    internal void Export(ProfileItemViewModel item)
    {
        var profile = _service.Find(item.Id);
        if (profile is null)
        {
            ReportLibraryError(ProfileLibraryError.NotFound);
            Refresh();
            return;
        }

        WriteExportFile(profile.Name, profile.Json);
    }

    private void WriteExportFile(string name, string text)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.Get("ProfileExportDialogTitle"),
            Filter = Strings.Get("ProfileFileFilter"),
            FileName = SafeFileName(name) + FileExtension,
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (!ShowDialog(dialog))
            return;

        try
        {
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
            Report("ProfileExported", false, Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            Report("ProfileExportFailed", true);
        }
    }

    [RelayCommand]
    private void Import()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Get("ProfileImportDialogTitle"),
            Filter = Strings.Get("ProfileFileFilter"),
            CheckFileExists = true,
        };

        if (!ShowDialog(dialog))
            return;

        string text;
        try
        {
            // Çok büyük dosya belleğe okunmadan reddedilir (güvenilmez girdi).
            if (new FileInfo(dialog.FileName).Length > ProfileLibrary.MaxFileBytes)
            {
                Report("ProfileImportErrorTooLarge", true);
                return;
            }

            text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            Report("ProfileImportReadFailed", true);
            return;
        }

        var read = SettingsProfileCodec.Read(text);
        if (!read.IsValid)
        {
            ClearPending();
            Report("ProfileImportError" + read.Error, true);
            return;
        }

        var imported = read.Profile!;
        var changed = SettingsProfileCodec.ChangedSections(_service.CurrentSettings(), imported);

        _pending = imported;
        PendingName = imported.Name;
        PendingSectionsText = DescribeSections(imported.Sections);
        PendingChangesText = changed.Count == 0
            ? Strings.Get("ProfileImportNoChanges")
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("ProfileImportChanges"), DescribeSections(changed));
        HasPending = true;
        StatusText = "";
    }

    [RelayCommand]
    private void ConfirmImportSave()
    {
        if (_pending is null)
            return;

        var result = _service.AddImported(_pending);
        if (!result.IsSuccess)
        {
            ReportLibraryError(result.Error);
            return;
        }

        ClearPending();
        Refresh();
        Report("ProfileSaved", false, result.Profile!.Name);
    }

    [RelayCommand]
    private void ConfirmImportApply()
    {
        if (_pending is null)
            return;

        var added = _service.AddImported(_pending);
        if (!added.IsSuccess)
        {
            ReportLibraryError(added.Error);
            return;
        }

        ClearPending();
        Refresh();
        ApplyById(added.Profile!);
    }

    [RelayCommand]
    private void CancelImport() => ClearPending();

    private void ClearPending()
    {
        _pending = null;
        HasPending = false;
        PendingName = PendingSectionsText = PendingChangesText = "";
    }

    internal void Apply(ProfileItemViewModel item)
    {
        var profile = _service.Find(item.Id);
        if (profile is null)
        {
            ReportLibraryError(ProfileLibraryError.NotFound);
            Refresh();
            return;
        }

        ApplyById(profile);
    }

    private void ApplyById(SavedProfile profile)
    {
        var result = _service.Apply(profile.Id);
        if (!result.IsSuccess)
        {
            ReportLibraryError(result.Error);
            return;
        }

        // Açık pencerenin değerleri ProfileService.Applied olayıyla KoyWindowViewModel'de yenilenir.
        Report(result.ChangedSections.Count == 0 ? "ProfileAppliedNoChange" : "ProfileApplied", false, profile.Name);
    }

    internal void Rename(ProfileItemViewModel item)
    {
        var result = _service.Rename(item.Id, item.EditName);
        if (!result.IsSuccess)
        {
            ReportLibraryError(result.Error);
            return;
        }

        Refresh();
        Report("ProfileRenamed", false, result.Profile!.Name);
    }

    internal void Duplicate(ProfileItemViewModel item)
    {
        var result = _service.Duplicate(item.Id);
        if (!result.IsSuccess)
        {
            ReportLibraryError(result.Error);
            return;
        }

        Refresh();
        Report("ProfileSaved", false, result.Profile!.Name);
    }

    internal void Delete(ProfileItemViewModel item)
    {
        var result = _service.Delete(item.Id);
        if (!result.IsSuccess)
        {
            ReportLibraryError(result.Error);
            return;
        }

        Refresh();
        Report("ProfileDeleted", false, result.Profile!.Name);
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
        return cleaned.Length == 0 ? "koy-profile" : cleaned;
    }

    private static bool ShowDialog(Microsoft.Win32.FileDialog dialog)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        return (owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;
    }
}
