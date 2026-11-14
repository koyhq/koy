using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Notch.LiveBackground;
using Koy.App.Resources;
using Koy.Core.Notch;

namespace Koy.App.Features.Extensions;

/// <summary>
/// Eklentiler sayfasındaki yerleşik canlı arka plan kartı (kodlu efektler ve ambiyans renk geçişleri).
/// Mağaza indirmesi gerektirmez; tıklandığında doğrudan ayarlara uygulanır.
/// </summary>
public sealed partial class BuiltinBackgroundCardViewModel : ObservableObject
{
    private readonly ExtensionStoreViewModel _owner;
    private readonly LiveBackgroundPreset _preset;

    public BuiltinBackgroundCardViewModel(ExtensionStoreViewModel owner, LiveBackgroundPreset preset, bool isApplied)
    {
        _owner = owner;
        _preset = preset;
        _isApplied = isApplied;
        RefreshLanguage();
    }

    public string Id => _preset.Id;

    public bool IsVideo => _preset.VideoFile is not null;

    public BitmapSource Preview => LiveBackgroundPreviewRenderer.Get(_preset.Id);

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _kindLabel = "";

    [ObservableProperty]
    private bool _isApplied;

    public bool ShowApply => !IsApplied;

    partial void OnIsAppliedChanged(bool value) => OnPropertyChanged(nameof(ShowApply));

    [RelayCommand]
    private void Apply() => _owner.ApplyBuiltinBackground(Id);

    [RelayCommand]
    private void Disable() => _owner.DisableBuiltinBackground(Id);

    internal void RefreshLanguage()
    {
        Name = Strings.Get(_preset.NameKey);
        KindLabel = Strings.Get(IsVideo ? "LiveBgKindVideo" : "LiveBgKindAmbient");
    }
}
