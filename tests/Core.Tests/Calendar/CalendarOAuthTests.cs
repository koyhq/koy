using System.Security.Cryptography;
using System.Text;
using Koy.Core.Calendar;

namespace Koy.Core.Tests.Calendar;

public sealed class CalendarOAuthTests
{
    [Fact]
    public void Pkce_VerifierAndChallenge_ProduceValidS256Hash()
    {
        var verifier = OAuthPkce.CreateCodeVerifier();
        Assert.True(verifier.Length >= 43);
        Assert.DoesNotContain('=', verifier);
        Assert.DoesNotContain('+', verifier);
        Assert.DoesNotContain('/', verifier);

        var challenge = OAuthPkce.ComputeCodeChallenge(verifier);
        var expected = OAuthPkce.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        Assert.Equal(expected, challenge);
    }

    [Fact]
    public void BuildAuthorizationUrl_UsesReadOnlyScopesAndNoSecret()
    {
        var googleUrl = OAuthPkce.BuildAuthorizationUrl(
            CalendarProvider.Google,
            "google-client-123",
            "http://127.0.0.1:50001/callback/",
            "challenge123",
            "state456");

        var googleQuery = OAuthPkce.ParseQueryString(googleUrl.Query);
        Assert.Equal("google-client-123", googleQuery["client_id"]);
        Assert.Equal("code", googleQuery["response_type"]);
        Assert.Equal("S256", googleQuery["code_challenge_method"]);
        Assert.Equal(CalendarOAuthConfig.GoogleScope, googleQuery["scope"]);
        Assert.False(googleQuery.ContainsKey("client_secret"));

        var outlookUrl = OAuthPkce.BuildAuthorizationUrl(
            CalendarProvider.Outlook,
            "outlook-client-456",
            "http://127.0.0.1:50002/callback/",
            "challenge123",
            "state789");

        var outlookQuery = OAuthPkce.ParseQueryString(outlookUrl.Query);
        Assert.Equal("outlook-client-456", outlookQuery["client_id"]);
        Assert.Equal(CalendarOAuthConfig.OutlookScope, outlookQuery["scope"]);
        Assert.False(outlookQuery.ContainsKey("client_secret"));
    }

    [Fact]
    public void ValidateCallback_VerifiesStateAndExtractsCode()
    {
        var ok = OAuthPkce.ValidateCallback(
            new Uri("http://127.0.0.1:50001/callback/?code=abc123&state=xyz"),
            "xyz");
        Assert.True(ok.Success);
        Assert.Equal("abc123", ok.Code);

        var mismatch = OAuthPkce.ValidateCallback(
            new Uri("http://127.0.0.1:50001/callback/?code=abc123&state=wrong"),
            "xyz");
        Assert.False(mismatch.Success);
        Assert.Equal("state_mismatch", mismatch.Error);

        var denied = OAuthPkce.ValidateCallback(
            new Uri("http://127.0.0.1:50001/callback/?error=access_denied&state=xyz"),
            "xyz");
        Assert.False(denied.Success);
        Assert.Equal("access_denied", denied.Error);
    }

    [Fact]
    public void ResolveClient_PrefersSettingsThenEnvironmentThenBuiltIn()
    {
        var builtIn = new OAuthBuiltInClients("built-g", "built-secret", "built-o");
        Func<string, string?> env = name => name switch
        {
            CalendarOAuthConfig.GoogleClientIdEnvVar => "env-g",
            CalendarOAuthConfig.GoogleClientSecretEnvVar => "env-secret",
            CalendarOAuthConfig.OutlookClientIdEnvVar => "env-o",
            _ => null
        };

        var fromSettings = CalendarOAuthConfig.ResolveClient(CalendarProvider.Google,
            new CalendarSettings { GoogleClientId = " cfg-g ", GoogleClientSecret = " cfg-secret " }, builtIn, env);
        Assert.Equal(new OAuthClient("cfg-g", "cfg-secret"), fromSettings);

        var fromEnv = CalendarOAuthConfig.ResolveClient(CalendarProvider.Google, new CalendarSettings(), builtIn, env);
        Assert.Equal(new OAuthClient("env-g", "env-secret"), fromEnv);

        var fromBuiltIn = CalendarOAuthConfig.ResolveClient(CalendarProvider.Google, new CalendarSettings(), builtIn, _ => null);
        Assert.Equal(new OAuthClient("built-g", "built-secret"), fromBuiltIn);

        Assert.Equal(new OAuthClient("env-o"), CalendarOAuthConfig.ResolveClient(CalendarProvider.Outlook, new CalendarSettings(), builtIn, env));
        Assert.Equal(new OAuthClient("built-o"), CalendarOAuthConfig.ResolveClient(CalendarProvider.Outlook, new CalendarSettings(), builtIn, _ => null));
    }

    [Fact]
    public void ResolveClient_NeverPairsBuiltInSecretWithUserClientId()
    {
        var builtIn = new OAuthBuiltInClients("built-g", "built-secret");
        var client = CalendarOAuthConfig.ResolveClient(CalendarProvider.Google,
            new CalendarSettings { GoogleClientId = "my-id" }, builtIn, _ => null);

        Assert.Equal("my-id", client.ClientId);
        Assert.Equal("", client.ClientSecret);
    }

    [Fact]
    public void ResolveClient_IsNotConfiguredWithoutAnySource()
    {
        var client = CalendarOAuthConfig.ResolveClient(CalendarProvider.Google, new CalendarSettings(), null, _ => null);
        Assert.False(client.IsConfigured);
    }

    [Fact]
    public void OAuthClient_AddsSecretOnlyWhenPresent()
    {
        var withSecret = new Dictionary<string, string>();
        new OAuthClient("id", "secret").AddTo(withSecret);
        Assert.Equal("id", withSecret["client_id"]);
        Assert.Equal("secret", withSecret["client_secret"]);

        var publicClient = new Dictionary<string, string>();
        new OAuthClient("id").AddTo(publicClient);
        Assert.Equal("id", publicClient["client_id"]);
        Assert.False(publicClient.ContainsKey("client_secret"));
    }

    [Fact]
    public void SessionState_FollowsValidTransitions()
    {
        var session = new OAuthSessionState();
        Assert.Equal(OAuthFlowState.Disconnected, session.State);
        Assert.False(session.IsBusy);

        Assert.True(session.TryBeginAuthorize());
        Assert.Equal(OAuthFlowState.Authorizing, session.State);
        Assert.True(session.IsBusy);
        Assert.False(session.TryBeginAuthorize());

        session.BeginTokenExchange();
        Assert.Equal(OAuthFlowState.ExchangingToken, session.State);

        session.CompleteConnected();
        Assert.Equal(OAuthFlowState.Connected, session.State);
        Assert.False(session.IsBusy);

        session.Fail("network_error");
        Assert.Equal(OAuthFlowState.Error, session.State);
        Assert.Equal("network_error", session.ErrorCode);

        session.Disconnect();
        Assert.Equal(OAuthFlowState.Disconnected, session.State);
        Assert.Null(session.ErrorCode);
    }
}
