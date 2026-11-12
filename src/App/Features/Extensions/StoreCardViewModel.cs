using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Extensions.Store;

namespace Koy.App.Features.Extensions;

/// <summary>
/// Mağaza listesinde bir kart. Veriyi <see cref="StoreEntryView"/> verir (Core), düğmelerin hangisinin görüneceği buradan
/// çıkar; işi sahibi olan <see cref="ExtensionStoreViewModel"/> yapar. Önizleme görseli yalnız kart listede ve sayfa
/// görünürken tutulur (sayfa kapanınca bırakılır).
/// </summary>
public sealed partial class StoreCardViewModel : ObservableObject
{
    private readonly ExtensionStoreViewModel _owner;

    public StoreCardViewModel(ExtensionStoreViewModel owner, StoreEntryView view, bool isActive)
    {
        _owner = owner;
        Id = view.Id;
        Update(view, isActive);
    }

    public string Id { get; }

    public ExtensionKind Kind { get; private set; }

    /// <summary>İndeks öğesi; dosyadan kurulan eklentide null.</summary>
    public StoreItem? Item { get; private set; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _metaText = "";

    [ObservableProperty]
    private string _kindLabel = "";

    [ObservableProperty]
    private string _versionText = "";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _detailsText = "";

    [ObservableProperty]
    private string? _homepage;

    [ObservableProperty]
    private Geometry? _kindIcon;

    // --- Durum ve düğmeler ---

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showInstall;

    [ObservableProperty]
    private bool _showUpdate;

    [ObservableProperty]
    private bool _showApply;

    [ObservableProperty]
    private bool _isApplied;

    [ObservableProperty]
    private bool _showUninstall;

    [ObservableProperty]
    private string _applyLabel = "";

    /// <summary>İndirme ilerlemesi 0..1.</summary>
    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isUninstallConfirming;

    public bool ShowUninstallButton => ShowUninstall && !IsUninstallConfirming;

    public bool ShowUninstallConfirm => ShowUninstall && IsUninstallConfirming;

    partial void OnShowUninstallChanged(bool value) => NotifyUninstallParts();

    partial void OnIsUninstallConfirmingChanged(bool value) => NotifyUninstallParts();

    private void NotifyUninstallParts()
    {
        OnPropertyChanged(nameof(ShowUninstallButton));
        OnPropertyChanged(nameof(ShowUninstallConfirm));
    }

    [ObservableProperty]
    private bool _isExpanded;

    // --- Kartın kendi iletisi (son eylemin sonucu) ---

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private bool _messageIsError;

    public bool HasMessage => Message.Length > 0;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    public bool HasHomepage => !string.IsNullOrEmpty(Homepage);

    partial void OnHomepageChanged(string? value) => OnPropertyChanged(nameof(HasHomepage));

    public bool HasDescription => Description.Length > 0;

    partial void OnDescriptionChanged(string value) => OnPropertyChanged(nameof(HasDescription));

    public bool HasSummary => Summary.Length > 0;

    partial void OnSummaryChanged(string value) => OnPropertyChanged(nameof(HasSummary));

    // --- Önizleme ---

    [ObservableProperty]
    private ImageSource? _preview;

    public bool HasPreview => Preview is not null;

    partial void OnPreviewChanged(ImageSource? value) => OnPropertyChanged(nameof(HasPreview));

    /// <summary>İndirme ya da önbellek okuması başlatıldı (aynı kart için iki kez istenmesin).</summary>
    internal bool PreviewRequested { get; set; }

    internal bool NeedsPreview => Preview is null && !PreviewRequested && Item?.Preview is not null;

    internal void ClearPreview()
    {
        Preview = null;
        PreviewRequested = false;
    }

    /// <summary>Satırı yeni görünümle tazeler. Kullanıcının açtığı ayrıntı, ilerleme ve ileti korunur.</summary>
    internal void Update(StoreEntryView view, bool isActive)
    {
        Kind = view.Kind;
        Item = view.Item;

        Name = view.Name;
        KindLabel = Strings.Get("ExtKind" + view.Kind);
        KindIcon = Application.Current?.TryFindResource(KindIconKey(view.Kind)) as Geometry;
        MetaText = string.Join(" · ", new[] { view.Author, VersionOf(view), view.License }.Where(s => s.Length > 0));
        VersionText = VersionOf(view);
        Summary = view.Summary.Length > 0 || view.Item is not null
            ? view.Summary
            : Strings.Get("ExtInstalledFromFile");
        Description = view.Description;
        Homepage = view.Item?.Homepage;
        DetailsText = DetailsOf(view);

        var busy = view.Status == StoreItemStatus.Installing;
        IsBusy = busy;
        IsApplied = isActive && !busy;
        ShowInstall = !busy && !view.IsInstalled && view.Item is not null;
        ShowUpdate = !busy && view.HasUpdate && view.Item is not null;
        ShowApply = !busy && view.IsInstalled && !IsApplied;
        ShowUninstall = !busy && view.IsInstalled;
        ApplyLabel = Strings.Get(view.Kind == ExtensionKind.Profile ? "ExtAddToProfiles" : "ExtApply");
        if (!busy && !view.IsInstalled)
            IsUninstallConfirming = false;
    }

    private static string VersionOf(StoreEntryView view)
    {
        if (view.IsInstalled && view.HasUpdate && view.AvailableVersion is { } available)
            return string.Format(CultureInfo.CurrentCulture, Strings.Get("ExtVersionUpdateFormat"), view.InstalledVersion, available);

        var version = view.InstalledVersion ?? view.AvailableVersion;
        return version is null ? "" : string.Format(CultureInfo.CurrentCulture, Strings.Get("ExtVersionFormat"), version);
    }

    private static string DetailsOf(StoreEntryView view)
    {
        var lines = new List<string>();
        if (view.IsInstalled && view.InstalledVersion is { } installed)
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.Get("ExtInstalledVersion"), installed));
        if (view.Item is { } item)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.Get("ExtUpdatedFormat"), item.Updated.ToString("d", CultureInfo.CurrentCulture)));
            if (item.Downloads > 0)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.Get("ExtDownloadsFormat"), item.Downloads));
        }

        return string.Join(" · ", lines);
    }

    private static string KindIconKey(ExtensionKind kind) => kind switch
    {
        ExtensionKind.Theme => "IconPalette",
        ExtensionKind.IconPack => "IconAppWindow",
        ExtensionKind.Background => "IconPlay",
        _ => "IconSettings",
    };

    // --- Komutlar ---

    [RelayCommand]
    private void Install() => _owner.RequestInstall(this, update: false);

    [RelayCommand]
    private void UpdateExtension() => _owner.RequestInstall(this, update: true);

    [RelayCommand]
    private void Apply() => _owner.Apply(this);

    [RelayCommand]
    private void RequestUninstall() => IsUninstallConfirming = true;

    [RelayCommand]
    private void CancelUninstall() => IsUninstallConfirming = false;

    [RelayCommand]
    private Task ConfirmUninstallAsync()
    {
        IsUninstallConfirming = false;
        return _owner.UninstallAsync(this);
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void OpenHomepage() => _owner.OpenHomepage(this);

    internal void Say(string text, bool error)
    {
        Message = text;
        MessageIsError = error;
    }

    internal void RefreshLanguage(StoreEntryView view, bool isActive) => Update(view, isActive);
}
