using Koy.Core.Diagnostics;

namespace Koy.Core.Search;

public enum SearchActivation
{
    None,

    /// <summary>Çalıştırıldı; panel kapanmalı.</summary>
    Executed,

    /// <summary>Onay bekliyor; panel açık kalır.</summary>
    NeedsConfirmation,

    /// <summary>Sorgu değişti; panel açık kalır.</summary>
    QueryUpdated,

    /// <summary>Sonuçlar henüz gelmedi; ilk sonuç gelince çalıştırılacak.</summary>
    Deferred
}

public readonly record struct SearchActivationResult(SearchActivation Outcome, SearchResult? Result);

/// <summary>Arama panelinin durumu: sorgu, sonuçlar, seçili satır ve eylem, onay, geçmiş.</summary>
public sealed class SearchPanelController : IDisposable
{
    private readonly IReadOnlyList<ISearchSource> _sources;
    private readonly Action<Action> _post;
    private readonly SearchHistory? _history;
    private readonly Func<SearchQuery, SearchResult?>? _fallback;
    private readonly string _historyCategory;
    private readonly string _removeLabel;
    private SearchQuery _query = new("");
    private string _untrimmed = "";
    private string _prior = "";
    private bool _activateWhenReady;
    private bool _disposed;

    /// <param name="sources">Sonuç kaynakları.</param>
    /// <param name="post">Kaynak olaylarını UI iş parçacığına taşır; verilmezse olduğu yerde çalışır.</param>
    /// <param name="history">Çalıştırılan aramalar; yoksa geçmiş kapalıdır.</param>
    /// <param name="fallback">Hiç sonuç yoksa gösterilecek satır (varsayılan web araması).</param>
    public SearchPanelController(
        IReadOnlyList<ISearchSource> sources,
        Action<Action>? post = null,
        SearchHistory? history = null,
        Func<SearchQuery, SearchResult?>? fallback = null,
        string historyCategory = "",
        string removeLabel = "")
    {
        _sources = sources;
        _post = post ?? (action => action());
        _history = history;
        _fallback = fallback;
        _historyCategory = historyCategory;
        _removeLabel = removeLabel;
    }

    public event EventHandler? Changed;

    /// <summary>Sorgu program tarafından değişti; yazı kutusu güncellenmeli.</summary>
    public event EventHandler<int>? QueryReplaced;

    /// <summary>Bekletilen Enter, sonuç gelince çalıştı; panel kapanmalı.</summary>
    public event EventHandler<SearchResult>? AutoActivated;

    public bool RetainPriorSearch { get; set; } = true;

    public SearchHistoryMode HistoryMode { get; set; } = SearchHistoryMode.Suggestion;

    public bool IsOpen { get; private set; }

    /// <summary>Yazı kutusundaki metin (boşluklar dahil).</summary>
    public string Query => _untrimmed;

    public IReadOnlyList<SearchResult> Results { get; private set; } = [];

    public int SelectedIndex { get; private set; } = -1;

    /// <summary>Seçili satırın vurgulu eylemi; -1 satırın kendisi.</summary>
    public int ActiveAction { get; private set; } = -1;

    public string? PendingConfirmationId { get; private set; }

    public bool ShowingHistory { get; private set; }

    public bool IsBusy => _sources.Any(s => s.IsBusy);

    public bool HasHistory => HistoryMode != SearchHistoryMode.Disabled && _history is { Entries.Count: > 0 };

    public SearchResult? Selected => SelectedIndex >= 0 && SelectedIndex < Results.Count ? Results[SelectedIndex] : null;

    /// <summary>Yazılanı tamamlayan geçmiş kaydı; yoksa boş.</summary>
    public string Suggestion =>
        HistoryMode == SearchHistoryMode.Disabled || _untrimmed.Length == 0 ? "" : _history?.Suggest(_untrimmed) ?? "";

    public void Open()
    {
        if (IsOpen || _disposed)
            return;

        IsOpen = true;
        ResetSelection();
        Results = [];
        ShowingHistory = false;

        foreach (var source in _sources)
        {
            source.Changed += OnSourceChanged;
            CrashLog.Guard("Arama kaynağı hazırlığı", source.Prepare);
        }

        SetText(RetainPriorSearch ? _prior : "");
        if (_untrimmed.Length > 0)
            Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        _prior = _untrimmed;
        SetText("");
        Results = [];
        ShowingHistory = false;
        ResetSelection();

        foreach (var source in _sources)
        {
            source.Changed -= OnSourceChanged;
            CrashLog.Guard("Arama kaynağı bırakma", source.Release);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetQuery(string? text)
    {
        if (!IsOpen)
            return;

        var value = text ?? "";
        if (value == _untrimmed)
            return;

        SetText(value);
        ShowingHistory = false;
        _activateWhenReady = false;
        PendingConfirmationId = null;
        Refresh();
    }

    /// <summary>Boş kutuda aşağı ok ya da düğme: son aramaları gösterir / gizler.</summary>
    public void ToggleHistory()
    {
        if (!IsOpen || !HasHistory || _query.Raw.Length > 0)
            return;

        ShowingHistory = !ShowingHistory;
        Results = ShowingHistory ? BuildHistoryRows() : [];
        SelectedIndex = Results.Count > 0 ? 0 : -1;
        ActiveAction = -1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
            return;

        SelectedIndex = ((SelectedIndex + delta) % Results.Count + Results.Count) % Results.Count;
        ActiveAction = -1;
        PendingConfirmationId = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Bir sonraki ya da önceki kategorinin ilk satırına atlar, sona gelince başa döner.</summary>
    public void MoveCategory(int direction)
    {
        if (Results.Count == 0)
            return;

        var current = Math.Max(SelectedIndex, 0);
        var category = Results[current].Category;
        var starts = Enumerable.Range(0, Results.Count)
            .Where(i => i == 0 || Results[i].Category != Results[i - 1].Category)
            .ToList();
        var position = starts.FindLastIndex(i => i <= current);

        // Yukarı giderken önce bulunulan kategorinin başına değil, bir önceki kategoriye atlanır.
        var next = ((position + Math.Sign(direction)) % starts.Count + starts.Count) % starts.Count;
        if (Results[starts[next]].Category != category || starts.Count == 1)
            Select(starts[next]);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= Results.Count || index == SelectedIndex)
            return;

        SelectedIndex = index;
        ActiveAction = -1;
        PendingConfirmationId = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Tab: satırın eylemleri arasında gezer. Eylemlerin dışına çıkıyorsa false döner, satır değişmelidir.</summary>
    public bool CycleAction(int direction)
    {
        if (Selected is not { } result)
            return false;

        var next = ActiveAction + Math.Sign(direction);
        if (next < -1 || next >= result.Actions.Count)
            return false;

        ActiveAction = next;
        PendingConfirmationId = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Üst tuşuyla satıra dönerken son eylemin vurgulanması için.</summary>
    public void FocusLastAction()
    {
        if (Selected is not { Actions.Count: > 0 } result)
            return;

        ActiveAction = result.Actions.Count - 1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Seçili satırı ya da vurgulu eylemini çalıştırır. Sonuçlar henüz gelmediyse ilk sonuca ertelenir.</summary>
    /// <param name="firstAction">Üst+Enter: satırın ilk eylemi.</param>
    public SearchActivationResult Activate(bool firstAction = false)
    {
        if (Selected is not { } result)
        {
            if (_query.IsEmpty || !IsBusy)
                return new SearchActivationResult(SearchActivation.None, null);

            _activateWhenReady = true;
            return new SearchActivationResult(SearchActivation.Deferred, null);
        }

        var actionIndex = firstAction && result.Actions.Count > 0 ? 0 : ActiveAction;
        if (actionIndex >= 0 && actionIndex < result.Actions.Count)
        {
            CrashLog.Guard("Arama eylemi", result.Actions[actionIndex].Execute);
            return Finish(result, staysOpen: ShowingHistory);
        }

        if (result.RequiresConfirmation && PendingConfirmationId != result.Id)
        {
            PendingConfirmationId = result.Id;
            Changed?.Invoke(this, EventArgs.Empty);
            return new SearchActivationResult(SearchActivation.NeedsConfirmation, result);
        }

        PendingConfirmationId = null;
        CrashLog.Guard("Arama sonucu çalıştırma", result.Execute);

        if (result.ReplacesQuery is { } replacement)
        {
            Remember(result);
            ReplaceQuery(replacement);
            return new SearchActivationResult(SearchActivation.QueryUpdated, result);
        }

        return Finish(result, staysOpen: false);
    }

    /// <summary>Sorguyu program tarafından değiştirir ve yazı kutusuna bildirir.</summary>
    public void ReplaceQuery(string text)
    {
        SetQuery(text);
        QueryReplaced?.Invoke(this, text.Length);
    }

    public void ClearHistory()
    {
        _history?.Clear();
        if (!ShowingHistory)
            return;

        ShowingHistory = false;
        Results = [];
        ResetSelection();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Close();
        _disposed = true;
    }

    private SearchActivationResult Finish(SearchResult result, bool staysOpen)
    {
        Remember(result);
        if (staysOpen)
        {
            // Geçmiş satırından kayıt silindi: liste yerinde tazelenir.
            var index = SelectedIndex;
            Results = HasHistory ? BuildHistoryRows() : [];
            ShowingHistory = Results.Count > 0;
            SelectedIndex = Math.Min(index, Results.Count - 1);
            ActiveAction = -1;
            Changed?.Invoke(this, EventArgs.Empty);
            return new SearchActivationResult(SearchActivation.QueryUpdated, result);
        }

        return new SearchActivationResult(SearchActivation.Executed, result);
    }

    private void Remember(SearchResult result)
    {
        // Başında boşluk olan arama, kabuktaki gibi geçmişe yazılmaz.
        if (result.SkipHistory || HistoryMode == SearchHistoryMode.Disabled || ShowingHistory
            || _query.IsEmpty || _untrimmed.StartsWith(' '))
            return;

        _history?.Add(_query.Raw);
    }

    private IReadOnlyList<SearchResult> BuildHistoryRows()
    {
        var entries = _history?.Entries ?? [];
        return entries.Take(SearchHistory.VisibleEntries).Select((entry, index) =>
            new SearchResult("history:" + index, _historyCategory, entry, () => { })
            {
                Glyph = SearchGlyph.History,
                ReplacesQuery = entry,
                SkipHistory = true,
                Actions = [new SearchAction("remove", _removeLabel, () => _history?.RemoveAt(index))]
            }).ToList();
    }

    private void SetText(string text)
    {
        _untrimmed = text;
        _query = new SearchQuery(text);
    }

    private void ResetSelection()
    {
        SelectedIndex = -1;
        ActiveAction = -1;
        PendingConfirmationId = null;
        _activateWhenReady = false;
    }

    private void OnSourceChanged(object? sender, EventArgs e) => _post(() =>
    {
        if (IsOpen && !ShowingHistory)
            Refresh();
    });

    private void Refresh()
    {
        var previous = Selected;
        var found = new List<SearchResult>();

        if (!_query.IsEmpty)
        {
            foreach (var source in _sources)
            {
                try
                {
                    found.AddRange(source.Search(_query));
                }
                catch (Exception ex)
                {
                    // Bir kaynağın hatası diğerlerinin sonuçlarını götürmesin.
                    CrashLog.Write("Arama kaynağı", ex);
                }
            }
        }

        // Aynı hedefe giden sonuçlar tekilleşir; en önemli olan kalır.
        var unique = found.GroupBy(r => r.Id).Select(g => g.OrderByDescending(r => r.CategoryRelevance).ThenByDescending(r => r.Relevance).First()).ToList();
        if (unique.Count == 0 && !_query.IsEmpty && !IsBusy && _fallback?.Invoke(_query) is { } fallback)
            unique.Add(fallback);

        Results = SearchLayout.Arrange(unique);

        // Liste tazelenince kullanıcının seçimi korunur; dokunmadıysa en üstteki seçili olur.
        var kept = previous is null || SelectedIndex <= 0 ? -1 : Results.ToList().FindIndex(r => r.Id == previous.Id);
        var selectionMoved = kept < 0 || previous is null || Results[kept].Id != previous.Id;
        SelectedIndex = kept >= 0 ? kept : Results.Count > 0 ? 0 : -1;
        if (selectionMoved)
            ActiveAction = -1;

        if (PendingConfirmationId is not null && Results.All(r => r.Id != PendingConfirmationId))
            PendingConfirmationId = null;

        Changed?.Invoke(this, EventArgs.Empty);

        if (!_activateWhenReady)
            return;

        if (Results.Count > 0)
        {
            _activateWhenReady = false;
            var result = Activate();
            if (result is { Outcome: SearchActivation.Executed, Result: { } executed })
                AutoActivated?.Invoke(this, executed);
        }
        else if (!IsBusy)
        {
            _activateWhenReady = false;
        }
    }
}
