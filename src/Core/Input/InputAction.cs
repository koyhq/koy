namespace Koy.Core.Input;

/// <summary>
/// Kısayol, köşe ya da fare hareketiyle tetiklenebilen eylemler. Kapalı liste.
/// Ayar dosyasında ad olarak yazılır: değerler yalnız SONA eklenir, mevcut adlar ve sıra değiştirilmez.
/// </summary>
public enum InputAction
{
    ShowDesktop,
    OpenSearch,
    OpenNotch,
    OpenKoyWindow,
    ToggleWifi,
    ToggleBluetooth,
    ToggleDoNotDisturb,
    SwitchProfile,
    MoveNotchToNextMonitor,
    MoveDockToNextMonitor,

    /// <summary>Kullanıcının tanımladığı bir <see cref="CustomAction"/>; hangisi olduğu binding'de tutulur.</summary>
    RunCustomAction
}
