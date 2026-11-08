using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// Dock öğesine ait açık bir pencerenin başlık ve tanıtıcı bilgisi (DWM önizlemesi ve küçültülmüş pencereler için).
/// </summary>
public sealed record DockWindowEntry(
    nint Handle,
    string Title,
    WindowRect Bounds,
    bool IsMinimized = false);

/// <summary>
/// Dock üzerinde gösterilen bir uygulamanın (sabitli, çalışan veya her ikisi) durum modeli.
/// Win32 veya WPF tipi içermez, saf C# modelidir.
/// </summary>
public sealed record DockItem(
    string Id,
    string Title,
    string? FilePath,
    string? TargetPath,
    byte[]? IconData,
    bool IsPinned,
    bool IsRunning,
    bool IsActive,
    IReadOnlyList<nint> WindowHandles,
    int ProcessId = 0,
    bool IsFlashing = false,
    int BadgeCount = 0,
    IReadOnlyList<DockWindowEntry>? Windows = null,
    string? AppUserModelId = null);
