using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Koy.Core.Input;
using Koy.Core.Settings;

namespace Koy.App.Features.Settings;

/// <summary>
/// Kısayollar sayfasındaki "Ekran köşeleri" bölümü. Değerler ComboBox etiketleriyle (metin) eşleşir; karar ve sınırlar
/// Core'da (<see cref="CornerActions"/>). Değişiklik hemen kaydedilir ve köşe takibi yeniden değerlendirilir
/// (hepsi "Yok" ise zamanlayıcı durur). Ana ayar kaydından bağımsızdır, <see cref="ShortcutsViewModel"/> gibi.
/// </summary>
public sealed partial class HotCornersViewModel : ObservableObject
{
    /// <summary>Seçilebilir bekleme süreleri (ms), sayfadaki ComboBox ile aynı.</summary>
    public static readonly int[] DwellOptions = [200, 400, 700, 1000];

    private readonly SettingsStore _store;
    private bool _isLoading;

    [ObservableProperty]
    private string _topLeft = nameof(CornerAction.None);

    [ObservableProperty]
    private string _topRight = nameof(CornerAction.None);

    [ObservableProperty]
    private string _bottomLeft = nameof(CornerAction.None);

    [ObservableProperty]
    private string _bottomRight = nameof(CornerAction.None);

    [ObservableProperty]
    private string _dwell = CornerActions.DefaultDwellMs.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    private bool _disableInFullscreen = true;

    public HotCornersViewModel(SettingsStore store) => _store = store;

    public void Load(AppSettings settings)
    {
        var s = settings.HotCorners;
        _isLoading = true;
        try
        {
            TopLeft = s.TopLeft.ToString();
            TopRight = s.TopRight.ToString();
            BottomLeft = s.BottomLeft.ToString();
            BottomRight = s.BottomRight.ToString();
            Dwell = NearestOption(s.DwellMs).ToString(CultureInfo.InvariantCulture);
            DisableInFullscreen = s.DisableInFullscreen;
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnTopLeftChanged(string value) => Save();
    partial void OnTopRightChanged(string value) => Save();
    partial void OnBottomLeftChanged(string value) => Save();
    partial void OnBottomRightChanged(string value) => Save();
    partial void OnDwellChanged(string value) => Save();
    partial void OnDisableInFullscreenChanged(bool value) => Save();

    private void Save()
    {
        if (_isLoading)
            return;

        var settings = _store.Load();
        _store.Save(settings with
        {
            HotCorners = settings.HotCorners with
            {
                TopLeft = Parse(TopLeft),
                TopRight = Parse(TopRight),
                BottomLeft = Parse(BottomLeft),
                BottomRight = Parse(BottomRight),
                DwellMs = int.TryParse(Dwell, NumberStyles.None, CultureInfo.InvariantCulture, out var ms)
                    ? CornerActions.ClampDwell(ms)
                    : CornerActions.DefaultDwellMs,
                DisableInFullscreen = DisableInFullscreen
            }
        });

        (Application.Current as App)?.ApplyHotCorners();
    }

    private static CornerAction Parse(string name) =>
        Enum.TryParse<CornerAction>(name, ignoreCase: false, out var action) && Enum.IsDefined(action) ? action : CornerAction.None;

    private static int NearestOption(int dwellMs) =>
        DwellOptions.OrderBy(o => Math.Abs(o - dwellMs)).First();
}
