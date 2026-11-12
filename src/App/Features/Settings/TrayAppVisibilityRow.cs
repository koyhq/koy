using CommunityToolkit.Mvvm.ComponentModel;

namespace Koy.App.Features.Settings;

/// <summary>
/// Ayarlar › Dock'taki tepsi uygulaması satırı (issue #77). Açık = dock tepsi penceresinde görünür, kapalı = gizli.
/// Kullanıcı satırı bir kez değiştirince kaydı olur (<see cref="IsExplicit"/>); kaydı olmayan satır yeni simge varsayılanını izler.
/// </summary>
public sealed partial class TrayAppVisibilityRow(string exeName, bool isShown, bool isExplicit, Action changed) : ObservableObject
{
    public string ExeName { get; } = exeName;

    public bool IsExplicit { get; private set; } = isExplicit;

    [ObservableProperty]
    private bool _isShown = isShown;

    partial void OnIsShownChanged(bool value)
    {
        IsExplicit = true;
        changed();
    }
}
