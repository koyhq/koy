using Koy.Core.Media;

namespace Koy.Core.Tests.Media;

public class VideoFramePacerTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Half_speed_doubles_due_time()
    {
        var pacer = new VideoFramePacer(0.5, 0);

        Assert.Equal(Ms(0), pacer.DueAt(Ms(0)));
        Assert.Equal(Ms(200), pacer.DueAt(Ms(100)));
    }

    [Fact]
    public void Loop_continues_time_without_jumping_back()
    {
        var pacer = new VideoFramePacer(1.0, 0);
        pacer.DueAt(Ms(0));
        pacer.DueAt(Ms(100));
        var last = pacer.DueAt(Ms(200));

        // Dosya başa sardı: damga 0'a düştü; zaman bir kare adımı sonra devam eder.
        var wrapped = pacer.DueAt(Ms(0));

        Assert.Equal(Ms(200), last);
        Assert.Equal(Ms(300), wrapped);
        Assert.Equal(Ms(400), pacer.DueAt(Ms(100)));
    }

    [Fact]
    public void Max_fps_drops_frames_that_come_too_soon()
    {
        // 30 fps kaynak, en çok 15 fps gösterim: her iki kareden biri.
        var pacer = new VideoFramePacer(1.0, 15);
        var shown = 0;
        for (var i = 0; i < 30; i++)
        {
            var due = pacer.DueAt(Ms(i * 1000.0 / 30));
            if (pacer.ShouldShow(due, due))
                shown++;
        }

        Assert.InRange(shown, 14, 16);
    }

    [Fact]
    public void Half_speed_30fps_source_shows_every_frame_at_15fps_cap()
    {
        var pacer = new VideoFramePacer(0.5, 15);
        var shown = 0;
        for (var i = 0; i < 30; i++)
        {
            var due = pacer.DueAt(Ms(i * 1000.0 / 30));
            if (pacer.ShouldShow(due, due))
                shown++;
        }

        Assert.Equal(30, shown);
    }

    [Fact]
    public void Late_frame_is_skipped()
    {
        var pacer = new VideoFramePacer(1.0, 0);
        pacer.DueAt(Ms(0));
        var due = pacer.DueAt(Ms(33));

        Assert.False(pacer.ShouldShow(due, due + Ms(500)));
        Assert.True(pacer.ShouldShow(pacer.DueAt(Ms(66)), Ms(70)));
    }

    [Fact]
    public void Non_positive_speed_falls_back_to_normal()
    {
        var pacer = new VideoFramePacer(0, 0);
        pacer.DueAt(Ms(0));

        Assert.Equal(Ms(50), pacer.DueAt(Ms(50)));
    }
}
