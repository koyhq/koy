using CommunityToolkit.Mvvm.ComponentModel;
using Koy.App.Resources;
using Koy.Core.Notch;

namespace Koy.App.Features.Settings;

/// <summary>
/// Ayarlar › Çentik › Ana sayfa kartları listesinde bir satır (issue #78).
/// </summary>
public sealed partial class HomeCardOrderItem : ObservableObject
{
    public HomeCard Card { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isEnabled;

    private readonly Action _onChanged;

    public HomeCardOrderItem(HomeCard card, string label, bool isEnabled, Action onChanged)
    {
        Card = card;
        Label = label;
        _isEnabled = isEnabled;
        _onChanged = onChanged;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _onChanged();
    }

    public static string LabelOf(HomeCard card) => card switch
    {
        HomeCard.Music => Loc.Instance["HomeCardMusic"],
        HomeCard.HeadphoneBattery => Loc.Instance["HomeCardHeadphoneBattery"],
        HomeCard.SystemNetwork => Loc.Instance["HomeCardSystemNetwork"],
        HomeCard.QuickLaunch => Loc.Instance["HomeCardQuickLaunch"],
        HomeCard.Weather => Loc.Instance["HomeCardWeather"],
        HomeCard.Timers => Loc.Instance["HomeCardTimers"],
        _ => card.ToString()
    };
}
