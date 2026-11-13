using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Calendar;
using Koy.App.Features.ControlCenter;
using Koy.App.Features.Focus;
using Koy.App.Features.SystemInfo;
using Koy.App.Features.Music;
using Koy.App.Features.Notch.LiveBackground;
using Koy.App.Features.Settings;
using Koy.App.Features.Status;
using Koy.App.Resources;
using Koy.Core.Calendar;
using Koy.Core.Diagnostics;
using Koy.Core.Notch;
using Koy.Core.Shell;

namespace Koy.App.Features.Home;

/// <summary>
/// Ana sekmesinin veri kaynağı. İki yüzü var (<see cref="Page"/>): tam sayfa müzik ve Ana (gün, hava, sıradaki
/// etkinlikler, Odak satırı, sistem, hızlı başlat). Yalnız mevcut ViewModel'leri birleştirir.
/// </summary>
public sealed partial class HomeViewModel : ObservableObject, IDisposable
{
    private readonly IShellActionService? _shell;
    private readonly Action? _openFocus;
    private bool _disposed;

    public MusicViewModel Music { get; }

    /// <summary>Ana sekmesinin görünen yüzü: tam sayfa müzik ya da Ana. Çentik her açılışta yeniden seçer (NotchHomePages).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMusicPage), nameof(IsHomePage))]
    private NotchHomePage _page = NotchHomePage.Home;

    public bool IsMusicPage => Page == NotchHomePage.Music;

    public bool IsHomePage => Page == NotchHomePage.Home;

    // Sıradaki etkinlikler: defter, takvimin "gün aç" eylemi ve sayfa görünürken abonelik.
    private SharedEvents? _events;
    private Action<DateOnly>? _openDay;
    private bool _active;
    private string _lastMinute = "";

    /// <summary>En yakın etkinlik (vurgulu kutu); yoksa null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNextEvent))]
    private UpcomingEventRow? _nextEvent;

    public bool HasNextEvent => NextEvent is not null;

    /// <summary>Sıradakinden sonraki en çok iki etkinlik (kısa satırlar).</summary>
    public ObservableCollection<UpcomingEventRow> LaterEvents { get; } = [];

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

    /// <summary>Odak satırı (Zamanlayıcılar kartı açık): çalışırken süre ve denetimler, değilken "Odak başlat".</summary>
    public bool ShowFocusRow => Cards.Contains(HomeCard.Timers);

    /// <summary>Kulaklık/ses cihazı kartı (cihaz var ve Kulaklık kartı açık).</summary>
    public bool ShowHeadphoneCard => (Bluetooth?.HasPrimaryAudio == true) && Cards.Contains(HomeCard.HeadphoneBattery);

    /// <summary>Sistem ve ağ kartı (CPU, RAM, Ağ).</summary>
    public bool ShowSystemCard => Cards.Contains(HomeCard.SystemNetwork);

    /// <summary>Hızlı başlat şeridi.</summary>
    public bool ShowQuickLaunchCard => Cards.Contains(HomeCard.QuickLaunch);

    /// <summary>Hava durumu (Bugün kartı ve üst satır).</summary>
    public bool ShowWeatherCard => Cards.Contains(HomeCard.Weather);

    /// <summary>Sistem kartına tıklanınca Sistem sayfası açılır (NotchViewModel verir).</summary>
    public Action? OpenSystemPage { get; set; }

    [RelayCommand]
    private void OpenSystem() => OpenSystemPage?.Invoke();

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
        Status.PropertyChanged += OnStatusPropertyChanged;
    }

    private void OnStatusPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Saat dakikada bir değişir: "… sonra" metni ve bitmiş etkinliklerin düşmesi için yeter (ayrı zamanlayıcı yok).
        if (e.PropertyName == nameof(StatusViewModel.CurrentTime) && _active && Status.CurrentTime != _lastMinute)
        {
            _lastMinute = Status.CurrentTime;
            RefreshUpcoming();
        }

        if (e.PropertyName == nameof(StatusViewModel.HasWeatherLocation))
        {
            OnPropertyChanged(nameof(ShowWeather));
            OnPropertyChanged(nameof(ShowNoLocation));
        }
    }

    /// <summary>Takvim defterini bağlar; <paramref name="openDay"/> etkinliğe tıklanınca Takvim'i o günde açar.</summary>
    public void AttachCalendar(SharedEvents events, Action<DateOnly> openDay)
    {
        _events = events;
        _openDay = openDay;
    }

    /// <summary>Ana yüzü görünürken sıradaki listesi kurulur ve dakikada bir ("10 sa 13 dk sonra") tazelenir; değilken abonelik yok.</summary>
    public void SetActive(bool active)
    {
        if (_active == active)
            return;

        _active = active;
        if (_events is null)
            return;

        if (active)
        {
            _events.Changed += OnEventsChanged;
            RefreshUpcoming();
        }
        else
        {
            _events.Changed -= OnEventsChanged;
        }
    }

    private void OnEventsChanged(object? sender, EventArgs e) => RefreshUpcoming();

    private void RefreshUpcoming()
    {
        if (_events is null)
            return;

        var now = DateTime.Now;
        var list = EventAgenda.Upcoming(_events.Book.Events, now, 3);
        NextEvent = list.Count > 0 ? UpcomingEventRow.From(list[0], now) : null;
        LaterEvents.Clear();
        foreach (var e in list.Skip(1))
            LaterEvents.Add(UpcomingEventRow.From(e, now));
    }

    [RelayCommand]
    private void OpenEvent(UpcomingEventRow? row)
    {
        if (row is not null)
            _openDay?.Invoke(row.Date);
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
        SetActive(false);
        Status.PropertyChanged -= OnStatusPropertyChanged;
        QuickLaunch.Dispose();
        Stats.Dispose();
    }
}

/// <summary>Ana'daki bir etkinlik satırı: gün ve saat ("Yarın 10:00"), başlık ve kalan süre ("10 sa 13 dk sonra").</summary>
public sealed record UpcomingEventRow(string Id, DateOnly Date, string When, string Title, string Until)
{
    public static UpcomingEventRow From(CalendarEvent e, DateTime now)
    {
        var day = (e.Date.DayNumber - DateOnly.FromDateTime(now).DayNumber) switch
        {
            0 => Strings.Get("HomeToday"),
            1 => Strings.Get("HomeTomorrow"),
            _ => e.Start.ToString("ddd d", System.Globalization.CultureInfo.CurrentCulture)
        };
        var time = e.DurationMinutes == 0 && e.Start.TimeOfDay == TimeSpan.Zero ? "" : " " + e.Start.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        return new(e.Id, e.Date, day + time, e.Title, FormatUntil(EventAgenda.Until(e, now)));
    }

    private static string FormatUntil(TimeSpan left)
    {
        if (left <= TimeSpan.Zero)
            return Strings.Get("HomeEventNow");

        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        if (minutes < 60)
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("HomeEventInMinutes"), minutes);
        if (minutes < 24 * 60)
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("HomeEventInHours"), minutes / 60, minutes % 60);
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("HomeEventInDays"), (int)Math.Round(left.TotalDays));
    }
}
