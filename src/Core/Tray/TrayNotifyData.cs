namespace Koy.Core.Tray;

/// <summary>Shell_NotifyIcon çağrısının türü (NIM_*).</summary>
public enum TrayMessage : uint
{
    Add = 0,
    Modify = 1,
    Delete = 2,
    SetFocus = 3,
    SetVersion = 4
}

/// <summary>Mesajda hangi alanların geçerli olduğu (NIF_*).</summary>
[Flags]
public enum TrayIconFields : uint
{
    None = 0,
    Message = 0x01,
    Icon = 0x02,
    Tip = 0x04,
    State = 0x08,
    Info = 0x10,
    Guid = 0x20
}

/// <summary>Balon bildiriminin simge ve davranış bayrakları (NIIF_*).</summary>
[Flags]
public enum TrayBalloonFlags : uint
{
    None = 0,
    Info = 0x01,
    Warning = 0x02,
    Error = 0x03,
    User = 0x04,
    IconMask = 0x0F,
    NoSound = 0x10,
    LargeIcon = 0x20,
    RespectQuietTime = 0x80
}

/// <summary>
/// Bir uygulamanın tepsiye gönderdiği tek mesaj. Pencere ve simge tanıtıcıları <c>nint</c>;
/// Win32 tipi taşımaz (AGENTS.md #11). <see cref="Version"/> alanı <see cref="TrayIconFields.Info"/>
/// taşındığında Win32 <c>uTimeout</c> birleşimini tutar.
/// </summary>
public sealed record TrayNotifyData(
    TrayMessage Message,
    nint OwnerHwnd,
    uint Id,
    TrayIconFields Fields,
    uint CallbackMessage,
    nint IconHandle,
    string Tooltip,
    uint State,
    uint StateMask,
    uint Version,
    Guid Guid,
    string Info = "",
    string InfoTitle = "",
    TrayBalloonFlags InfoFlags = TrayBalloonFlags.None,
    nint BalloonIconHandle = 0);

/// <summary>Tepsi simgesinden gelen geçerli bir balon bildirimi.</summary>
public sealed record TrayBalloonNotification(
    TrayIconKey Key,
    string AppName,
    string Title,
    string Body,
    TrayBalloonFlags Flags,
    DateTimeOffset Time,
    byte[]? Icon = null);

/// <summary>
/// Tepsi simgesinin kimliği. GUID verilmişse simge yalnız GUID ile, verilmemişse pencere + kimlik numarasıyla tanınır.
/// </summary>
public readonly record struct TrayIconKey(nint OwnerHwnd, uint Id, Guid Guid)
{
    public static TrayIconKey From(TrayNotifyData data) =>
        (data.Fields & TrayIconFields.Guid) != 0 && data.Guid != Guid.Empty
            ? new TrayIconKey(0, 0, data.Guid)
            : new TrayIconKey(data.OwnerHwnd, data.Id, Guid.Empty);
}

/// <summary>Dock'un tepsi penceresinde gösterilen simge.</summary>
public sealed record TrayIcon(
    TrayIconKey Key,
    nint OwnerHwnd,
    uint Id,
    uint CallbackMessage,
    nint IconHandle,
    string Tooltip,
    bool IsHidden,
    uint Version)
{
    /// <summary>Gizli işaretli ya da simgesi olmayan kayıt gösterilmez (boş kare çizilmesin).</summary>
    public bool IsVisible => !IsHidden && IconHandle != 0;
}
