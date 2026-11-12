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

    // Varsayılan değişince kaydı olmayan satır onu izler; bu değişiklik satırı "kayıtlı" yapmaz.
    private bool _following;

    /// <summary>Kaydı olmayan satırı yeni simge varsayılanına uydurur.</summary>
    public void FollowDefault(bool shown)
    {
        if (IsExplicit)
            return;

        _following = true;
        IsShown = shown;
        _following = false;
    }

    partial void OnIsShownChanged(bool value)
    {
        if (_following)
            return;

        IsExplicit = true;
        changed();
    }
}
