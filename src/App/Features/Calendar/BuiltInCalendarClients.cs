using System.Reflection;
using Koy.Core.Calendar;

namespace Koy.App.Features.Calendar;

/// <summary>
/// Sürüme gömülü OAuth istemcileri. Değerler derlemede <c>KOY_BUILD_GOOGLE_CLIENT_ID</c>,
/// <c>KOY_BUILD_GOOGLE_CLIENT_SECRET</c> ve <c>KOY_BUILD_OUTLOOK_CLIENT_ID</c> ortam değişkenlerinden
/// <c>AssemblyMetadata</c> olarak yazılır (Koy.App.csproj); yerel derlemede boştur.
/// </summary>
public static class BuiltInCalendarClients
{
    public static OAuthBuiltInClients Current { get; } = Read();

    /// <summary>Ayarlardaki kimlik, ortam değişkeni ya da yerleşik istemciden kullanılacak olanı seçer.</summary>
    public static OAuthClient Resolve(CalendarProvider provider, CalendarSettings settings) =>
        CalendarOAuthConfig.ResolveClient(provider, settings, Current);

    private static OAuthBuiltInClients Read()
    {
        var metadata = typeof(BuiltInCalendarClients).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key.StartsWith("KoyOAuth", StringComparison.Ordinal))
            .ToDictionary(a => a.Key, a => a.Value ?? "", StringComparer.Ordinal);

        string Get(string key) => metadata.TryGetValue(key, out var value) ? value.Trim() : "";

        return new OAuthBuiltInClients(
            Get("KoyOAuthGoogleClientId"),
            Get("KoyOAuthGoogleClientSecret"),
            Get("KoyOAuthOutlookClientId"));
    }
}
