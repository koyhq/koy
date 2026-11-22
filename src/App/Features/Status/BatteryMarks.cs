using System.Windows.Media;
using Koy.Core.Power;

namespace Koy.App.Features.Status;

/// <summary>Pil işaretlerinin (yıldırım, fiş, yaprak) dondurulmuş geometrileri; 24x24 ızgara. Simge ve çentik halkası paylaşır.</summary>
public static class BatteryMarks
{
    public static readonly Geometry Bolt = Freeze("M13,2 L4,14 H11 L10,22 L19,10 H12 Z");
    public static readonly Geometry Plug = Freeze("M8,2 H10 V7 H8 Z M14,2 H16 V7 H14 Z M6,7 H18 V11 A6,6 0 0 1 6,11 Z M11,16 H13 V22 H11 Z");
    public static readonly Geometry Leaf = Freeze("M5,20 C3,10 10,3 21,4 C21,14 15,21 5,20 Z");

    public static Geometry? For(BatteryOverlay overlay) => overlay switch
    {
        BatteryOverlay.Bolt => Bolt,
        BatteryOverlay.Plug => Plug,
        BatteryOverlay.Leaf => Leaf,
        _ => null
    };

    private static Geometry Freeze(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }
}
