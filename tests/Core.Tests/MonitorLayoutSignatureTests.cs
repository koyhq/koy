using Koy.Core.Display;
using Koy.Core.Shell;

namespace Koy.Core.Tests;

public class MonitorLayoutSignatureTests
{
    private static DisplayMonitor Mon(string id, WindowRect work, double dpi = 1.0, bool primary = true) =>
        new(id, id, new WindowRect(0, 0, 1920, 1080), work, primary, dpi, dpi);

    [Fact]
    public void Work_area_change_does_not_change_the_signature()
    {
        var a = MonitorLayoutSignature.Compute([Mon("m", new WindowRect(0, 0, 1920, 1034))]);
        var b = MonitorLayoutSignature.Compute([Mon("m", new WindowRect(0, 0, 1920, 1080))]);
        var c = MonitorLayoutSignature.Compute([Mon("m", new WindowRect(0, 0, 1920, 1032))]);

        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    [Fact]
    public void Dpi_bounds_and_primary_changes_do_change_it()
    {
        var work = new WindowRect(0, 0, 1920, 1080);
        var baseSig = MonitorLayoutSignature.Compute([Mon("m", work)]);

        Assert.NotEqual(baseSig, MonitorLayoutSignature.Compute([Mon("m", work, dpi: 1.5)]));
        Assert.NotEqual(baseSig, MonitorLayoutSignature.Compute([Mon("m", work, primary: false)]));
        Assert.NotEqual(baseSig, MonitorLayoutSignature.Compute([Mon("m", work), Mon("n", work, primary: false)]));
        Assert.NotEqual(baseSig, MonitorLayoutSignature.Compute([
            new DisplayMonitor("m", "m", new WindowRect(0, 0, 2560, 1440), work, true)]));
    }

    [Fact]
    public void Order_of_monitors_does_not_matter()
    {
        var w = new WindowRect(0, 0, 1920, 1080);
        Assert.Equal(
            MonitorLayoutSignature.Compute([Mon("a", w), Mon("b", w, primary: false)]),
            MonitorLayoutSignature.Compute([Mon("b", w, primary: false), Mon("a", w)]));
    }
}
