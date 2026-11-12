using System.Globalization;

namespace Koy.Core.Clock;

/// <summary>İkinci saat satırı: kısa etiket ve biçimlenmiş saat.</summary>
public sealed record SecondClockReading(string Label, string Time)
{
    public string Text => Label.Length == 0 ? Time : Label + " " + Time;
}

/// <summary>
/// İkinci saat dilimi (issue #77). Saf: şimdiki an dışarıdan gelir. Kimlik boş, bilinmeyen ya da bozuksa "ikinci saat yok"
/// (null) döner; hiçbir durumda istisna fırlatmaz.
/// </summary>
public static class SecondClock
{
    /// <summary>Kimliği saat dilimine çözer; boş, bulunamayan ya da bozuk kimlikte null.</summary>
    public static TimeZoneInfo? Resolve(string? timeZoneId)
    {
        if (!ClockFormatPolicy.HasSecondClock(timeZoneId))
            return null;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId!.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>İkinci saat satırı; kimlik çözülemezse null. Etiket boşsa "UTC+3" gibi kısa fark etiketi kullanılır.</summary>
    public static SecondClockReading? Read(string? timeZoneId, DateTimeOffset now, string timePattern, CultureInfo culture, string? label = null) =>
        Resolve(timeZoneId) is { } zone ? Read(zone, now, timePattern, culture, label) : null;

    /// <summary>Verilen saat dilimindeki saat satırı.</summary>
    public static SecondClockReading Read(TimeZoneInfo zone, DateTimeOffset now, string timePattern, CultureInfo culture, string? label = null)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var text = string.IsNullOrWhiteSpace(label) ? OffsetLabel(local.Offset) : label.Trim();
        return new SecondClockReading(text, local.ToString(timePattern, culture));
    }

    /// <summary>"UTC", "UTC+3", "UTC-5", "UTC+5:30" biçiminde UTC farkı.</summary>
    public static string OffsetLabel(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero)
            return "UTC";

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        var hours = (int)abs.TotalHours;
        return abs.Minutes == 0
            ? "UTC" + sign + hours.ToString(CultureInfo.InvariantCulture)
            : "UTC" + sign + hours.ToString(CultureInfo.InvariantCulture) + ":" + abs.Minutes.ToString("00", CultureInfo.InvariantCulture);
    }
}
