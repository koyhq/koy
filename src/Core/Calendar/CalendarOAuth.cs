using System.Security.Cryptography;
using System.Text;

namespace Koy.Core.Calendar;

/// <summary>Desteklenen harici takvim OAuth sağlayıcıları (salt okunur).</summary>
public enum CalendarProvider
{
    Google = 1,
    Outlook = 2
}

/// <summary>OAuth 2.0 yetkilendirme akışı durumu.</summary>
public enum OAuthFlowState
{
    Disconnected = 0,
    Authorizing = 1,
    ExchangingToken = 2,
    Connected = 3,
    Refreshing = 4,
    Error = 5,
    Cancelled = 6
}

/// <summary>OAuth PKCE yetkilendirme geri dönüşünün doğrulanmış sonucu.</summary>
public sealed record OAuthCallbackResult(bool Success, string? Code = null, string? Error = null)
{
    public static OAuthCallbackResult Ok(string code) => new(true, Code: code);
    public static OAuthCallbackResult Fail(string error) => new(false, Error: error);
}

/// <summary>
/// Sağlayıcı başına OAuth 2.0 PKCE uç noktaları ve salt okunur kapsamlar.
/// PKCE akışı kullanılır. Depoda istemci kimliği ya da sırrı yoktur; sürümde CI gizli anahtarlarından gömülür.
/// </summary>
public static class CalendarOAuthConfig
{
    public const string GoogleScope = "https://www.googleapis.com/auth/calendar.readonly";
    public const string OutlookScope = "Calendars.Read offline_access";

    public const string GoogleClientIdEnvVar = "KOY_GOOGLE_CLIENT_ID";
    public const string GoogleClientSecretEnvVar = "KOY_GOOGLE_CLIENT_SECRET";
    public const string OutlookClientIdEnvVar = "KOY_OUTLOOK_CLIENT_ID";

    public static CalendarEventSource ToEventSource(CalendarProvider provider) => provider switch
    {
        CalendarProvider.Google => CalendarEventSource.Google,
        CalendarProvider.Outlook => CalendarEventSource.Outlook,
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static string GetAuthorizationEndpoint(CalendarProvider provider) => provider switch
    {
        CalendarProvider.Google => "https://accounts.google.com/o/oauth2/v2/auth",
        CalendarProvider.Outlook => "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static string GetTokenEndpoint(CalendarProvider provider) => provider switch
    {
        CalendarProvider.Google => "https://oauth2.googleapis.com/token",
        CalendarProvider.Outlook => "https://login.microsoftonline.com/common/oauth2/v2.0/token",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static string GetReadOnlyScope(CalendarProvider provider) => provider switch
    {
        CalendarProvider.Google => GoogleScope,
        CalendarProvider.Outlook => OutlookScope,
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    /// <summary>
    /// Kullanılacak OAuth istemcisini seçer. Sıra: ayarlar → ortam değişkeni → sürüme gömülü yerleşik istemci.
    /// Gizli anahtar yalnız kendi kimliğiyle birlikte kullanılır: ayarlarda kimlik varsa ayarlardaki sır, ortam
    /// değişkeninde kimlik varsa ortamdaki sır, yoksa yerleşik sır. Hiçbiri yoksa boş istemci döner.
    /// </summary>
    public static OAuthClient ResolveClient(
        CalendarProvider provider,
        CalendarSettings settings,
        OAuthBuiltInClients? builtIn = null,
        Func<string, string?>? getEnv = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        getEnv ??= Environment.GetEnvironmentVariable;

        var (configuredId, configuredSecret) = provider switch
        {
            CalendarProvider.Google => (settings.GoogleClientId, settings.GoogleClientSecret),
            CalendarProvider.Outlook => (settings.OutlookClientId, ""),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        if (!string.IsNullOrWhiteSpace(configuredId))
            return new OAuthClient(configuredId.Trim(), Trim(configuredSecret));

        var (idVar, secretVar) = provider switch
        {
            CalendarProvider.Google => (GoogleClientIdEnvVar, GoogleClientSecretEnvVar),
            _ => (OutlookClientIdEnvVar, null)
        };
        var envId = getEnv(idVar);
        if (!string.IsNullOrWhiteSpace(envId))
            return new OAuthClient(envId.Trim(), secretVar is null ? "" : Trim(getEnv(secretVar)));

        return provider == CalendarProvider.Google
            ? new OAuthClient(Trim(builtIn?.GoogleClientId), Trim(builtIn?.GoogleClientSecret))
            : new OAuthClient(Trim(builtIn?.OutlookClientId));
    }

    private static string Trim(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}

/// <summary>
/// Token isteğinde kullanılan OAuth istemcisi. Google masaüstü istemcileri PKCE ile bile token uç noktasında
/// <c>client_secret</c> ister (Google bu sırrı gizli saymaz); Outlook genel istemcisinde sır boştur.
/// </summary>
public sealed record OAuthClient(string ClientId, string ClientSecret = "")
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>Token isteği formuna istemci alanlarını ekler; sır boşsa <c>client_secret</c> gönderilmez.</summary>
    public void AddTo(IDictionary<string, string> form)
    {
        form["client_id"] = ClientId;
        if (!string.IsNullOrWhiteSpace(ClientSecret))
            form["client_secret"] = ClientSecret;
    }
}

/// <summary>Sürüm derlenirken CI gizli anahtarlarından gömülen yerleşik istemciler (yerel derlemede boş).</summary>
public sealed record OAuthBuiltInClients(string GoogleClientId = "", string GoogleClientSecret = "", string OutlookClientId = "");

/// <summary>RFC 7636 PKCE (S256) üretimi, yetkilendirme URL'si oluşturma ve loopback callback doğrulama.</summary>
public static class OAuthPkce
{
    public static string CreateCodeVerifier(int byteLength = 32)
    {
        if (byteLength < 32)
            throw new ArgumentOutOfRangeException(nameof(byteLength), "PKCE code_verifier en az 32 bayt olmalıdır.");

        return Base64UrlEncode(RandomNumberGenerator.GetBytes(byteLength));
    }

    public static string ComputeCodeChallenge(string codeVerifier)
    {
        if (string.IsNullOrWhiteSpace(codeVerifier))
            throw new ArgumentException("codeVerifier boş olamaz.", nameof(codeVerifier));

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    public static string CreateState() => Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

    public static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static Uri BuildAuthorizationUrl(
        CalendarProvider provider,
        string clientId,
        string redirectUri,
        string codeChallenge,
        string state)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("clientId boş olamaz.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new ArgumentException("redirectUri boş olamaz.", nameof(redirectUri));

        var endpoint = CalendarOAuthConfig.GetAuthorizationEndpoint(provider);
        var scope = CalendarOAuthConfig.GetReadOnlyScope(provider);

        var query = new List<string>
        {
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_type=code",
            $"scope={Uri.EscapeDataString(scope)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256",
            $"state={Uri.EscapeDataString(state)}"
        };

        if (provider == CalendarProvider.Google)
        {
            query.Add("access_type=offline");
            query.Add("prompt=consent");
        }

        return new Uri($"{endpoint}?{string.Join("&", query)}");
    }

    public static OAuthCallbackResult ValidateCallback(Uri? callbackUri, string expectedState)
    {
        if (callbackUri is null)
            return OAuthCallbackResult.Fail("missing_callback");

        var query = ParseQueryString(callbackUri.Query);
        if (query.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
            return OAuthCallbackResult.Fail(error);

        if (!query.TryGetValue("state", out var actualState) ||
            !string.Equals(actualState, expectedState, StringComparison.Ordinal))
        {
            return OAuthCallbackResult.Fail("state_mismatch");
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            return OAuthCallbackResult.Fail("missing_code");

        return OAuthCallbackResult.Ok(code);
    }

    public static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query))
            return result;

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                result[Uri.UnescapeDataString(pair)] = "";
            }
            else
            {
                var key = Uri.UnescapeDataString(pair[..eq].Replace('+', ' '));
                var val = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
                result[key] = val;
            }
        }

        return result;
    }
}

/// <summary>OAuth oturum akışının saf durum makinesi.</summary>
public sealed class OAuthSessionState
{
    public OAuthFlowState State { get; private set; } = OAuthFlowState.Disconnected;
    public string? ErrorCode { get; private set; }
    public bool IsBusy => State is OAuthFlowState.Authorizing or OAuthFlowState.ExchangingToken or OAuthFlowState.Refreshing;

    public void SetInitialConnected(bool connected)
    {
        State = connected ? OAuthFlowState.Connected : OAuthFlowState.Disconnected;
        ErrorCode = null;
    }

    public bool TryBeginAuthorize()
    {
        if (IsBusy)
            return false;

        State = OAuthFlowState.Authorizing;
        ErrorCode = null;
        return true;
    }

    public void BeginTokenExchange()
    {
        if (State == OAuthFlowState.Authorizing)
            State = OAuthFlowState.ExchangingToken;
    }

    public void BeginRefresh()
    {
        if (!IsBusy)
        {
            State = OAuthFlowState.Refreshing;
            ErrorCode = null;
        }
    }

    public void CompleteConnected()
    {
        State = OAuthFlowState.Connected;
        ErrorCode = null;
    }

    public void Cancel()
    {
        State = OAuthFlowState.Cancelled;
        ErrorCode = null;
    }

    public void Fail(string errorCode)
    {
        State = OAuthFlowState.Error;
        ErrorCode = errorCode;
    }

    public void Disconnect()
    {
        State = OAuthFlowState.Disconnected;
        ErrorCode = null;
    }
}
