using Koy.Core.Shell;

namespace Koy.Core.Visibility;

/// <summary>
/// Görünürlük durum makinesi. Çentik ve dock'un hangi koşullarda ekranda görüneceğini,
/// ne zaman gizleneceğini ve tıklama geçirgenlik durumunu belirler.
/// Tamamen saf mantıktır; Win32/WPF bağımlılığı içermez.
/// </summary>
public sealed class VisibilityController : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private ITimer? _mediaPeekTimer;
    private bool _isMediaPeekActive;
    private VisibilityMode _mode;
    private WindowRect _notchBounds;
    private bool _hideInFullscreen = true;
    private bool _disposed;

    public event EventHandler<VisibilityResult>? Changed;

    public VisibilityMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
                return;
            _mode = value;
            Evaluate();
        }
    }

    public WindowRect NotchBounds
    {
        get => _notchBounds;
        set
        {
            if (_notchBounds == value)
                return;
            _notchBounds = value;
            Evaluate();
        }
    }

    public nint NotchHandle { get; set; }

    /// <summary>Tam ekran uygulamada gizlen (varsayılan). Kapalıysa tam ekranda da moda göre görünür kalır.</summary>
    public bool HideInFullscreen
    {
        get => _hideInFullscreen;
        set
        {
            if (_hideInFullscreen == value)
                return;
            _hideInFullscreen = value;
            Evaluate();
        }
    }

    public bool IsFullscreen { get; private set; }
    public bool IsWindowOverlapping { get; private set; }
    public bool IsMouseAtEdge { get; private set; }
    public bool IsMouseOver { get; private set; }
    public bool IsExpanded { get; private set; }
    public bool IsHeld { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsClickThrough => !IsVisible;
    public bool IsMediaPeekActive => _isMediaPeekActive;

    public VisibilityController(
        VisibilityMode initialMode = VisibilityMode.Fixed,
        TimeProvider? timeProvider = null)
    {
        _mode = initialMode;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Evaluate(forceNotify: true);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        if (IsFullscreen == isFullscreen)
            return;
        IsFullscreen = isFullscreen;
        Evaluate();
    }

    public void SetWindowOverlapping(bool isOverlapping)
    {
        if (IsWindowOverlapping == isOverlapping)
            return;
        IsWindowOverlapping = isOverlapping;
        Evaluate();
    }

    public void UpdateForegroundWindow(nint handle, WindowRect windowBounds)
    {
        if (handle == nint.Zero || handle == NotchHandle || windowBounds.IsEmpty)
        {
            SetWindowOverlapping(false);
            return;
        }

        var overlaps = !NotchBounds.IsEmpty && NotchBounds.Intersects(windowBounds);
        SetWindowOverlapping(overlaps);
    }

    public void SetMouseAtEdge(bool atEdge)
    {
        if (IsMouseAtEdge == atEdge)
            return;
        IsMouseAtEdge = atEdge;
        Evaluate();
    }

    public void SetMouseOver(bool mouseOver)
    {
        if (IsMouseOver == mouseOver)
            return;
        IsMouseOver = mouseOver;
        Evaluate();
    }

    public void SetExpanded(bool isExpanded)
    {
        if (IsExpanded == isExpanded)
            return;
        IsExpanded = isExpanded;
        Evaluate();
    }

    /// <summary>
    /// Çentiği görünür tutar (ör. düğmeli kart süresince: Akıllı/Fareyle modunda 3 sn sonra gizlenmesin).
    /// Zamanlayıcı yok; sahibi kart kapanınca false verir. Tam ekran gizlemesini aşmaz.
    /// </summary>
    public void SetHold(bool hold)
    {
        if (IsHeld == hold)
            return;
        IsHeld = hold;
        Evaluate();
    }

    public void OnMediaActivity()
    {
        _isMediaPeekActive = true;
        _mediaPeekTimer?.Dispose();
        _mediaPeekTimer = _timeProvider.CreateTimer(
            _ => OnMediaPeekTimeout(),
            null,
            TimeSpan.FromSeconds(3),
            Timeout.InfiniteTimeSpan);

        Evaluate();
    }

    public void OnMediaPeekTimeout()
    {
        _isMediaPeekActive = false;
        Evaluate();
    }

    private void Evaluate(bool forceNotify = false)
    {
        var nextVisible = ComputeVisibility();
        if (nextVisible == IsVisible && !forceNotify)
            return;

        IsVisible = nextVisible;
        Changed?.Invoke(this, new VisibilityResult(IsVisible, IsClickThrough));
    }

    private bool ComputeVisibility()
    {
        // 1. Tam ekran uygulamada gizli (oyun ve video izleme konforu); kullanıcı kapatabilir.
        if (IsFullscreen && _hideInFullscreen)
            return false;

        // 2. Fare çentiğin üstünde, kenarda veya çentik genişletilmiş durumdaysa görünür.
        if (IsMouseOver || IsMouseAtEdge || IsExpanded || IsHeld)
            return true;

        // 3. Görünürlük moduna göre karar.
        return Mode switch
        {
            VisibilityMode.Fixed => true,
            VisibilityMode.Smart => !IsWindowOverlapping || _isMediaPeekActive,
            VisibilityMode.Peek => _isMediaPeekActive,
            _ => true
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _mediaPeekTimer?.Dispose();
        _mediaPeekTimer = null;
    }
}
