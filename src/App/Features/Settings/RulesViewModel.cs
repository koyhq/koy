using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Rules;
using Koy.App.Resources;
using Koy.Core.Rules;
using Koy.Core.Settings.Profiles;

namespace Koy.App.Features.Settings;

/// <summary>Profil seçeneği (kural ekleme açılır listesi).</summary>
public sealed record RuleProfileOption(string Id, string Name);

/// <summary>Kural listesinde bir satır. Açma/kapama ve silme sahibi <see cref="RulesViewModel"/>'e iner.</summary>
public sealed partial class RuleItemViewModel(RulesViewModel owner, int index, RuleDraft draft, string profileName) : ObservableObject
{
    private bool _isEnabled = draft.Enabled;

    public int Index { get; } = index;

    /// <summary>Tek süreç koşullu kuralda exe adı; elle yazılmış gelişmiş kuralda genel ad.</summary>
    public string Title { get; } = RuleListEditor.ProcessOf(draft) is { } process
        ? process + ".exe"
        : (draft.Name ?? "") + " (" + Strings.Get("RulesAdvancedRule") + ")";

    public string ProfileText { get; } = string.Format(Strings.Get("RulesUsesProfile"), profileName);

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value))
                return;
            owner.SetEnabled(this, value);
        }
    }

    [RelayCommand]
    private void Delete() => owner.Delete(this);
}

/// <summary>
/// Kurallar bölümü (Genel sayfası): uygulamaya (exe) göre profil kuralı ekle, aç/kapat, sil. Karar mantığı Core'dadır
/// (<see cref="RuleListEditor"/>, <see cref="ContextProfileSwitcher"/>); bu sınıf yalnız metin, durum ve çalışan uygulama listesi.
/// Yeni kural kapalı eklenir.
/// </summary>
public sealed partial class RulesViewModel : ObservableObject
{
    private readonly ProfileService _profiles;
    private readonly ContextRuleService _rules;
    private bool _refreshing;

    public RulesViewModel(ProfileService profiles, ContextRuleService rules)
    {
        _profiles = profiles;
        _rules = rules;
        Refresh();
        RefreshRunningApps();
    }

    public ObservableCollection<RuleItemViewModel> Items { get; } = [];

    public ObservableCollection<RuleProfileOption> ProfileOptions { get; } = [];

    public ObservableCollection<string> RunningApps { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool HasProfiles => ProfileOptions.Count > 0;

    [ObservableProperty]
    private string _processText = "";

    [ObservableProperty]
    private string? _pickedApp;

    [ObservableProperty]
    private RuleProfileOption? _selectedProfile;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _statusIsError;

    public bool HasStatus => StatusText.Length > 0;

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnPickedAppChanged(string? value)
    {
        if (!_refreshing && !string.IsNullOrEmpty(value))
            ProcessText = value;
    }

    /// <summary>Kural listesini, profil seçeneklerini (dil / profil değişince) ve çalışan uygulamaları yeniden okur.</summary>
    public void Refresh()
    {
        _refreshing = true;
        try
        {
            var selectedId = SelectedProfile?.Id;
            ProfileOptions.Clear();
            foreach (var profile in _profiles.List())
                ProfileOptions.Add(new RuleProfileOption(profile.Id, profile.Name));
            SelectedProfile = ProfileOptions.FirstOrDefault(p => p.Id == selectedId) ?? ProfileOptions.FirstOrDefault();

            Items.Clear();
            var drafts = _rules.LoadDrafts();
            for (var i = 0; i < drafts.Count; i++)
            {
                var name = ProfileOptions.FirstOrDefault(p => p.Id == drafts[i].ProfileId)?.Name ?? Strings.Get("RulesMissingProfile");
                Items.Add(new RuleItemViewModel(this, i, drafts[i], name));
            }
        }
        finally
        {
            _refreshing = false;
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasProfiles));
    }

    /// <summary>Penceresi olan çalışan uygulamaların exe adları (bir kez, düğmeyle; yoklama yok).</summary>
    [RelayCommand]
    private void RefreshRunningApps()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle != 0 && process.Id != Environment.ProcessId)
                        names.Add(process.ProcessName);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Süreç bu sırada kapandı ya da erişilemiyor: listeye alınmaz.
                }
            }
        }

        RunningApps.Clear();
        foreach (var name in names)
            RunningApps.Add(name);
    }

    [RelayCommand]
    private void Add()
    {
        var (result, saved) = _rules.Edit(rules => RuleListEditor.AddProcessRule(rules, ProcessText, SelectedProfile?.Id));
        if (!result.IsSuccess)
        {
            Report("RulesError" + result.Error, error: true);
            return;
        }

        if (!saved)
        {
            Report("RulesSaveFailed", error: true);
            return;
        }

        ProcessText = "";
        Refresh();
        Report("RulesAdded");
    }

    internal void SetEnabled(RuleItemViewModel item, bool enabled)
    {
        if (_refreshing)
            return;
        var (result, saved) = _rules.Edit(rules => RuleListEditor.SetEnabled(rules, item.Index, enabled));
        if (!result.IsSuccess || !saved)
        {
            Report(result.IsSuccess ? "RulesSaveFailed" : "RulesError" + result.Error, error: true);
            Refresh();
        }
    }

    internal void Delete(RuleItemViewModel item)
    {
        var (result, saved) = _rules.Edit(rules => RuleListEditor.Delete(rules, item.Index));
        if (!result.IsSuccess || !saved)
            Report(result.IsSuccess ? "RulesSaveFailed" : "RulesError" + result.Error, error: true);
        Refresh();
    }

    private void Report(string key, bool error = false)
    {
        StatusText = Strings.Get(key);
        StatusIsError = error;
    }
}
