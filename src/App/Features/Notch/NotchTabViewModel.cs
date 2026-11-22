using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Koy.App.Resources;
using Koy.Core.Notch;

namespace Koy.App.Features.Notch;

/// <summary>Açık çentiğin altındaki sekme çubuğunun bir sekmesi: simge, yazı ve sayfa rengi.</summary>
public sealed partial class NotchTabViewModel(NotchPanel panel, string label, Geometry? icon, Brush? accent) : ObservableObject
{
    public NotchPanel Panel { get; } = panel;

    public string Label { get; } = label;

    /// <summary>24x24 tuvalde çizgi simgesi.</summary>
    public Geometry? Icon { get; } = icon;

    /// <summary>Seçiliyken simgenin rengi (sayfa rengi).</summary>
    public Brush? Accent { get; } = accent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionStatus))]
    private bool _isSelected;

    /// <summary>Ekran okuyucu için seçili durumu (renk tek başına taşımaz): "Seçili" ya da boş.</summary>
    public string SelectionStatus => IsSelected ? Strings.Get("TabSelected") : "";
}
