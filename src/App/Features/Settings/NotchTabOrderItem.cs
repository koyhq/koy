using Koy.App.Features.Notch;
using Koy.Core.Notch;

namespace Koy.App.Features.Settings;

/// <summary>Ayarlar › Çentik › Sekme sırası listesinde bir satır (issue #78).</summary>
public sealed record NotchTabOrderItem(NotchPanel Panel, string Label)
{
    /// <summary>Sekmenin çentikteki adıyla aynı metin (NotchTabs tek kaynak).</summary>
    public static string LabelOf(NotchPanel panel) => NotchTabs.Create(panel, selected: false).Label;
}
