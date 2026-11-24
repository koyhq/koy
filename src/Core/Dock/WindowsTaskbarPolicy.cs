using Koy.Core.Settings;

namespace Koy.Core.Dock;

/// <summary>
/// Dock açıkken Windows görev çubuğu gizlenir mi (saf karar). Tam genişlik dock Windows görev çubuğunun yerini alır:
/// iki çubuk aynı kenarda üst üste durmasın diye "Windows görev çubuğunu gizle" ayarından bağımsız gizlenir.
/// Kapsülde ayar geçerlidir. Dock kapalıyken bu karar hiç sorulmaz (AGENTS #12).
/// </summary>
public static class WindowsTaskbarPolicy
{
    public static bool ShouldHide(DockSettings dock)
        => dock.HideWindowsTaskbar || dock.Style == DockStyleMode.FullWidth;
}
