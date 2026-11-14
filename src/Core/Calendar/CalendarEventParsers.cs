using System.Globalization;
using System.Text.Json;

namespace Koy.Core.Calendar;

/// <summary>
/// Google Calendar API v3 ve Microsoft Graph yanıtlarını <see cref="CalendarEvent"/> listesine dönüştüren saf ayrıştırıcı.
/// </summary>
public static class CalendarEventParsers
{
    public static OAuthTokenSet? ParseTokenResponse(string json, DateTimeOffset nowUtc, string existingRefreshToken = "", string accountLabel = "")
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("access_token", out var accessProp))
            return null;

        var accessToken = accessProp.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var refreshToken = root.TryGetProperty("refresh_token", out var refreshProp)
            ? refreshProp.GetString() ?? existingRefreshToken
            : existingRefreshToken;

        var expiresIn = root.TryGetProperty("expires_in", out var expProp) && expProp.TryGetInt32(out var secs)
            ? Math.Max(60, secs)
            : 3600;

        return new OAuthTokenSet(accessToken, refreshToken ?? "", nowUtc.AddSeconds(expiresIn), accountLabel);
    }

    public static IReadOnlyList<CalendarEvent> ParseGoogleEvents(string json, TimeZoneInfo? localZone = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        localZone ??= TimeZoneInfo.Local;
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<CalendarEvent>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("status", out var status) &&
                string.Equals(status.GetString(), "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var summary = item.TryGetProperty("summary", out var sumProp) ? sumProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(summary))
                continue;

            if (!item.TryGetProperty("start", out var startObj))
                continue;

            if (!TryParseGoogleDateTime(startObj, localZone, out var startLocal, out var isAllDay))
                continue;

            var durationMinutes = 0;
            if (!isAllDay && item.TryGetProperty("end", out var endObj) &&
                TryParseGoogleDateTime(endObj, localZone, out var endLocal, out _))
            {
                durationMinutes = Math.Clamp((int)Math.Round((endLocal - startLocal).TotalMinutes), 0, 24 * 60);
            }

            list.Add(new CalendarEvent($"google:{id}", summary.Trim(), startLocal, durationMinutes, CalendarEventSource.Google));
        }

        return list;
    }

    public static IReadOnlyList<CalendarEvent> ParseOutlookEvents(string json, TimeZoneInfo? localZone = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        localZone ??= TimeZoneInfo.Local;
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("value", out var items) || items.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<CalendarEvent>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("isCancelled", out var cancelled) && cancelled.ValueKind == JsonValueKind.True)
                continue;

            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var subject = item.TryGetProperty("subject", out var subProp) ? subProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(subject))
                continue;

            var isAllDay = item.TryGetProperty("isAllDay", out var allDayProp) && allDayProp.ValueKind == JsonValueKind.True;
            if (!item.TryGetProperty("start", out var startObj) ||
                !TryParseGraphDateTime(startObj, localZone, isAllDay, out var startLocal))
            {
                continue;
            }

            var durationMinutes = 0;
            if (!isAllDay && item.TryGetProperty("end", out var endObj) &&
                TryParseGraphDateTime(endObj, localZone, false, out var endLocal))
            {
                durationMinutes = Math.Clamp((int)Math.Round((endLocal - startLocal).TotalMinutes), 0, 24 * 60);
            }

            list.Add(new CalendarEvent($"outlook:{id}", subject.Trim(), startLocal, durationMinutes, CalendarEventSource.Outlook));
        }

        return list;
    }

    private static bool TryParseGoogleDateTime(JsonElement obj, TimeZoneInfo localZone, out DateTime localTime, out bool isAllDay)
    {
        if (obj.TryGetProperty("dateTime", out var dtProp) &&
            DateTimeOffset.TryParse(dtProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            localTime = TimeZoneInfo.ConvertTime(dto, localZone).DateTime;
            isAllDay = false;
            return true;
        }

        if (obj.TryGetProperty("date", out var dProp) &&
            DateOnly.TryParseExact(dProp.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            localTime = dateOnly.ToDateTime(TimeOnly.MinValue);
            isAllDay = true;
            return true;
        }

        localTime = default;
        isAllDay = false;
        return false;
    }

    private static bool TryParseGraphDateTime(JsonElement obj, TimeZoneInfo localZone, bool isAllDay, out DateTime localTime)
    {
        localTime = default;
        if (!obj.TryGetProperty("dateTime", out var dtProp))
            return false;

        var raw = dtProp.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;

        if (isAllDay)
        {
            localTime = parsed.Date;
            return true;
        }

        var tzName = obj.TryGetProperty("timeZone", out var tzProp) ? tzProp.GetString() : null;
        if (string.Equals(tzName, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            var utc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            localTime = TimeZoneInfo.ConvertTimeFromUtc(utc, localZone);
            return true;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto) &&
            (raw.EndsWith('Z') || raw.Contains('+') || (raw.Length > 10 && raw[10..].Contains('-'))))
        {
            localTime = TimeZoneInfo.ConvertTime(dto, localZone).DateTime;
            return true;
        }

        localTime = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        return true;
    }
}
