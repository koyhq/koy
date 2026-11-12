using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Clipboard;

namespace Koy.App.Features.ClipboardPanel;

/// <summary>Pano sayfasının süzgeç düğmesi (Tümü / Sabit / Bağlantı / Renk / Kod).</summary>
public sealed partial class ClipboardFilterViewModel(ClipboardFilter filter, string label) : ObservableObject
{
    public ClipboardFilter Filter { get; } = filter;

    public string Label { get; } = label;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Listedeki tek pano kartı. Değişmez bir görünüm; sabitleme ve silme komutları üst modelde.</summary>
public sealed partial class ClipboardItemViewModel : ObservableObject
{
    public ClipboardItemViewModel(ClipboardItem item, DateTimeOffset now)
    {
        Id = item.Id;
        IsPinned = item.Pinned;
        Kind = item.Kind;
        Preview = ClipboardDisplay.Preview(item.Text);

        var kind = Strings.Get(item.Kind switch
        {
            ClipboardKind.Link => "ClipKindLink",
            ClipboardKind.Color => "ClipKindColor",
            ClipboardKind.Code => "ClipKindCode",
            _ => "ClipKindText"
        });
        Header = kind + " · " + (item.Pinned ? Strings.Get("ClipPinnedLabel") : FormatAge(item.CopiedAt, now));

        if (item.Kind == ClipboardKind.Color && ClipboardDisplay.TryParseColor(item.Text, out var r, out var g, out var b, out var a))
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            Swatch = brush;
        }
    }

    public Guid Id { get; }

    /// <summary>Sabitle / Sabitlemeyi kaldır ipucu.</summary>
    public string PinTip => Strings.Get(IsPinned ? "ClipUnpin" : "ClipPin");

    /// <summary>Az önce geri kopyalandı: başlıkta kısa süre "Kopyalandı" yazar.</summary>
    [ObservableProperty]
    private bool _isCopied;

    public bool IsPinned { get; }

    public ClipboardKind Kind { get; }

    /// <summary>"Bağlantı · sabit", "Renk · 2 dk".</summary>
    public string Header { get; }

    /// <summary>Metnin ilk satırları (renkte renk metninin kendisi).</summary>
    public string Preview { get; }

    /// <summary>Renk öğesinde örnek; diğerlerinde null.</summary>
    public Brush? Swatch { get; }

    public bool HasSwatch => Swatch is not null;

    public bool IsLink => Kind == ClipboardKind.Link;

    private static string FormatAge(DateTimeOffset copiedAt, DateTimeOffset now)
    {
        var (unit, value) = ClipboardDisplay.Age(copiedAt, now);
        return unit switch
        {
            AgeUnit.Minutes => string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("AgeMinutes"), value),
            AgeUnit.Hours => string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("AgeHours"), value),
            AgeUnit.Days => string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("AgeDays"), value),
            _ => Strings.Get("AgeNow")
        };
    }
}

/// <summary>
/// Pano sayfası: arama, süzgeç, kart listesi, geri kopyala / sabitle / sil / temizle. Servis NotchFeature'da (ayar
/// açıksa) alınır; bu model yalnız sayfa görünürken servis olayına abone olur, böylece gizliyken iş yapmaz.
/// <see cref="IClipboardHistoryService.Changed"/> arka plan iş parçacığından gelir; Dispatcher'a geçirilir.
/// </summary>
public sealed partial class ClipboardViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan CopiedFlashDuration = TimeSpan.FromMilliseconds(1400);

    private readonly IClipboardHistoryService _service;
    private readonly Dispatcher _dispatcher;
    private readonly Func<DateTimeOffset> _now;
    private DispatcherTimer? _flashTimer;
    private bool _active;
    private bool _disposed;

    public ClipboardViewModel(IClipboardHistoryService service, Dispatcher? dispatcher = null, Func<DateTimeOffset>? now = null)
    {
        _service = service;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _now = now ?? (() => DateTimeOffset.Now);
        Filters =
        [
            new ClipboardFilterViewModel(ClipboardFilter.All, Strings.Get("ClipFilterAll")) { IsSelected = true },
            new ClipboardFilterViewModel(ClipboardFilter.Pinned, Strings.Get("ClipFilterPinned")),
            new ClipboardFilterViewModel(ClipboardFilter.Link, Strings.Get("ClipFilterLink")),
            new ClipboardFilterViewModel(ClipboardFilter.Color, Strings.Get("ClipFilterColor")),
            new ClipboardFilterViewModel(ClipboardFilter.Code, Strings.Get("ClipFilterCode"))
        ];
    }

    public ObservableCollection<ClipboardItemViewModel> Items { get; } = [];

    public IReadOnlyList<ClipboardFilterViewModel> Filters { get; }

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private ClipboardFilter _filter = ClipboardFilter.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    private bool _isEmpty = true;

    /// <summary>Liste boşken yazı: geçmiş hiç yok / süzgeç ve arama eşleşmedi.</summary>
    [ObservableProperty]
    private string _emptyText = "";

    private ClipboardItemViewModel? _copiedItem;

    public bool ShowEmpty => IsEmpty;

    partial void OnSearchTextChanged(string value) => Reload();

    partial void OnFilterChanged(ClipboardFilter value)
    {
        foreach (var f in Filters)
            f.IsSelected = f.Filter == value;
        Reload();
    }

    /// <summary>Sayfa görünürken true: olaya abone olur ve listeyi tazeler; false: aboneliği keser.</summary>
    public void SetActive(bool active)
    {
        if (_disposed || active == _active)
            return;

        _active = active;
        if (active)
        {
            _service.Changed += OnServiceChanged;
            Reload();
        }
        else
        {
            _service.Changed -= OnServiceChanged;
            _flashTimer?.Stop();
            ClearFlash();
        }
    }

    private void OnServiceChanged(object? sender, EventArgs e) => _dispatcher.InvokeAsync(() =>
    {
        if (!_disposed && _active)
            Reload();
    });

    private void Reload()
    {
        var all = _service.Query(Filter, string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim());
        var now = _now();

        Items.Clear();
        foreach (var item in all)
            Items.Add(new ClipboardItemViewModel(item, now));

        IsEmpty = Items.Count == 0;
        var filtering = Filter != ClipboardFilter.All || !string.IsNullOrWhiteSpace(SearchText);
        EmptyText = Strings.Get(filtering ? "ClipNoMatch" : "ClipEmpty");
    }

    [RelayCommand]
    private void SetFilter(ClipboardFilter filter) => Filter = filter;

    /// <summary>Karta tıklayınca öğe panoya geri yazılır (listeye yeniden eklenmez, sıra değişmez).</summary>
    [RelayCommand]
    private void CopyBack(ClipboardItemViewModel? item)
    {
        if (item is null || !_service.CopyBack(item.Id))
            return;

        ClearFlash();
        item.IsCopied = true;
        _copiedItem = item;
        _flashTimer ??= CreateFlashTimer();
        _flashTimer.Stop();
        _flashTimer.Start();
    }

    [RelayCommand]
    private void TogglePin(ClipboardItemViewModel? item)
    {
        if (item is not null)
            _service.SetPinned(item.Id, !item.IsPinned);
    }

    [RelayCommand]
    private void Remove(ClipboardItemViewModel? item)
    {
        if (item is not null)
            _service.Remove(item.Id);
    }

    /// <summary>Sabitlenmeyenleri siler.</summary>
    [RelayCommand]
    private void Clear() => _service.Clear();

    private void ClearFlash()
    {
        if (_copiedItem is not null)
            _copiedItem.IsCopied = false;
        _copiedItem = null;
    }

    private DispatcherTimer CreateFlashTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = CopiedFlashDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ClearFlash();
        };
        return timer;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        SetActive(false);
        _disposed = true;
        _flashTimer = null;
    }
}
