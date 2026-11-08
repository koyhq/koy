using CommunityToolkit.Mvvm.ComponentModel;

namespace Koy.App.Features.Settings;

/// <summary>Ayarlar sayfasında bir arama kaynağının anahtarı.</summary>
public sealed partial class SearchSourceToggle(string id, string name, bool isEnabled, Action changed) : ObservableObject
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    private bool _isEnabled = isEnabled;

    partial void OnIsEnabledChanged(bool value) => changed();
}
