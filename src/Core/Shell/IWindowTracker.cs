namespace Koy.Core.Shell;

/// <summary>
/// Pencere tanıtıcı bilgisi ve ekran konumu.
/// </summary>
public sealed record WindowInfo(nint Handle, WindowRect Bounds);

/// <summary>
/// Açık olan bir üst düzey uygulama penceresinin bilgisi.
/// </summary>
public sealed record OpenWindowInfo(
    nint Handle,
    string Title,
    string? ProcessPath,
    int ProcessId,
    WindowRect Bounds,
    byte[]? IconData = null,
    string? AppUserModelId = null,
    string? ClassName = null);

/// <summary>
/// Ön plan penceresi, konum değişiklikleri ve açık pencereleri izleyen sözleşme.
/// Win32 tipleri içermez, pencere tanıtıcıları nint ile temsil edilir.
/// </summary>
public interface IWindowTracker : IDisposable
{
    event EventHandler<WindowInfo>? ForegroundWindowChanged;

    event EventHandler? WindowsChanged;

    event EventHandler<nint>? WindowFlashing;

    WindowInfo? CurrentForeground { get; }

    IReadOnlyList<OpenWindowInfo> GetOpenWindows();

    void ActivateWindow(nint handle);

    void MinimizeWindow(nint handle);

    void CloseWindow(nint handle);

    void TerminateProcess(int processId);

    void Start();

    void Stop();

    bool GetCursorPosition(out int x, out int y);
}
