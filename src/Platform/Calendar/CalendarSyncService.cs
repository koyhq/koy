using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using Koy.Core.Calendar;

namespace Koy.Platform.Calendar;

/// <summary>
/// Google Calendar API v3 ve Microsoft Graph Outlook Calendar için OAuth 2.0 PKCE + salt okunur eşitleme servisi.
/// Google için istemci sırrı token isteğine eklenir (<see cref="OAuthClient"/>); belirteçler DPAPI ile şifreli
/// saklanır, etkinlikler yerel önbellekte tutulur.
/// </summary>
public sealed class CalendarSyncService : ICalendarSyncService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly CalendarTokenStore _tokenStore;
    private readonly CalendarCacheStore _cacheStore;

    public CalendarSyncService(CalendarTokenStore? tokenStore = null, CalendarCacheStore? cacheStore = null)
    {
        _tokenStore = tokenStore ?? new CalendarTokenStore(new DpapiTokenProtector());
        _cacheStore = cacheStore ?? new CalendarCacheStore();
    }

    public bool HasToken(CalendarProvider provider) => _tokenStore.Exists(provider);

    public async Task<OAuthConnectResult> ConnectAsync(CalendarProvider provider, OAuthClient client, CancellationToken ct = default)
    {
        if (!client.IsConfigured)
            return new OAuthConnectResult(false, Error: "missing_client_id");

        var codeVerifier = OAuthPkce.CreateCodeVerifier();
        var codeChallenge = OAuthPkce.ComputeCodeChallenge(codeVerifier);
        var state = OAuthPkce.CreateState();
        var port = LoopbackOAuthListener.AllocateFreePort();
        var redirectUri = $"http://127.0.0.1:{port}/callback/";

        var callback = await LoopbackOAuthListener.AuthorizeAsync(
            provider,
            client.ClientId,
            codeChallenge,
            state,
            redirectUri,
            TimeSpan.FromMinutes(3),
            ct).ConfigureAwait(false);

        if (!callback.Success || string.IsNullOrEmpty(callback.Code))
            return new OAuthConnectResult(false, Error: callback.Error ?? "auth_failed");

        try
        {
            var tokenSet = await ExchangeCodeAsync(provider, client, callback.Code, redirectUri, codeVerifier, ct)
                .ConfigureAwait(false);
            if (tokenSet is null)
                return new OAuthConnectResult(false, Error: "token_exchange_failed");

            _tokenStore.Save(provider, tokenSet);
            return new OAuthConnectResult(true, tokenSet.AccountLabel);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new OAuthConnectResult(false, Error: ex.Message);
        }
    }

    public Task DisconnectAsync(CalendarProvider provider, CancellationToken ct = default)
    {
        _tokenStore.Delete(provider);
        _cacheStore.RemoveProvider(CalendarOAuthConfig.ToEventSource(provider));
        return Task.CompletedTask;
    }

    public async Task<CalendarSyncResult> SyncAsync(
        CalendarProvider provider,
        OAuthClient client,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct = default)
    {
        var source = CalendarOAuthConfig.ToEventSource(provider);
        var tokens = _tokenStore.Load(provider);
        if (tokens is null)
            return LoadCachedForProvider(source, "missing_token");

        try
        {
            var now = DateTimeOffset.UtcNow;
            if (tokens.IsExpired(now))
            {
                if (string.IsNullOrWhiteSpace(tokens.RefreshToken) || !client.IsConfigured)
                    return LoadCachedForProvider(source, "token_expired");

                var refreshed = await RefreshTokenAsync(provider, client, tokens, ct).ConfigureAwait(false);
                if (refreshed is null)
                    return LoadCachedForProvider(source, "refresh_failed");

                tokens = refreshed;
                _tokenStore.Save(provider, tokens);
            }

            var events = provider == CalendarProvider.Google
                ? await FetchGoogleEventsAsync(tokens.AccessToken, fromUtc, toUtc, ct).ConfigureAwait(false)
                : await FetchOutlookEventsAsync(tokens.AccessToken, fromUtc, toUtc, ct).ConfigureAwait(false);

            UpdateCache(source, events, DateTimeOffset.UtcNow);
            return new CalendarSyncResult(true, events, FromCache: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return LoadCachedForProvider(source, ex.Message);
        }
    }

    private CalendarSyncResult LoadCachedForProvider(CalendarEventSource source, string error)
    {
        var snap = _cacheStore.Load();
        var cached = snap.Events.Where(e => e.Source == source).ToList();
        return new CalendarSyncResult(false, cached, FromCache: true, Error: error);
    }

    private void UpdateCache(CalendarEventSource source, IReadOnlyList<CalendarEvent> fresh, DateTimeOffset syncUtc)
    {
        var current = _cacheStore.Load();
        var merged = current.Events.Where(e => e.Source != source).ToList();
        merged.AddRange(fresh);
        _cacheStore.Save(new CalendarCacheSnapshot(syncUtc, merged));
    }

    private static async Task<OAuthTokenSet?> ExchangeCodeAsync(
        CalendarProvider provider,
        OAuthClient client,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        };
        client.AddTo(form);

        using var req = new HttpRequestMessage(HttpMethod.Post, CalendarOAuthConfig.GetTokenEndpoint(provider))
        {
            Content = new FormUrlEncodedContent(form)
        };
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return null;

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return CalendarEventParsers.ParseTokenResponse(json, DateTimeOffset.UtcNow);
    }

    private static async Task<OAuthTokenSet?> RefreshTokenAsync(
        CalendarProvider provider,
        OAuthClient client,
        OAuthTokenSet current,
        CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["refresh_token"] = current.RefreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = CalendarOAuthConfig.GetReadOnlyScope(provider)
        };
        client.AddTo(form);

        using var req = new HttpRequestMessage(HttpMethod.Post, CalendarOAuthConfig.GetTokenEndpoint(provider))
        {
            Content = new FormUrlEncodedContent(form)
        };
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return null;

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return CalendarEventParsers.ParseTokenResponse(json, DateTimeOffset.UtcNow, current.RefreshToken, current.AccountLabel);
    }

    private static async Task<IReadOnlyList<CalendarEvent>> FetchGoogleEventsAsync(
        string accessToken,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct)
    {
        var min = Uri.EscapeDataString(fromUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        var max = Uri.EscapeDataString(toUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        var url = $"https://www.googleapis.com/calendar/v3/calendars/primary/events?timeMin={min}&timeMax={max}&singleEvents=true&orderBy=startTime&maxResults=250";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return CalendarEventParsers.ParseGoogleEvents(json);
    }

    private static async Task<IReadOnlyList<CalendarEvent>> FetchOutlookEventsAsync(
        string accessToken,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct)
    {
        var start = Uri.EscapeDataString(fromUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        var end = Uri.EscapeDataString(toUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        var url = $"https://graph.microsoft.com/v1.0/me/calendarView?startDateTime={start}&endDateTime={end}&$top=250&$select=id,subject,start,end,isAllDay,isCancelled";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"UTC\"");
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return CalendarEventParsers.ParseOutlookEvents(json);
    }
}
