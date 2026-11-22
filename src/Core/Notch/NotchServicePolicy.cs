namespace Koy.Core.Notch;

/// <summary>Servis tablosunun girdisi: çentiğin o anki durumu ve görünen sayfa.</summary>
public readonly record struct NotchServiceInput(
    NotchState State,
    bool IsSearchOpen,
    bool IsNotchVisible,
    NotchPanel CurrentPanel,
    bool HomeIsMusicFace,
    bool HomeIsHomeFace,
    bool HomeShowsSystemCard,
    bool ShowWaveform,
    bool ReduceMotion,
    bool OsdActive);

/// <summary>Hangi servisin çalışacağı. Her alan, aynı adlı servisin SetActive / SetViewing / SetAllowed çağrısına gider.</summary>
public readonly record struct NotchServiceDecision(
    bool UndoActive,
    bool SpectrumAllowed,
    bool MusicActive,
    bool CollapsedSpectrumActive,
    bool StatusActive,
    bool ControlCenterActive,
    bool FocusPageViewing,
    bool FocusTimerVisible,
    bool SystemStatsActive,
    bool HomeFaceActive,
    bool TasksActive,
    bool CalendarActive,
    bool ClipboardActive);

/// <summary>
/// AGENTS #5 servis tablosu: "hangi sayfa görünürken hangi servis alınır". Saf ve testli; NotchViewModel yalnız
/// sonucu servislere iletir. Arama açıkken sayfalar görünmez, servisleri de çalışmaz.
/// </summary>
public static class NotchServicePolicy
{
    public static NotchServiceDecision Decide(NotchServiceInput i)
    {
        var isExpanded = i.State == NotchState.Expanded && !i.IsSearchOpen;
        var shown = isExpanded && i.IsNotchVisible;
        var onHome = i.CurrentPanel == NotchPanel.Home;
        var homeFace = shown && onHome && i.HomeIsHomeFace;

        return new NotchServiceDecision(
            // Geri al şeridi ve süre zamanlayıcısı yalnız görünür açık çentikte.
            UndoActive: shown,
            // Ses çubukları "kapak ve dalga" ayarına bağlı; pil tasarrufu hareketi kısarken hiç yakalanmaz.
            SpectrumAllowed: i.ShowWaveform && !i.ReduceMotion,
            MusicActive: shown && onHome && i.HomeIsMusicFace,
            CollapsedSpectrumActive: i.IsNotchVisible && i.State == NotchState.Collapsed && !i.OsdActive,
            // Saat, hava ve pil her sekmede üstteki durum satırında görünür.
            StatusActive: isExpanded,
            ControlCenterActive: isExpanded && i.CurrentPanel == NotchPanel.ControlCenter,
            // Zamanlayıcı süresi görünür çentikte ve Odak sayfasında tazelenir.
            FocusPageViewing: isExpanded && i.CurrentPanel == NotchPanel.Focus,
            FocusTimerVisible: i.IsNotchVisible,
            // Sistem verisi Ana'da (kartlar orada) ve Sistem sayfasında.
            SystemStatsActive: (homeFace && i.HomeShowsSystemCard) || (shown && i.CurrentPanel == NotchPanel.System),
            HomeFaceActive: homeFace,
            TasksActive: shown && i.CurrentPanel == NotchPanel.Focus,
            CalendarActive: shown && i.CurrentPanel == NotchPanel.Calendar,
            ClipboardActive: shown && i.CurrentPanel == NotchPanel.Clipboard);
    }
}
