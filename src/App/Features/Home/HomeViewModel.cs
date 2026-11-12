using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.ControlCenter;
using Koy.App.Features.Focus;
using Koy.App.Features.SystemInfo;
using Koy.App.Features.Music;
using Koy.App.Features.Notch.LiveBackground;
using Koy.App.Features.Settings;
using Koy.App.Features.Status;
using Koy.Core.Diagnostics;
using Koy.Core.Notch;
using Koy.Core.Shell;

namespace Koy.App.Features.Home;

/// <summary>
/// Ana sayfanın veri kaynağı: müzik (çalıyorsa kart, değilse selamlama + saat), hava durumu, çalışan zamanlayıcının
/// satırı ve Bildirim Merkezi komutu. Yalnız mevcut ViewModel'leri birleştirir.
/// </summary>
public sealed partial class HomeViewModel : ObservableObject, IDisposable
{
    private readonly IShellActionService? _shell;
    private readonly Action? _openFocus;
    private bool _disposed;

    public MusicViewModel Music { get; }

    /// <summary>Müzik kartı zemini için canlı arka plan kararı (NotchViewModel verir; yoksa kart eski zeminini korur).</summary>
    public LiveBackgroundState? LiveBackground { get; init; }
    public StatusViewModel Status { get; }
    public FocusViewModel Focus { get; }

    /// <summary>CPU / RAM ve ağ kartlarının verisi (servis yalnız Ana ya da Sistem görünürken alınır).</summary>
    public SystemStatsViewModel Stats { get; }

    /// <summary>Kulaklık kartının kaynağı (bağlı ses cihazı varsa kart görünür). Sahibi Kontrol ViewModel'idir.</summary>
    public BluetoothDevicesViewModel? Bluetooth { get; }

    /// <summary>Hızlı başlat şeridi.</summary>
    public QuickLaunchViewModel QuickLaunch { get; }

    /// <summary>Aktif kartlar ve sıraları.</summary>
    public IReadOnlyList<HomeCard> Cards { get; }

    /// <summary>Sol kartta müzik gösterilir (parça var ve Müzik kartı açık).</summary>
    public bool ShowMusic => Music.HasTrack && Cards.Contains(HomeCard.Music);

    /// <summary>Sol kartta selamlama ve büyük saat gösterilir (parça yok veya müzik kartı kapalı).</summary>
    public bool ShowIdle => !ShowMusic;

    /// <summary>Odak zamanlayıcı butonu (zamanlayıcı çalışıyor ve Zamanlayıcılar kartı açık).</summary>
    public bool ShowFocusCard => Focus.HasRunning && Cards.Contains(HomeCard.Timers);

    /// <summary>Kulaklık/ses cihazı kartı (cihaz var ve Kulaklık kartı açık).</summary>
    public bool ShowHeadphoneCard => (Bluetooth?.HasPrimaryAudio == true) && Cards.Contains(HomeCard.HeadphoneBattery);

    /// <summary>Sistem ve ağ kartı (CPU, RAM, Ağ).</summary>
    public bool ShowSystemCard => Cards.Contains(HomeCard.SystemNetwork);

    /// <summary>Hızlı başlat şeridi.</summary>
    public bool ShowQuickLaunchCard => Cards.Contains(HomeCard.QuickLaunch);

    /// <summary>Hava durumu kartı (durum satırı/ana sayfa).</summary>
    public bool ShowWeatherCard => Cards.Contains(HomeCard.Weather);

    /// <summary>
    /// Kullanıcı müzik kartını büyüttü mü (karta tıklayınca açılır/kapanır). Çentik kapanıp açılınca korunur;
    /// parça yokken etkisizdir (kartta saat vardır), parça gelince son seçim geri gelir.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMusicCompact), nameof(ShowMusicLarge), nameof(ShowSideColumn))]
    private bool _isMusicExpanded;

    /// <summary>Müzik kartı küçük düzende (sol sütun).</summary>
    public bool ShowMusicCompact => ShowMusic && !IsMusicExpanded;

    /// <summary>Müzik kartı büyük düzende: Ana gövdesinin tamamını kaplar, sağ sütun gizlenir. Çentik boyutu değişmez.</summary>
    public bool ShowMusicLarge => ShowMusic && IsMusicExpanded;

    /// <summary>Sağ sütun (kulaklık, CPU/RAM, ağ, hızlı başlat) yalnız müzik kartı büyük değilken ve en az bir kart etkinken görünür.</summary>
    public bool ShowSideColumn => !ShowMusicLarge && (ShowHeadphoneCard || ShowSystemCard || ShowQuickLaunchCard);

    /// <summary>Müzik kartına (düğme ve ilerleme çubuğu dışında) tıklanınca büyür ya da eski boyutuna döner.</summary>
    [RelayCommand]
    private void ToggleMusicExpanded()
    {
        if (ShowMusic)
            IsMusicExpanded = !IsMusicExpanded;
    }

    /// <summary>Hava durumu kartı konum seçilmişse hava gösterir, seçilmemişse konum seçmeye yönlendirir.</summary>
    public bool ShowWeather => Status.HasWeatherLocation && ShowWeatherCard;

    public bool ShowNoLocation => !Status.HasWeatherLocation && ShowWeatherCard;

    public HomeViewModel(MusicViewModel music, StatusViewModel status, FocusViewModel focus, SystemStatsViewModel stats, QuickLaunchViewModel quickLaunch,
        IShellActionService? shell = null, Action? openFocus = null, BluetoothDevicesViewModel? bluetooth = null, IEnumerable<HomeCard>? homeCards = null)
    {
        Cards = HomeCardLayout.Normalize(homeCards);
        Bluetooth = bluetooth;
        Stats = stats;
        QuickLaunch = quickLaunch;
        Music = music;
        Status = status;
        Focus = focus;
        _shell = shell;
        _openFocus = openFocus;
        Music.PropertyChanged += OnMusicPropertyChanged;
        Status.PropertyChanged += OnStatusPropertyChanged;
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MusicViewModel.HasTrack))
        {
            OnPropertyChanged(nameof(ShowMusic));
            OnPropertyChanged(nameof(ShowIdle));
            OnPropertyChanged(nameof(ShowMusicCompact));
            OnPropertyChanged(nameof(ShowMusicLarge));
            OnPropertyChanged(nameof(ShowSideColumn));
        }
    }

    private void OnStatusPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StatusViewModel.HasWeatherLocation))
        {
            OnPropertyChanged(nameof(ShowWeather));
            OnPropertyChanged(nameof(ShowNoLocation));
        }
    }

    /// <summary>Win+N ile Windows Bildirim Merkezi'ni açar (SendInput; <c>ms-actioncenter:</c> Hızlı Ayarlar'ı açar, bildirimleri değil).</summary>
    [RelayCommand]
    private void OpenNotificationCenter()
    {
        try
        {
            _shell?.OpenNotificationCenter();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bildirim Merkezi açılamadı", ex);
        }
    }

    /// <summary>"Odak · 18 dk kaldı" satırına tıklanınca Odak sayfası açılır.</summary>
    [RelayCommand]
    private void OpenFocus() => _openFocus?.Invoke();

    /// <summary>Konum seçilmemişse Koy penceresinin Çentik sayfası açılır (hava konumu orada seçilir).</summary>
    [RelayCommand]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "RelayCommand")]
    private void ChooseWeatherLocation() => KoyWindow.ShowInstance(1);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Music.PropertyChanged -= OnMusicPropertyChanged;
        Status.PropertyChanged -= OnStatusPropertyChanged;
        QuickLaunch.Dispose();
        Stats.Dispose();
    }
}
