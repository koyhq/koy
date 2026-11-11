using Koy.Core.Shortcuts;

namespace Koy.Core.Input;

/// <summary>Bir eylemi tetikleyen şeyin türü. Ayar dosyasında ad olarak yazılır.</summary>
public enum InputTriggerKind
{
    Shortcut,
    HotCorner,
    MouseButton
}

/// <summary>Ekranın dört köşesi.</summary>
public enum ScreenCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>Fare tetikleyicisinin yüzeyi: çentik ya da dock.</summary>
public enum InputSurface
{
    Notch,
    Dock
}

/// <summary>Çentik ve dock için seçilebilir fare hareketleri (mevcut WheelAction / MiddleClickAction'ın genişletilmesi).</summary>
public enum MouseGesture
{
    Wheel,
    MiddleClick,
    DoubleClick,
    RightClick
}

/// <summary>Tetikleyici. Her tür kendi verisini taşır; tür bilinmiyorsa <see cref="InputBindingParser"/> onu düşürür.</summary>
public abstract record InputTrigger;

/// <summary>Genel klavye kısayolu (RegisterHotKey ya da kanca ile).</summary>
public sealed record ShortcutTrigger(Hotkey Hotkey) : InputTrigger;

/// <summary>
/// Ekran köşesi. <see cref="DwellMs"/>: imlecin köşede kalması gereken süre (yalnız veri; zamanlayıcı Platform'da).
/// <see cref="PushBackPx"/>: köşeden geri itme mesafesi, yanlış tetiklemeyi önlemek için (yalnız veri).
/// </summary>
public sealed record HotCornerTrigger(ScreenCorner Corner, int DwellMs, int PushBackPx) : InputTrigger;

/// <summary>Çentik ya da dock üzerinde bir fare hareketi.</summary>
public sealed record MouseTrigger(InputSurface Surface, MouseGesture Gesture) : InputTrigger;

/// <summary>
/// Bir eylem ile bir tetikleyicinin bağı. <see cref="CustomActionId"/> yalnız <see cref="InputAction.RunCustomAction"/>
/// için doludur ve bir <see cref="CustomAction"/> kimliğine işaret eder.
/// </summary>
public sealed record InputBinding(InputAction Action, InputTrigger Trigger, string? CustomActionId = null);
