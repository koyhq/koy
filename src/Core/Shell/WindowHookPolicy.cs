using Koy.Core.Visibility;

namespace Koy.Core.Shell;

/// <summary>
/// Bir tüketicinin pencere izleyicisinden ne istediği (kancayı değil, bilgiyi tarif eder).
/// Sistem geneli WinEvent kancaları pahalıdır (her olay arayüz iş parçacığının kuyruğuna taşınır), bu yüzden yalnız
/// istenen bilgiyi üretmeye yetecek kancalar kurulur (<see cref="WindowHookPolicy.HooksFor"/>).
/// </summary>
[Flags]
public enum WindowHookNeeds
{
    None = 0,

    /// <summary>Ön plan pencere değişimi (<c>ForegroundWindowChanged</c>, tutamaç değişince).</summary>
    Foreground = 1,

    /// <summary>Ön plan pencerenin taşınma / boyut değişimi (<c>ForegroundWindowChanged</c>, sınırlar değişince). Ön planı da gerektirir.</summary>
    ForegroundMove = 2,

    /// <summary>Açık pencere kümesi değişimi: açılma, kapanma, gösterilme, gizlenme, gizlenme (cloak) (<c>WindowsChanged</c>).</summary>
    WindowList = 4,

    /// <summary>Henüz listede olmayan pencerenin başlık değişimi (<c>WindowsChanged</c>, ertelemeli).</summary>
    TitleChanges = 8,

    /// <summary>Dock'un ihtiyacı: hepsi.</summary>
    All = Foreground | ForegroundMove | WindowList | TitleChanges,
}

/// <summary>İşletim sistemi düzeyinde kurulacak WinEvent kancaları.</summary>
[Flags]
public enum WinEventHooks
{
    None = 0,
    Foreground = 1,
    Location = 2,
    Lifecycle = 4,
    Cloak = 8,
    Name = 16,
    All = Foreground | Location | Lifecycle | Cloak | Name,
}

/// <summary>Bir WinEvent'in ilgilenilen sınıfı.</summary>
public enum WinEventKind
{
    Other,
    Foreground,
    Location,
    Lifecycle,
    Cloak,
    Name,
}

/// <summary>
/// Hangi tüketicinin hangi kancayı gerektirdiğine ve gelen olayın işlenmeye değip değmediğine karar verir.
/// Saf mantık: Win32 ve WPF yok (AGENTS #11).
/// </summary>
public static class WindowHookPolicy
{
    // Win32 WinEvent sabitleri (winuser.h); Core'da CsWin32 yok, değerler sabittir.
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectCreate = 0x8000;
    private const uint EventObjectDestroy = 0x8001;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectHide = 0x8003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint EventObjectNameChange = 0x800C;
    private const uint EventObjectCloaked = 0x8017;
    private const uint EventObjectUncloaked = 0x8018;

    /// <summary>
    /// Çentik her zaman ön plan değişimini ister. Ön plan pencerenin sınırları ise yalnız karar onlara bağlıysa:
    /// Akıllı / Gözetleme kipinde pencere çentiğin üstüne gelince gizlenir, tam ekranda gizlenme açıksa aynı pencere
    /// odak değişmeden tam ekrana geçebilir (F11). İkisi de yoksa (Sabit kip ve tam ekranda gizlenme kapalı) taşınma önemsizdir.
    /// </summary>
    public static WindowHookNeeds ForNotch(VisibilityMode mode, bool hideInFullscreen)
        => WindowHookNeeds.Foreground
           | (mode != VisibilityMode.Fixed || hideInFullscreen ? WindowHookNeeds.ForegroundMove : WindowHookNeeds.None);

    /// <summary>Dock pencere listesini, ön plan sınırlarını (yaklaşma, tam ekran) ve başlıkları izler: hepsi.</summary>
    public static WindowHookNeeds ForDock() => WindowHookNeeds.All;

    /// <summary>Bağlam kuralları ön plan uygulamasına bakar; tam ekran koşulu varsa ön plan pencerenin sınırlarına da.</summary>
    public static WindowHookNeeds ForContextRules(bool needsFullscreen)
        => WindowHookNeeds.Foreground | (needsFullscreen ? WindowHookNeeds.ForegroundMove : WindowHookNeeds.None);

    /// <summary>Birden çok tüketicinin isteği birleşir (kancalar paylaşılır).</summary>
    public static WindowHookNeeds Combine(IEnumerable<WindowHookNeeds> needs)
    {
        var result = WindowHookNeeds.None;
        foreach (var n in needs)
            result |= n;
        return result;
    }

    /// <summary>İstenen bilgiyi üretmeye yetecek en az kanca kümesi.</summary>
    public static WinEventHooks HooksFor(WindowHookNeeds needs)
    {
        var hooks = WinEventHooks.None;

        // Taşınma yalnız ön planı günceller; olay ön plan tutamacını da bildirdiği için ön plan kancası da gerekir.
        if (needs.HasFlag(WindowHookNeeds.Foreground) || needs.HasFlag(WindowHookNeeds.ForegroundMove))
            hooks |= WinEventHooks.Foreground;
        if (needs.HasFlag(WindowHookNeeds.ForegroundMove))
            hooks |= WinEventHooks.Location;
        if (needs.HasFlag(WindowHookNeeds.WindowList))
            hooks |= WinEventHooks.Lifecycle | WinEventHooks.Cloak;
        if (needs.HasFlag(WindowHookNeeds.TitleChanges))
            hooks |= WinEventHooks.Name;

        return hooks;
    }

    /// <summary>WinEvent kodunu ilgilenilen sınıfa çevirir.</summary>
    public static WinEventKind Classify(uint winEvent) => winEvent switch
    {
        EventSystemForeground => WinEventKind.Foreground,
        EventObjectLocationChange => WinEventKind.Location,
        EventObjectCreate or EventObjectDestroy or EventObjectShow or EventObjectHide => WinEventKind.Lifecycle,
        EventObjectCloaked or EventObjectUncloaked => WinEventKind.Cloak,
        EventObjectNameChange => WinEventKind.Name,
        _ => WinEventKind.Other,
    };

    /// <summary>Sınıfı üreten kanca.</summary>
    public static WinEventHooks HookFor(WinEventKind kind) => kind switch
    {
        WinEventKind.Foreground => WinEventHooks.Foreground,
        WinEventKind.Location => WinEventHooks.Location,
        WinEventKind.Lifecycle => WinEventHooks.Lifecycle,
        WinEventKind.Cloak => WinEventHooks.Cloak,
        WinEventKind.Name => WinEventHooks.Name,
        _ => WinEventHooks.None,
    };

    /// <summary>
    /// Ucuz ön süzgeç: pencere tutamacı yoksa (imleç, düzenleme imleci olayları), olay pencerenin kendisine değil bir alt
    /// nesnesine ait ya da (idObject OBJID_WINDOW değil, idChild CHILDID_SELF değil) ise hiçbir sistem çağrısı yapmadan atılır.
    /// Süreç kimliği sorgusundan (<c>GetWindowThreadProcessId</c>) önce uygulanır.
    /// </summary>
    public static bool IsWindowLevelEvent(bool hasWindow, int idObject, int idChild)
        => hasWindow && idObject == 0 /* OBJID_WINDOW */ && idChild == 0 /* CHILDID_SELF */;
}
