using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Calendar;
using Koy.App.Resources;
using Koy.Core.Calendar;
using Koy.Core.Diagnostics;
using Koy.Platform.Calendar;

namespace Koy.App.Features.Settings;

/// <summary>
/// Ayarlar › Çentik › Takvim hesapları: Google ve Outlook salt okunur OAuth 2.0 PKCE bağlantısı.
/// Sürümün yerleşik istemcisi kullanılır; kimlik alanları yalnız kendi istemcisini kullanmak isteyen için.
/// Belirteçler DPAPI ile şifreli saklanır, etkinlikler yerel önbellekte tutulur.
/// </summary>
public sealed partial class CalendarAccountsViewModel : ObservableObject
{
    private readonly CalendarSettingsStore _store;
    private readonly ICalendarSyncService _sync;
    private readonly CalendarCacheStore _cacheStore;
    private bool _loading;

    [ObservableProperty]
    private string _googleClientId = "";

    [ObservableProperty]
    private string _googleClientSecret = "";

    [ObservableProperty]
    private string _outlookClientId = "";

    [ObservableProperty]
    private bool _isGoogleConnected;

    [ObservableProperty]
    private bool _isOutlookConnected;

    /// <summary>
    /// Outlook bağlanabilir mi: sürümde (ya da ortam değişkeninde / ayarlarda) istemci var veya zaten bağlı.
    /// Yoksa satır "Yakında" der; kimlik alanı ve Bağlan düğmesi gizlenir.
    /// </summary>
    [ObservableProperty]
    private bool _isOutlookAvailable;

    [ObservableProperty]
    private bool _isGoogleBusy;

    [ObservableProperty]
    private bool _isOutlookBusy;

    [ObservableProperty]
    private string _googleStatusText = "";

    [ObservableProperty]
    private string _outlookStatusText = "";

    [ObservableProperty]
    private string _statusMessage = "";

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    public CalendarAccountsViewModel(
        CalendarSettingsStore? store = null,
        ICalendarSyncService? sync = null,
        CalendarCacheStore? cacheStore = null)
    {
        _store = store ?? new CalendarSettingsStore();
        _cacheStore = cacheStore ?? new CalendarCacheStore();
        _sync = sync ?? new CalendarSyncService(cacheStore: _cacheStore);
    }

    public void Load()
    {
        _loading = true;
        try
        {
            var settings = _store.Load();
            GoogleClientId = settings.GoogleClientId;
            GoogleClientSecret = settings.GoogleClientSecret;
            OutlookClientId = settings.OutlookClientId;
            IsGoogleConnected = settings.GoogleEnabled && _sync.HasToken(CalendarProvider.Google);
            IsOutlookConnected = settings.OutlookEnabled && _sync.HasToken(CalendarProvider.Outlook);
            IsOutlookAvailable = IsOutlookConnected || BuiltInCalendarClients.Resolve(CalendarProvider.Outlook, settings).IsConfigured;
            UpdateStatusTexts(settings);
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnGoogleClientIdChanged(string value)
    {
        if (_loading)
            return;

        var current = _store.Load();
        _store.Save(current with { GoogleClientId = value?.Trim() ?? "" });
    }

    partial void OnGoogleClientSecretChanged(string value)
    {
        if (_loading)
            return;

        var current = _store.Load();
        _store.Save(current with { GoogleClientSecret = value?.Trim() ?? "" });
    }

    partial void OnOutlookClientIdChanged(string value)
    {
        if (_loading)
            return;

        var current = _store.Load();
        _store.Save(current with { OutlookClientId = value?.Trim() ?? "" });
    }

    [RelayCommand]
    private async Task ConnectGoogleAsync() => await ConnectAsync(CalendarProvider.Google);

    [RelayCommand]
    private async Task DisconnectGoogleAsync() => await DisconnectAsync(CalendarProvider.Google);

    [RelayCommand]
    private async Task ConnectOutlookAsync() => await ConnectAsync(CalendarProvider.Outlook);

    [RelayCommand]
    private async Task DisconnectOutlookAsync() => await DisconnectAsync(CalendarProvider.Outlook);

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        StatusMessage = Strings.Get("CalSyncing");
        var current = _store.Load();
        var now = DateTimeOffset.UtcNow;
        var from = now.AddDays(-14);
        var to = now.AddDays(60);
        var anyError = false;
        var fromCache = false;

        try
        {
            if (current.GoogleEnabled && _sync.HasToken(CalendarProvider.Google))
            {
                var client = BuiltInCalendarClients.Resolve(CalendarProvider.Google, current);
                var res = await _sync.SyncAsync(CalendarProvider.Google, client, from, to);
                if (!res.Success)
                {
                    anyError = true;
                    fromCache |= res.FromCache;
                }
            }

            if (current.OutlookEnabled && _sync.HasToken(CalendarProvider.Outlook))
            {
                var client = BuiltInCalendarClients.Resolve(CalendarProvider.Outlook, current);
                var res = await _sync.SyncAsync(CalendarProvider.Outlook, client, from, to);
                if (!res.Success)
                {
                    anyError = true;
                    fromCache |= res.FromCache;
                }
            }

            var updated = current with { LastSyncUnix = now.ToUnixTimeSeconds() };
            _store.Save(updated);
            UpdateStatusTexts(updated);

            StatusMessage = anyError
                ? Strings.Get(fromCache ? "CalSyncOfflineCache" : "CalSyncFailed")
                : Strings.Get("CalSyncDone");
        }
        catch (Exception ex)
        {
            CrashLog.Write("Takvim eşitlemesi", ex);
            StatusMessage = Strings.Get("CalSyncOfflineCache");
        }
    }

    private async Task ConnectAsync(CalendarProvider provider)
    {
        var current = _store.Load();
        var client = BuiltInCalendarClients.Resolve(provider, current);
        if (!client.IsConfigured)
        {
            StatusMessage = Strings.Get("CalClientIdRequired");
            return;
        }

        SetBusy(provider, true);
        StatusMessage = Strings.Get("CalAuthorizing");
        try
        {
            var result = await _sync.ConnectAsync(provider, client);
            if (!result.Success)
            {
                StatusMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("CalConnectFailed"), result.Error ?? "");
                return;
            }

            var updated = provider == CalendarProvider.Google
                ? current with { GoogleEnabled = true, GoogleAccountLabel = result.AccountLabel }
                : current with { OutlookEnabled = true, OutlookAccountLabel = result.AccountLabel };
            _store.Save(updated);

            if (provider == CalendarProvider.Google)
                IsGoogleConnected = true;
            else
                IsOutlookConnected = true;

            var now = DateTimeOffset.UtcNow;
            await _sync.SyncAsync(provider, client, now.AddDays(-14), now.AddDays(60));
            UpdateStatusTexts(updated);
            StatusMessage = Strings.Get("CalConnectedDone");
        }
        catch (Exception ex)
        {
            CrashLog.Write("Takvim OAuth bağlantısı", ex);
            StatusMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("CalConnectFailed"), ex.Message);
        }
        finally
        {
            SetBusy(provider, false);
        }
    }

    private async Task DisconnectAsync(CalendarProvider provider)
    {
        SetBusy(provider, true);
        try
        {
            await _sync.DisconnectAsync(provider);
            var current = _store.Load();
            var updated = provider == CalendarProvider.Google
                ? current with { GoogleEnabled = false, GoogleAccountLabel = "" }
                : current with { OutlookEnabled = false, OutlookAccountLabel = "" };
            _store.Save(updated);

            if (provider == CalendarProvider.Google)
                IsGoogleConnected = false;
            else
                IsOutlookConnected = false;

            UpdateStatusTexts(updated);
            StatusMessage = Strings.Get("CalDisconnectedDone");
        }
        finally
        {
            SetBusy(provider, false);
        }
    }

    private void SetBusy(CalendarProvider provider, bool busy)
    {
        if (provider == CalendarProvider.Google)
            IsGoogleBusy = busy;
        else
            IsOutlookBusy = busy;
    }

    private void UpdateStatusTexts(CalendarSettings s)
    {
        GoogleStatusText = IsGoogleConnected
            ? (string.IsNullOrWhiteSpace(s.GoogleAccountLabel)
                ? Strings.Get("CalStatusConnected")
                : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("CalStatusConnectedAs"), s.GoogleAccountLabel))
            : Strings.Get("CalStatusDisconnected");

        OutlookStatusText = !IsOutlookAvailable
            ? Strings.Get("CalComingSoon")
            : IsOutlookConnected
            ? (string.IsNullOrWhiteSpace(s.OutlookAccountLabel)
                ? Strings.Get("CalStatusConnected")
                : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("CalStatusConnectedAs"), s.OutlookAccountLabel))
            : Strings.Get("CalStatusDisconnected");
    }
}
