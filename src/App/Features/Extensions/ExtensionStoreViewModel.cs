using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Settings;
using Koy.App.Resources;
using Koy.Core.Appearance;
using Koy.Core.Diagnostics;
using Koy.Core.Extensions.Store;
using Koy.Core.Settings;
using Koy.Core.SystemStats;
using Koy.Platform.Extensions;

namespace Koy.App.Features.Extensions;

/// <summary>Sıralama seçeneği; adı dil değişince yeniden okunur (öğe örnekleri sabit kalır, seçim bozulmaz).</summary>
public sealed partial class StoreSortOption(string key) : ObservableObject
{
    public string Key { get; } = key;

    public string Label => Strings.Get("ExtSort" + Key);

    public void RefreshLabel() => OnPropertyChanged(nameof(Label));
}

/// <summary>Kurulum onayındaki bir satır (etiket / değer).</summary>
public sealed record ExtensionConfirmFact(string Label, string Value);

/// <summary>
/// Eklentiler sayfası (Koy penceresi): mağaza indeksini ve kurulu eklentileri listeler, kurar, günceller, uygular ve
/// kaldırır. Mantığın hepsi Core'dadır (<see cref="StoreListState"/>, <see cref="ExtensionApplyPlanner"/>, kurulum
/// denetimleri); ağ ve dosya işi <see cref="ExtensionStoreClient"/> ile <see cref="StorePreviewLoader"/> içindedir. Burası
/// durumu, onay adımını ve metinleri bağlar. Sayfa görünürken indeksi bir kez çeker; arka planda yoklama yoktur
/// (AGENTS #5). Hepsi arayüz iş parçacığında çağrılır.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Tek örnek KoyWindow'un ömrü kadar (süreç boyu) yaşar.")]
public sealed partial class ExtensionStoreViewModel : ObservableObject
{
    /// <summary>Listede bir seferde gösterilen kart sayısı ("Daha fazla göster" bu kadar ekler).</summary>
    public const int PageSize = 20;

    private const int PreviewDecodePixels = 320;
    private const int MaxParallelPreviews = 3;

    private readonly ExtensionStoreClient _client;
    private readonly StorePreviewLoader _previews;
    private readonly ProfileService _profiles;
    private readonly ExtensionFileActionRunner _runner;
    private readonly Dictionary<string, StoreCardViewModel> _cards = new(StringComparer.Ordinal);
    private readonly HashSet<string> _busy = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _previewGate = new(MaxParallelPreviews);

    private StoreListState _state = StoreListState.Initial;
    private AppSettings _settings = new();
    private bool _active;
    private int _displayCount = PageSize;
    private int _skippedItems;
    private bool _isLoadingConfig;
    private CancellationTokenSource? _indexCts;
    private CancellationTokenSource? _previewCts;
    private Func<Task>? _pendingInstall;

    public ExtensionStoreViewModel(
        ExtensionStoreClient client, StorePreviewLoader previews, ProfileService profiles, string? userStyleFolder = null)
    {
        _client = client;
        _previews = previews;
        _profiles = profiles;
        _runner = new ExtensionFileActionRunner(userStyleFolder ?? UserStyleFile.DefaultFolder, profiles);
        foreach (var key in new[] { "Newest", "Alphabetical", "MostDownloads" })
            SortOptions.Add(new StoreSortOption(key));
    }

    /// <summary>Gerçek mağaza adresi, gerçek ayar klasörleri ve ortak HTTP istemcisi.</summary>
    public static ExtensionStoreViewModel CreateDefault() =>
        new(new ExtensionStoreClient(), new StorePreviewLoader(), ProfileService.Instance);

    /// <summary>Profil kitaplığına bir profil eklendi (Genel sayfasındaki liste tazelensin).</summary>
    public event Action? ProfilesChanged;

    public ObservableCollection<StoreCardViewModel> Cards { get; } = [];

    public ObservableCollection<StoreSortOption> SortOptions { get; } = [];

    // --- Sorgu (kategori, süzgeç, sıralama, arama) ---

    [ObservableProperty]
    private string _searchText = "";

    /// <summary>All, Theme, IconPack, Background, Profile.</summary>
    [ObservableProperty]
    private string _categoryKey = "All";

    /// <summary>All, Installed, Updates.</summary>
    [ObservableProperty]
    private string _filterKey = "All";

    /// <summary>Newest, Alphabetical, MostDownloads.</summary>
    [ObservableProperty]
    private string _sortKey = "Newest";

    partial void OnSearchTextChanged(string value) => QueryChanged();

    partial void OnCategoryKeyChanged(string value) => QueryChanged();

    partial void OnFilterKeyChanged(string value) => QueryChanged();

    partial void OnSortKeyChanged(string value) => QueryChanged();

    private void QueryChanged()
    {
        if (_isLoadingConfig)
            return;

        _displayCount = PageSize;
        Rebuild();
        LoadPreviews();
    }

    /// <summary>Başka bir sayfadaki "Mağazadan daha fazla al…" bağlantısı: sayfayı bu kategoriyle açar.</summary>
    public void ShowCategory(string? category)
    {
        _isLoadingConfig = true;
        try
        {
            SearchText = "";
            FilterKey = "All";
            CategoryKey = Enum.TryParse<ExtensionKind>(category, out var kind) && Enum.IsDefined(kind) ? kind.ToString() : "All";
        }
        finally
        {
            _isLoadingConfig = false;
        }

        _displayCount = PageSize;
        Rebuild();
        LoadPreviews();
    }

    // --- Görünür durum ---

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _indexErrorText = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private string _updatesLabel = Strings.Get("ExtFilterUpdates");

    [ObservableProperty]
    private string _countText = "";

    [ObservableProperty]
    private string _showMoreText = "";

    [ObservableProperty]
    private bool _isInstallingFile;

    public bool HasIndexError => IndexErrorText.Length > 0;

    public bool HasStatus => StatusText.Length > 0;

    public bool ShowLoading => IsLoading && Cards.Count == 0;

    // Katalog alınamadıysa hata kutusu zaten söylüyor; ayrıca "eşleşen yok" yazılmaz.
    public bool ShowEmpty => !IsLoading && Cards.Count == 0 && !HasIndexError;

    public bool HasMore => _state.Visible().Count > Cards.Count;

    public bool CanInstallFromFile => !IsInstallingFile;

    partial void OnIndexErrorTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasIndexError));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsLoadingChanged(bool value) => NotifyListStates();

    partial void OnIsInstallingFileChanged(bool value) => OnPropertyChanged(nameof(CanInstallFromFile));

    private void NotifyListStates()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(HasMore));
    }

    // --- Yaşam döngüsü ---

    /// <summary>
    /// Sayfa görünürken true: kayıt ve ayarlar okunur, indeks henüz çekilmediyse (ya da önceki deneme başarısızsa) bir kez
    /// çekilir, görünen kartların önizlemesi istenir. false: bekleyen indirmeler iptal edilir, görseller bırakılır.
    /// </summary>
    public void SetActive(bool active)
    {
        if (active == _active)
            return;

        _active = active;
        if (active)
        {
            _settings = _profiles.CurrentSettings();
            _state = _state with { Registry = _client.Registry.Load() };
            Rebuild();
            if (_state.Index is null && !IsLoading)
                _ = LoadIndexAsync();
            else
                LoadPreviews();
        }
        else
        {
            _indexCts?.Cancel();
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = null;
            foreach (var card in _cards.Values)
                card.ClearPreview();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => IsLoading ? Task.CompletedTask : LoadIndexAsync();

    private async Task LoadIndexAsync()
    {
        _indexCts?.Dispose();
        _indexCts = new CancellationTokenSource();
        var token = _indexCts.Token;

        IsLoading = true;
        IndexErrorText = "";
        try
        {
            var result = await _client.FetchIndexAsync(token);
            if (result.IsSuccess)
            {
                _skippedItems = result.SkippedItems;
                _state = _state with { Index = result.Index, Registry = _client.Registry.Load() };
            }
            else if (result.Error != StoreIndexError.Cancelled)
            {
                IndexErrorText = Strings.Get("ExtIndexError" + result.Error);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fire-and-forget çağrılır: yakalanmayan istisna görev içinde kaybolurdu.
            CrashLog.Write("ExtensionStoreViewModel.LoadIndex", ex);
            IndexErrorText = Strings.Get("ExtUnexpectedError");
        }
        finally
        {
            IsLoading = false;
        }

        Rebuild();
        LoadPreviews();
    }

    // --- Liste ---

    private StoreQuery BuildQuery() => new(
        Enum.TryParse<StoreFilter>(FilterKey, out var filter) && Enum.IsDefined(filter) ? filter : StoreFilter.All,
        Enum.TryParse<ExtensionKind>(CategoryKey, out var kind) && Enum.IsDefined(kind) ? kind : null,
        SearchText,
        Enum.TryParse<StoreSort>(SortKey, out var sort) && Enum.IsDefined(sort) ? sort : StoreSort.Newest);

    private void Rebuild()
    {
        _state = _state with { Query = BuildQuery(), Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName };
        var all = _state.Visible();
        var wanted = new List<StoreCardViewModel>();
        foreach (var view in all.Take(_displayCount))
        {
            var active = IsActive(view);
            if (_cards.TryGetValue(view.Id, out var card))
            {
                card.Update(view, active);
            }
            else
            {
                card = new StoreCardViewModel(this, view, active);
                _cards[view.Id] = card;
            }

            wanted.Add(card);
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < Cards.Count && ReferenceEquals(Cards[i], wanted[i]))
                continue;

            var at = Cards.IndexOf(wanted[i]);
            if (at > i)
                Cards.Move(at, i);
            else
                Cards.Insert(i, wanted[i]);
        }

        while (Cards.Count > wanted.Count)
        {
            Cards[^1].ClearPreview();
            Cards.RemoveAt(Cards.Count - 1);
        }

        // Listeden düşenlerin görseli bırakılır; kartlar (ileti, ilerleme) saklanır.
        foreach (var card in _cards.Values)
        {
            if (!wanted.Contains(card))
                card.ClearPreview();
        }

        var updates = _state.UpdateCount;
        UpdatesLabel = updates > 0
            ? string.Format(Strings.Get("ExtFilterUpdatesCount"), updates)
            : Strings.Get("ExtFilterUpdates");
        CountText = _state.Index is null && all.Count == 0 ? "" : BuildCountText(all.Count);
        var remaining = all.Count - Cards.Count;
        ShowMoreText = string.Format(Strings.Get("ExtShowMore"), Math.Min(remaining, PageSize));
        NotifyListStates();
    }

    private string BuildCountText(int visible)
    {
        var text = string.Format(Strings.Get("ExtCountFormat"), visible);
        return _skippedItems > 0 ? text + " · " + string.Format(Strings.Get("ExtSkippedFormat"), _skippedItems) : text;
    }

    [RelayCommand]
    private void ShowMore()
    {
        _displayCount += PageSize;
        Rebuild();
        LoadPreviews();
    }

    private bool IsActive(StoreEntryView view) =>
        view.Installed is { } entry
        && ExtensionPaths.FolderFor(view.Id, _client.ExtensionsFolder) is { } folder
        && ExtensionApplyPlanner.IsActive(_settings, entry, folder);

    private void ReloadRegistry()
    {
        _settings = _profiles.CurrentSettings();
        _state = _state with { Registry = _client.Registry.Load(), BusyIds = new HashSet<string>(_busy, StringComparer.Ordinal) };
        Rebuild();
    }

    private void SetBusy(string id, bool busy)
    {
        if (busy)
            _busy.Add(id);
        else
            _busy.Remove(id);
        ReloadRegistry();
    }

    /// <summary>Dil değişti: metinler baştan kurulur.</summary>
    public void RefreshLanguage()
    {
        foreach (var option in SortOptions)
            option.RefreshLabel();
        Rebuild();
    }

    // --- Önizleme ---

    private void LoadPreviews()
    {
        if (!_active)
            return;

        _previewCts ??= new CancellationTokenSource();
        var token = _previewCts.Token;
        foreach (var card in Cards.ToList())
        {
            if (card.NeedsPreview)
            {
                card.PreviewRequested = true;
                _ = LoadPreviewAsync(card, token);
            }
        }
    }

    private async Task LoadPreviewAsync(StoreCardViewModel card, CancellationToken token)
    {
        try
        {
            await _previewGate.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            card.PreviewRequested = false;
            return;
        }

        try
        {
            var bytes = card.Item?.Preview is { } preview ? await _previews.LoadAsync(preview, token) : null;
            if (token.IsCancellationRequested || !Cards.Contains(card))
            {
                card.PreviewRequested = false;
                return;
            }

            // Görünür boyutta çözülür ve dondurulur (AGENTS #9); çözülemeyen görsel kartı bozmaz.
            card.Preview = bytes is null ? null : Decode(bytes);
        }
        finally
        {
            _previewGate.Release();
        }
    }

    private static BitmapImage? Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = PreviewDecodePixels;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidOperationException
                                       or ArgumentException or FormatException
                                       or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    // --- Kurulum onayı ---

    [ObservableProperty]
    private bool _isConfirming;

    [ObservableProperty]
    private string _confirmTitle = "";

    [ObservableProperty]
    private string _confirmButton = "";

    [ObservableProperty]
    private string _confirmFileNote = "";

    [ObservableProperty]
    private IReadOnlyList<ExtensionConfirmFact> _confirmFacts = [];

    public bool HasConfirmFileNote => ConfirmFileNote.Length > 0;

    partial void OnConfirmFileNoteChanged(string value) => OnPropertyChanged(nameof(HasConfirmFileNote));

    private void OpenConfirm(string title, string button, string note, IReadOnlyList<ExtensionConfirmFact> facts, Func<Task> run)
    {
        ConfirmTitle = title;
        ConfirmButton = button;
        ConfirmFileNote = note;
        ConfirmFacts = facts;
        _pendingInstall = run;
        IsConfirming = true;
    }

    private static IReadOnlyList<ExtensionConfirmFact> FactsOf(
        ExtensionKind kind, string version, string author, string license, long size, bool fromFile) =>
    [
        new(Strings.Get("ExtConfirmKind"), Strings.Get("ExtKind" + kind)),
        new(Strings.Get("ExtConfirmVersion"), version),
        new(Strings.Get("ExtConfirmAuthor"), author),
        new(Strings.Get("ExtConfirmLicense"), license),
        new(Strings.Get(fromFile ? "ExtConfirmFileSize" : "ExtConfirmSize"), SystemFormat.Size(size, CultureInfo.CurrentUICulture)),
        // Veri paketleri yetki istemez (yetki isteyen paket reddedilir); liste her zaman boştur ve yine de gösterilir.
        new(Strings.Get("ExtConfirmPermissions"), Strings.Get("ExtConfirmNoPermissions")),
    ];

    [RelayCommand]
    private async Task ConfirmInstallAsync()
    {
        IsConfirming = false;
        var run = _pendingInstall;
        _pendingInstall = null;
        if (run is not null)
            await run();
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        IsConfirming = false;
        _pendingInstall = null;
    }

    // --- Kur / Güncelle ---

    internal void RequestInstall(StoreCardViewModel card, bool update)
    {
        if (card.Item is not { } item)
            return;

        card.Say("", false);
        var title = string.Format(Strings.Get(update ? "ExtConfirmUpdateTitle" : "ExtConfirmInstallTitle"), item.Name);
        OpenConfirm(
            title,
            Strings.Get(update ? "ExtUpdate" : "ExtInstall"),
            "",
            FactsOf(item.Kind, item.Version, item.Author, item.License, item.Package.Size, fromFile: false),
            () => InstallFromStoreAsync(card, item, update));
    }

    private async Task InstallFromStoreAsync(StoreCardViewModel card, StoreItem item, bool update)
    {
        card.Progress = 0;
        SetBusy(item.Id, true);
        var progress = new Progress<double>(value => card.Progress = value);

        try
        {
            InstallResult result;
            try
            {
                result = await _client.InstallFromStoreAsync(item, progress, CancellationToken.None);
            }
            finally
            {
                SetBusy(item.Id, false);
            }

            if (!result.IsSuccess)
            {
                card.Say(Strings.Get("ExtInstallError" + result.Error), true);
                return;
            }

            FinishInstall(card, result, update);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CrashLog.Write("ExtensionStoreViewModel.InstallFromStore", ex);
            Report(card, Strings.Get("ExtUnexpectedError"), true);
        }
    }

    // Kurulum bitti: güncellenen eklenti uygulanmışsa yeni dosyalar ayara/kullanıcı dosyasına yansısın diye Uygula yeniden çalışır.
    private void FinishInstall(StoreCardViewModel? card, InstallResult result, bool update)
    {
        var entry = result.Entry!;
        var name = entry.Name;
        if (result.PreviouslyApplied)
        {
            var applied = ApplyCore(entry.Id);
            if (applied.ErrorKey is { } error)
            {
                Report(card, Strings.Get(error), true);
                return;
            }

            Report(card, string.Format(Strings.Get("ExtUpdatedAndAppliedOk"), name), false);
            return;
        }

        ReloadRegistry();
        Report(card, string.Format(Strings.Get(update ? "ExtUpdatedOk" : "ExtInstalledOk"), name), false);
    }

    // --- Dosyadan kur ---

    [RelayCommand]
    private void InstallFromFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Get("ExtFileDialogTitle"),
            Filter = Strings.Get("ExtFileFilter"),
            CheckFileExists = true,
        };

        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if ((owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog()) != true)
            return;

        var path = dialog.FileName;
        var peek = PeekPackage(path);
        if (peek.Error != ExtensionInstallError.None)
        {
            StatusText = Strings.Get("ExtInstallError" + peek.Error);
            StatusIsError = true;
            return;
        }

        StatusText = "";
        var manifest = peek.Manifest!;
        OpenConfirm(
            string.Format(Strings.Get("ExtConfirmInstallTitle"), manifest.Manifest.Name),
            Strings.Get("ExtInstall"),
            Strings.Get("ExtConfirmFileNote"),
            FactsOf(manifest.Kind, manifest.Manifest.Version, manifest.Manifest.Author, manifest.License, peek.Size, fromFile: true),
            () => InstallFileAsync(path));
    }

    private async Task InstallFileAsync(string path)
    {
        IsInstallingFile = true;
        StatusText = Strings.Get("ExtInstalling");
        StatusIsError = false;
        try
        {
            var result = await _client.InstallFromFileAsync(path, CancellationToken.None);
            if (!result.IsSuccess)
            {
                StatusText = Strings.Get("ExtInstallError" + result.Error);
                StatusIsError = true;
                return;
            }

            FinishInstall(null, result, update: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CrashLog.Write("ExtensionStoreViewModel.InstallFile", ex);
            StatusText = Strings.Get("ExtUnexpectedError");
            StatusIsError = true;
        }
        finally
        {
            IsInstallingFile = false;
        }
    }

    private sealed record PackagePeek(ExtensionInstallError Error, PackageManifest? Manifest, long Size);

    // Onay penceresi gerçek bilgiyi göstersin diye yalnız manifest okunur; asıl denetim kurulumda yapılır.
    private static PackagePeek PeekPackage(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return new(ExtensionInstallError.FileNotFound, null, 0);
            if (info.Length > PackageEntryPolicy.MaxDownloadBytes)
                return new(ExtensionInstallError.DownloadTooLarge, null, info.Length);

            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry(PackageEntryPolicy.ManifestName);
            if (entry is null)
                return new(ExtensionInstallError.MissingManifest, null, info.Length);
            if (entry.Length > PackageEntryPolicy.MaxManifestBytes)
                return new(ExtensionInstallError.FileTooLarge, null, info.Length);

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var parsed = PackageManifestParser.Parse(ms.ToArray());
            return parsed.IsValid
                ? new(ExtensionInstallError.None, parsed.Manifest, info.Length)
                : new(parsed.Error, null, info.Length);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            return new(ExtensionInstallError.InvalidArchive, null, 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(ExtensionInstallError.IoError, null, 0);
        }
    }

    // --- Uygula ---

    internal void Apply(StoreCardViewModel card)
    {
        card.Say("", false);
        var outcome = ApplyCore(card.Id);
        if (outcome.ErrorKey is { } error)
        {
            Report(card, Strings.Get(error), true);
            return;
        }

        var name = outcome.Entry!.Name;
        if (outcome.Files?.ImportedProfile is not null)
        {
            Report(card, string.Format(Strings.Get(outcome.Files.ProfileAlreadyPresent ? "ExtProfileAlready" : "ExtProfileAdded"), name), false);
            return;
        }

        Report(card, string.Format(Strings.Get("ExtAppliedOk"), name), false);
    }

    private sealed record ApplyOutcome(string? ErrorKey, InstalledEntry? Entry, ExtensionFileActionResult? Files);

    // Sıra: plan → dosya işleri → ayarı kaydet ve canlı uygula → kayıtta "uygulandı" işareti. Bir adım başarısız olursa
    // önceki adımlar geri alınır (kullanıcı stil dosyaları ve ayarlar eski haline döner); yarım durum kalmaz.
    private ApplyOutcome ApplyCore(string id)
    {
        var registry = _client.Registry.Load();
        var entry = registry.Find(id);
        var folder = ExtensionPaths.FolderFor(id, _client.ExtensionsFolder);
        if (entry is null || folder is null)
        {
            ReloadRegistry();
            return new("ExtApplyNotInstalled", null, null);
        }

        var before = _profiles.CurrentSettings();
        var plan = ExtensionApplyPlanner.Apply(before, entry, folder, registry.HasApplied(ExtensionKind.Theme));
        if (!plan.IsSuccess)
            return new("ExtApplyError" + plan.Error, entry, null);

        var snapshot = TakeSnapshotFor(plan);
        var files = _runner.Run(plan.FileActions);
        if (!files.IsSuccess)
        {
            RestoreSnapshot(snapshot);
            return new(files.ErrorKey, entry, files);
        }

        if (!_profiles.CommitSettings(plan.Settings!))
        {
            RestoreSnapshot(snapshot);
            return new("ExtApplySaveFailed", entry, files);
        }

        if (entry.Kind != ExtensionKind.Profile && !_client.Registry.Update(r => r.WithApplied(id)))
        {
            // Kayıt yazılamadı: "uygulandı" işareti olmadan ayar açık kalırsa kaldırma geri almayı atlardı.
            _profiles.CommitSettings(before);
            RestoreSnapshot(snapshot);
            ReloadRegistry();
            return new("ExtRegistrySaveFailed", entry, files);
        }

        if (files.ImportedProfile is not null && !files.ProfileAlreadyPresent)
            ProfilesChanged?.Invoke();

        ReloadRegistry();
        return new(null, entry, files);
    }

    // Kullanıcı stil dosyalarına dokunan plan için geri alma kopyası (diğer planlar dosyaya yazmaz).
    private UserStyleSnapshot? TakeSnapshotFor(ExtensionApplyPlan plan) =>
        plan.FileActions.Any(a => a.Kind != ExtensionFileActionKind.ImportProfile) ? _runner.TakeSnapshot() : null;

    private void RestoreSnapshot(UserStyleSnapshot? snapshot)
    {
        if (snapshot is not null)
            _runner.RestoreSnapshot(snapshot);
    }

    // --- Kaldır ---

    internal async Task UninstallAsync(StoreCardViewModel card)
    {
        var id = card.Id;
        var entry = _client.Registry.Load().Find(id);
        var folder = ExtensionPaths.FolderFor(id, _client.ExtensionsFolder);
        if (entry is null || folder is null)
        {
            ReloadRegistry();
            return;
        }

        card.Say("", false);
        SetBusy(id, true);
        try
        {
            // Önce ayar geri alınır (simge paketi → yerleşik, arka plan → ambiyans, tema → özgün durum), sonra klasör silinir.
            // Klasör silinemezse geri alma geri alınır: eklenti kurulu kalır ve uygulandığı gibi çalışmaya devam eder.
            var before = _profiles.CurrentSettings();
            var plan = ExtensionApplyPlanner.RevertOnUninstall(before, entry, folder, _runner.ReadBackup());
            if (!plan.IsSuccess)
            {
                card.Say(Strings.Get("ExtApplyError" + plan.Error), true);
                return;
            }

            var snapshot = TakeSnapshotFor(plan);
            var files = _runner.Run(plan.FileActions);
            if (!files.IsSuccess)
            {
                RestoreSnapshot(snapshot);
                card.Say(Strings.Get(files.ErrorKey!), true);
                return;
            }

            if (!_profiles.CommitSettings(plan.Settings!))
            {
                RestoreSnapshot(snapshot);
                card.Say(Strings.Get("ExtApplySaveFailed"), true);
                return;
            }

            if (!await _client.UninstallAsync(id))
            {
                // Klasör hâlâ duruyorsa eklenti kurulu kalmıştır: ayarı ve dosyaları eski haline döndür.
                if (Directory.Exists(folder))
                {
                    _profiles.CommitSettings(before);
                    RestoreSnapshot(snapshot);
                }

                card.Say(string.Format(Strings.Get("ExtUninstallFailed"), entry.Name), true);
                return;
            }

            StatusText = string.Format(Strings.Get("ExtUninstalledOk"), entry.Name);
            StatusIsError = false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CrashLog.Write("ExtensionStoreViewModel.Uninstall", ex);
            card.Say(Strings.Get("ExtUnexpectedError"), true);
        }
        finally
        {
            SetBusy(id, false);
        }
    }

    // --- Ayrıntı ---

    internal void OpenHomepage(StoreCardViewModel card)
    {
        // İndeks ayrıştırıcısı yalnız https kabul eder; yine de kabuğa vermeden önce denetlenir.
        if (card.Homepage is not { } url
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            card.Say(Strings.Get("ExtOpenLinkFailed"), true);
        }
    }

    private void Report(StoreCardViewModel? card, string text, bool error)
    {
        if (card is not null)
        {
            // Kartın kendisi yeniden kurulmuş olabilir; ileti kimlikten bulunur.
            (_cards.GetValueOrDefault(card.Id) ?? card).Say(text, error);
            return;
        }

        StatusText = text;
        StatusIsError = error;
    }
}
