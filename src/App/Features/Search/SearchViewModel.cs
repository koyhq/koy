using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Settings;
using Koy.App.Resources;
using Koy.Core.Search;

namespace Koy.App.Features.Search;

/// <summary>Satırdaki eylem düğmesi.</summary>
public sealed partial class SearchActionViewModel(string label, int index) : ObservableObject
{
    public string Label { get; } = label;

    public int Index { get; } = index;

    [ObservableProperty]
    private bool _isActive;
}

/// <summary>Listedeki bir satır.</summary>
public sealed partial class SearchRowViewModel(SearchResult result, string? header, int index) : ObservableObject
{
    public SearchResult Result { get; } = result;

    public int Index { get; } = index;

    public string Title => Result.Title;

    public string? Header { get; } = header;

    public bool HasHeader => Header is not null;

    public SearchGlyph Glyph => Result.Glyph;

    public IReadOnlyList<SearchActionViewModel> Actions { get; } =
        result.Actions.Select((a, i) => new SearchActionViewModel(a.Label, i)).ToList();

    public bool HasActions => Actions.Count > 0;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private ImageSource? _icon;

    public bool HasIcon => Icon is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubtitle))]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private bool _isConfirming;

    public string? Subtitle => IsConfirming
        ? Result.ConfirmationDetail is { } detail ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("SearchConfirmRun"), detail) : Strings.Get("SearchConfirmHint")
        : Result.Subtitle;

    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
}

/// <summary>Çentikteki arama paneli. Durum ve kararlar <see cref="SearchPanelController"/>'da.</summary>
public sealed partial class SearchViewModel : ObservableObject, IDisposable
{
    private const int IconDecodeSize = 48;

    private readonly SearchPanelController _panel;
    private readonly Dispatcher _dispatcher;
    private readonly Action<bool> _savePinned;
    private readonly Dictionary<string, ImageSource?> _icons = [];
    private IReadOnlyList<SearchResult>? _shownResults;
    private bool _syncingText;
    private bool _disposed;

    public SearchViewModel(SearchPanelController panel, Dispatcher dispatcher, bool pinned, Action<bool> savePinned)
    {
        _panel = panel;
        _dispatcher = dispatcher;
        _isPinned = pinned;
        _savePinned = savePinned;
        _panel.Changed += OnPanelChanged;
        _panel.QueryReplaced += OnQueryReplaced;
        _panel.AutoActivated += OnAutoActivated;
    }

    /// <summary>Panel kapanmalı; değer odağın önceki pencereye dönüp dönmeyeceği.</summary>
    public event Action<bool>? CloseRequested;

    /// <summary>İmleç şu konuma gitmeli.</summary>
    public event Action<int>? CaretRequested;

    /// <summary>Seçili satır değişti; görünür yapılmalı.</summary>
    public event Action<int>? SelectionChanged;

    public ObservableCollection<SearchRowViewModel> Rows { get; } = [];

    public bool IsOpen => _panel.IsOpen;

    public SearchHistoryMode HistoryMode => _panel.HistoryMode;

    public string Suggestion => _panel.Suggestion;

    [ObservableProperty]
    private string _queryText = "";

    [ObservableProperty]
    private string _suggestionText = "";

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _hasHistory;

    /// <summary>Kutu boş: gri "Ara…" yazısı.</summary>
    [ObservableProperty]
    private bool _showPlaceholder = true;

    [ObservableProperty]
    private bool _showEmptyHint = true;

    [ObservableProperty]
    private bool _isSearching;

    public void Open(string? initialQuery)
    {
        _panel.Open();
        if (initialQuery is not null)
            _panel.ReplaceQuery(initialQuery, fromClipboard: true);
        SyncText();
        Refresh();
    }

    public void Close()
    {
        _panel.Close();
        _icons.Clear();
    }

    partial void OnQueryTextChanged(string value)
    {
        if (!_syncingText)
            _panel.SetQuery(value);
    }

    partial void OnIsPinnedChanged(bool value) => _savePinned(value);

    public void MoveSelection(int delta) => _panel.MoveSelection(delta);

    public void MoveCategory(int direction) => _panel.MoveCategory(direction);

    public void SelectFirst() => _panel.Select(0);

    public void SelectLast() => _panel.Select(_panel.Results.Count - 1);

    public bool CycleAction(int direction) => _panel.CycleAction(direction);

    public void FocusLastAction() => _panel.FocusLastAction();

    public void ShowHistoryOrMoveDown()
    {
        if (QueryText.Length == 0 && !_panel.ShowingHistory && _panel.HasHistory)
            _panel.ToggleHistory();
        else
            _panel.MoveSelection(1);
    }

    /// <summary>Gri öneriyi kabul eder.</summary>
    public bool AcceptSuggestion()
    {
        var suggestion = _panel.Suggestion;
        if (suggestion.Length <= QueryText.Length)
            return false;

        _panel.ReplaceQuery(suggestion);
        return true;
    }

    /// <summary>Enter. Boş kutuda paneli kapatır.</summary>
    public void Activate(bool firstAction)
    {
        if (QueryText.Length == 0 && !_panel.ShowingHistory)
        {
            CloseRequested?.Invoke(true);
            return;
        }

        Handle(_panel.Activate(firstAction));
    }

    public void ActivateRow(SearchRowViewModel row)
    {
        _panel.Select(row.Index);
        Handle(_panel.Activate());
    }

    public void RunRowAction(SearchRowViewModel row, SearchActionViewModel action)
    {
        _panel.Select(row.Index);
        while (_panel.ActiveAction < action.Index && _panel.CycleAction(1))
        {
        }

        Handle(_panel.Activate());
    }

    [RelayCommand]
    private void RequestClose() => CloseRequested?.Invoke(true);

    [RelayCommand]
    private void ToggleHistory() => _panel.ToggleHistory();

    [RelayCommand]
    private void ToggleHelp() => _panel.ReplaceQuery(QueryText.StartsWith('?') ? "" : "?");

    [RelayCommand]
    private void TogglePin() => IsPinned = !IsPinned;

    [RelayCommand]
    private void OpenSettings()
    {
        CloseRequested?.Invoke(false);
        KoyWindow.ShowInstance(1);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _panel.Changed -= OnPanelChanged;
        _panel.QueryReplaced -= OnQueryReplaced;
        _panel.AutoActivated -= OnAutoActivated;
        _panel.Dispose();
    }

    private void Handle(SearchActivationResult activation)
    {
        if (activation is { Outcome: SearchActivation.Executed, Result: { } result } && !IsPinned)
            CloseRequested?.Invoke(result.RestoresFocus);
    }

    private void OnAutoActivated(object? sender, SearchResult result)
    {
        if (!IsPinned)
            CloseRequested?.Invoke(result.RestoresFocus);
    }

    private void OnQueryReplaced(object? sender, int caret)
    {
        SyncText();
        CaretRequested?.Invoke(caret);
    }

    private void OnPanelChanged(object? sender, EventArgs e) => Refresh();

    private void SyncText()
    {
        _syncingText = true;
        QueryText = _panel.Query;
        _syncingText = false;
    }

    private void Refresh()
    {
        if (!ReferenceEquals(_shownResults, _panel.Results))
            RebuildRows();

        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            row.IsSelected = i == _panel.SelectedIndex;
            row.IsConfirming = row.Result.Id == _panel.PendingConfirmationId;
            foreach (var action in row.Actions)
                action.IsActive = row.IsSelected && action.Index == _panel.ActiveAction;
        }

        var suggestion = _panel.Suggestion;
        SuggestionText = suggestion.Length > QueryText.Length ? QueryText + suggestion[QueryText.Length..] : "";
        HasHistory = _panel.HasHistory;
        ShowPlaceholder = QueryText.Length == 0;
        ShowEmptyHint = Rows.Count == 0 && QueryText.Length == 0;
        IsSearching = Rows.Count == 0 && QueryText.Length > 0 && _panel.IsBusy;
        OnPropertyChanged(nameof(IsOpen));

        if (_panel.SelectedIndex >= 0)
            SelectionChanged?.Invoke(_panel.SelectedIndex);
    }

    private void RebuildRows()
    {
        _shownResults = _panel.Results;
        Rows.Clear();

        string? category = null;
        for (var i = 0; i < _panel.Results.Count; i++)
        {
            var result = _panel.Results[i];
            var header = result.Category != category ? result.Category : null;
            category = result.Category;

            var row = new SearchRowViewModel(result, header, i);
            Rows.Add(row);
            LoadIcon(row);
        }
    }

    private void LoadIcon(SearchRowViewModel row)
    {
        var load = row.Result.LoadIcon;
        if (load is null)
            return;

        var id = row.Result.Id;
        if (_icons.TryGetValue(id, out var cached))
        {
            row.Icon = cached;
            return;
        }

        _ = Task.Run(() => Decode(load)).ContinueWith(task =>
        {
            var image = task.IsCompletedSuccessfully ? task.Result : null;
            _dispatcher.InvokeAsync(() =>
            {
                if (_disposed || !IsOpen)
                    return;

                _icons[id] = image;
                row.Icon = image;
            });
        }, TaskScheduler.Default);
    }

    private static BitmapImage? Decode(Func<byte[]?> load)
    {
        var bytes = load();
        if (bytes is null || bytes.Length == 0)
            return null;

        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(bytes);
        image.DecodePixelWidth = IconDecodeSize;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
