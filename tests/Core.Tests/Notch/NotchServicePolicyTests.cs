using Koy.Core.Notch;

namespace Koy.Core.Tests.Notch;

/// <summary>AGENTS #5 servis tablosu: servis yalnız sayfası görünürken çalışır.</summary>
public class NotchServicePolicyTests
{
    private static NotchServiceInput Open(NotchPanel panel, bool homeMusic = false, bool homeFace = true, bool systemCard = false) =>
        new(NotchState.Expanded, IsSearchOpen: false, IsNotchVisible: true, panel,
            HomeIsMusicFace: homeMusic, HomeIsHomeFace: homeFace, HomeShowsSystemCard: systemCard,
            ShowWaveform: true, ReduceMotion: false, OsdActive: false);

    [Fact]
    public void Collapsed_visible_notch_runs_only_the_timer_label_and_collapsed_spectrum()
    {
        var d = NotchServicePolicy.Decide(Open(NotchPanel.Home) with { State = NotchState.Collapsed });

        Assert.True(d.CollapsedSpectrumActive);
        Assert.True(d.FocusTimerVisible);
        Assert.False(d.StatusActive);
        Assert.False(d.UndoActive);
        Assert.False(d.HomeFaceActive);
        Assert.False(d.MusicActive);
        Assert.False(d.SystemStatsActive);
        Assert.False(d.CalendarActive);
    }

    [Fact]
    public void Hidden_notch_runs_nothing()
    {
        var d = NotchServicePolicy.Decide(Open(NotchPanel.Home, homeMusic: true) with { IsNotchVisible = false });

        Assert.False(d.UndoActive);
        Assert.False(d.MusicActive);
        Assert.False(d.CollapsedSpectrumActive);
        Assert.False(d.FocusTimerVisible);
        Assert.False(d.HomeFaceActive);
        Assert.False(d.SystemStatsActive);
        Assert.False(d.TasksActive);
        // Durum satırı yalnız açık çentiğe bağlıdır (görünürlük ayrıca VM'de denetlenir): olduğu gibi korunur.
        Assert.True(d.StatusActive);
    }

    [Fact]
    public void Open_search_hides_every_page_service()
    {
        var d = NotchServicePolicy.Decide(Open(NotchPanel.Focus) with { IsSearchOpen = true });

        Assert.False(d.UndoActive);
        Assert.False(d.StatusActive);
        Assert.False(d.FocusPageViewing);
        Assert.False(d.TasksActive);
        // Zamanlayıcı süresi görünür çentikte aranırken de tazelenir.
        Assert.True(d.FocusTimerVisible);
    }

    [Fact]
    public void Home_music_face_runs_music_but_not_home_cards()
    {
        var d = NotchServicePolicy.Decide(Open(NotchPanel.Home, homeMusic: true, homeFace: false));

        Assert.True(d.MusicActive);
        Assert.False(d.HomeFaceActive);
        Assert.False(d.SystemStatsActive);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Home_face_runs_system_stats_only_when_the_system_card_is_on(bool systemCard, bool expected)
    {
        var d = NotchServicePolicy.Decide(Open(NotchPanel.Home, homeFace: true, systemCard: systemCard));

        Assert.True(d.HomeFaceActive);
        Assert.Equal(expected, d.SystemStatsActive);
    }

    [Fact]
    public void System_page_runs_system_stats_even_without_the_home_card()
    {
        var d = NotchServicePolicy.Decide(Open(NotchPanel.System));

        Assert.True(d.SystemStatsActive);
        Assert.False(d.HomeFaceActive);
    }

    [Theory]
    [InlineData(NotchPanel.Focus)]
    [InlineData(NotchPanel.Calendar)]
    [InlineData(NotchPanel.Clipboard)]
    [InlineData(NotchPanel.ControlCenter)]
    [InlineData(NotchPanel.Shelf)]
    [InlineData(NotchPanel.Home)]
    [InlineData(NotchPanel.System)]
    public void Each_page_service_runs_only_on_its_own_page(NotchPanel panel)
    {
        var d = NotchServicePolicy.Decide(Open(panel));

        Assert.Equal(panel == NotchPanel.Focus, d.FocusPageViewing);
        Assert.Equal(panel == NotchPanel.Focus, d.TasksActive);
        Assert.Equal(panel == NotchPanel.Calendar, d.CalendarActive);
        Assert.Equal(panel == NotchPanel.Clipboard, d.ClipboardActive);
        Assert.Equal(panel == NotchPanel.ControlCenter, d.ControlCenterActive);
        Assert.Equal(panel == NotchPanel.Home, d.HomeFaceActive);
        // Durum satırı her sekmede.
        Assert.True(d.StatusActive);
        Assert.True(d.UndoActive);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void Spectrum_needs_the_setting_and_no_reduced_motion(bool showWaveform, bool reduceMotion, bool expected)
    {
        var d = NotchServicePolicy.Decide(Open(NotchPanel.Home) with { ShowWaveform = showWaveform, ReduceMotion = reduceMotion });

        Assert.Equal(expected, d.SpectrumAllowed);
    }

    [Fact]
    public void Collapsed_spectrum_pauses_while_an_osd_is_showing()
    {
        var collapsed = Open(NotchPanel.Home) with { State = NotchState.Collapsed };

        Assert.True(NotchServicePolicy.Decide(collapsed).CollapsedSpectrumActive);
        Assert.False(NotchServicePolicy.Decide(collapsed with { OsdActive = true }).CollapsedSpectrumActive);
        Assert.False(NotchServicePolicy.Decide(collapsed with { State = NotchState.Peek }).CollapsedSpectrumActive);
    }
}
