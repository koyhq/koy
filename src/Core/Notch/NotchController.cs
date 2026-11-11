namespace Koy.Core.Notch;

/// <summary>
/// Notch durumunu, panellerini ve zamanlayıcılarını yöneten saf iş mantığı denetleyicisi.
/// </summary>
public sealed class NotchController : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly List<NotchPanel> _enabledPanels = [NotchPanel.Home];
    private ITimer? _autoPeekTimer;
    private ITimer? _mouseLeaveTimer;
    private bool _disposed;

    public event EventHandler<NotchState>? StateChanged;
    public event EventHandler<NotchPanel>? PanelChanged;
    public event Action<int>? PanelTransitionTriggered;

    /// <summary>Açık paneller listesi (kümesi ya da sırası) değişti; sekme çubuğu yeniden kurulmalı.</summary>
    public event EventHandler? EnabledPanelsChanged;

    public NotchState State { get; private set; } = NotchState.Collapsed;
    public NotchPanel CurrentPanel { get; private set; } = NotchPanel.Home;
    public bool IsMouseOver { get; private set; }
    public IReadOnlyList<NotchPanel> EnabledPanels => _enabledPanels;

    /// <summary>
    /// Açılma davranışı: true ise fare üzerine gelince çentik doğrudan açılır; false ise önce küçük bir ipucu
    /// (Peek) görünür, tıklayınca açılır (ayar: 0 = üzerine gelince, 1 = tıklayınca).
    /// </summary>
    public bool OpenOnHover { get; set; }

    public NotchController(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void SetEnabledPanels(IEnumerable<NotchPanel> panels)
    {
        _enabledPanels.Clear();
        _enabledPanels.AddRange(panels);
        if (_enabledPanels.Count == 0)
            _enabledPanels.Add(NotchPanel.Home);

        EnabledPanelsChanged?.Invoke(this, EventArgs.Empty);

        if (!_enabledPanels.Contains(CurrentPanel))
            SelectPanel(_enabledPanels[0]);
    }

    public void NextPanel()
    {
        if (_enabledPanels.Count <= 1)
            return;

        var currentIndex = _enabledPanels.IndexOf(CurrentPanel);
        var nextIndex = (currentIndex + 1) % _enabledPanels.Count;
        PanelTransitionTriggered?.Invoke(1);
        SelectPanel(_enabledPanels[nextIndex]);
    }

    public void PreviousPanel()
    {
        if (_enabledPanels.Count <= 1)
            return;

        var currentIndex = _enabledPanels.IndexOf(CurrentPanel);
        var prevIndex = (currentIndex - 1 + _enabledPanels.Count) % _enabledPanels.Count;
        PanelTransitionTriggered?.Invoke(-1);
        SelectPanel(_enabledPanels[prevIndex]);
    }

    /// <summary>
    /// Sekmeden doğrudan bir panele geçer; kayma yönü panellerin sırasına göre seçilir (sağdaki sekme sağdan gelir).
    /// </summary>
    public void GoToPanel(NotchPanel panel)
    {
        if (!_enabledPanels.Contains(panel) || CurrentPanel == panel)
            return;

        var direction = _enabledPanels.IndexOf(panel) > _enabledPanels.IndexOf(CurrentPanel) ? 1 : -1;
        PanelTransitionTriggered?.Invoke(direction);
        SelectPanel(panel);
    }

    public void SelectPanel(NotchPanel panel)
    {
        if (!_enabledPanels.Contains(panel) || CurrentPanel == panel)
            return;

        CurrentPanel = panel;
        PanelChanged?.Invoke(this, CurrentPanel);
    }

    /// <summary>
    /// Çentiği doğrudan Expanded durumuna geçirir ve isteğe bağlı olarak belirtilen paneli seçer.
    /// Sürükle-bırak veya doğrudan kullanıcı etkileşimi için kullanılır.
    /// </summary>
    public void Expand(NotchPanel? panel = null)
    {
        _autoPeekTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _mouseLeaveTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        SetState(NotchState.Expanded);
        if (panel.HasValue)
        {
            if (!_enabledPanels.Contains(panel.Value))
            {
                // Ayarlarda kapalı panel (ör. sürükle-bırakta Raf) geçici olarak listeye girer; sekme çubuğu da bilsin.
                _enabledPanels.Add(panel.Value);
                _enabledPanels.Sort((a, b) => NotchPanels.SortIndex(a).CompareTo(NotchPanels.SortIndex(b)));
                EnabledPanelsChanged?.Invoke(this, EventArgs.Empty);
            }
            SelectPanel(panel.Value);
        }
    }

    public void OnMouseEnter()
    {
        IsMouseOver = true;
        _mouseLeaveTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        if (OpenOnHover && State != NotchState.Expanded)
        {
            _autoPeekTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            SetState(NotchState.Expanded);
        }
        else if (State == NotchState.Collapsed)
        {
            SetState(NotchStateTransitions.Transition(State, NotchTrigger.MouseEnter));
        }
    }

    public void OnMouseLeave()
    {
        IsMouseOver = false;

        if (State == NotchState.Peek)
        {
            _autoPeekTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            SetState(NotchStateTransitions.Transition(State, NotchTrigger.MouseLeave));
        }
        else if (State == NotchState.Expanded && !IsPinned && !IsHeld)
        {
            StartMouseLeaveTimer();
        }
    }

    private void StartMouseLeaveTimer()
    {
        _mouseLeaveTimer?.Dispose();
        _mouseLeaveTimer = _timeProvider.CreateTimer(
            _ => OnMouseLeaveTimeout(),
            null,
            TimeSpan.FromMilliseconds(NotchOpenPolicy.DefaultCloseDelayMs),
            Timeout.InfiniteTimeSpan);
    }

    // Geçici tutma: dosya seçici gibi çentiğin dışında açılan pencereler için. Kullanıcının iğnesinden (IsPinned)
    // ayrıdır: iğne simgesi yanmaz, kullanıcı seçimi bozulmaz. Sayaçlı: iç içe tutmalar birbirini açmaz.
    private int _holds;

    /// <summary>Bir pencere (ör. dosya seçici) açık olduğu için çentik kapanmaz: fare ayrılsa da, çentiğe tıklansa da.</summary>
    public bool IsHeld => _holds > 0;

    /// <summary>Tutmayı başlatır; her çağrı bir <see cref="ReleaseHold"/> ile eşlenmelidir.</summary>
    public void HoldOpen()
    {
        _mouseLeaveTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _holds++;
    }

    /// <summary>Tutmayı bırakır. Sonuncusu bırakılınca fare dışarıdaysa her zamanki gecikmeyle kapanma başlar.</summary>
    public void ReleaseHold()
    {
        if (_holds == 0)
            return;

        if (--_holds == 0 && State == NotchState.Expanded && !IsMouseOver && !IsPinned)
            StartMouseLeaveTimer();
    }

    /// <summary>"Çentiği açık tut": açıkken fare çıkınca kapanmaz. Çentik kapanınca (tıklamayla) kendiliğinden kalkar.</summary>
    public bool IsPinned { get; private set; }

    public event EventHandler? PinnedChanged;

    public void TogglePinned() => SetPinned(!IsPinned);

    public void SetPinned(bool pinned)
    {
        // Yalnız açık çentik tutulabilir; kapalıyken sabitlemenin anlamı yok.
        pinned &= State == NotchState.Expanded;
        if (IsPinned == pinned)
            return;

        IsPinned = pinned;
        PinnedChanged?.Invoke(this, EventArgs.Empty);

        // Sabit kalkarken fare zaten dışarıdaysa her zamanki gecikmeyle kapansın.
        if (!pinned && State == NotchState.Expanded && !IsMouseOver)
            StartMouseLeaveTimer();
    }

    public void OnClick()
    {
        // Seçici açıkken çentiğe tıklamak onu kapatıp seçiciyi yetim bırakmasın.
        if (State == NotchState.Expanded && IsHeld)
            return;

        _autoPeekTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _mouseLeaveTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        SetState(NotchStateTransitions.Transition(State, NotchTrigger.Click));
    }

    public void OnTrackChanged()
    {
        if (State == NotchState.Collapsed)
        {
            SetState(NotchStateTransitions.Transition(State, NotchTrigger.TrackChanged));
            ResetAutoPeekTimer();
        }
        else if (State == NotchState.Peek && !IsMouseOver)
        {
            ResetAutoPeekTimer();
        }
    }

    private void ResetAutoPeekTimer()
    {
        _autoPeekTimer?.Dispose();
        _autoPeekTimer = _timeProvider.CreateTimer(
            _ => OnAutoPeekTimeout(),
            null,
            TimeSpan.FromSeconds(3),
            Timeout.InfiniteTimeSpan);
    }

    public void OnAutoPeekTimeout()
    {
        if (State == NotchState.Peek && !IsMouseOver)
            SetState(NotchStateTransitions.Transition(State, NotchTrigger.AutoPeekTimeout));
    }

    public void OnMouseLeaveTimeout()
    {
        if (State == NotchState.Expanded && !IsMouseOver && !IsPinned && !IsHeld)
            SetState(NotchStateTransitions.Transition(State, NotchTrigger.MouseLeaveDelayTimeout));
    }

    private void SetState(NotchState newState)
    {
        if (State == newState)
            return;

        State = newState;
        if (State != NotchState.Expanded)
            SetPinned(false);
        StateChanged?.Invoke(this, State);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _autoPeekTimer?.Dispose();
        _mouseLeaveTimer?.Dispose();
        _autoPeekTimer = null;
        _mouseLeaveTimer = null;
    }
}
