using Koy.Core.Notch;

namespace Koy.Core.Notifications;

/// <summary>Göreli zaman metinleri; Core resx'e erişemez, App çevrilmiş halini verir. Biçimler <c>{0}</c> sayısını alır.</summary>
public sealed record RelativeTimeLabels(string Now, string MinutesAgoFormat, string HoursAgoFormat);

/// <summary>
/// Bildirimi çentik kartına çevirir: üst satır "UYGULAMA · şimdi", başlık, gövde. Uzun metinler kısaltılır
/// (kart 380×72 sabit; tek satır, ucu "…"). Gizlilik ayarı açıkken gövde hiç karta girmez.
/// </summary>
public static class NotificationPresentation
{
    public const int MaxTitleLength = 60;
    public const int MaxBodyLength = 90;
    public const int MaxAppNameLength = 30;

    public static NotchCard ToCard(NotificationItem item, DateTimeOffset now, bool hideBody, RelativeTimeLabels labels)
    {
        var app = Truncate(item.AppName, MaxAppNameLength);
        var title = Truncate(item.Title, MaxTitleLength);
        var body = hideBody ? "" : Truncate(item.Body, MaxBodyLength);

        // Başlık boşsa (yalnız gövdeli bildirim) gövde başlığın yerine geçer; satır boş kalmasın.
        if (title.Length == 0 && body.Length > 0)
            (title, body) = (body, "");

        var header = app.Length == 0
            ? FormatAge(item.Time, now, labels)
            : $"{app} · {FormatAge(item.Time, now, labels)}";

        return new NotchCard(
            NotchCardKind.Notification,
            title,
            body,
            AppName: app,
            Timestamp: item.Time,
            Icon: item.Icon,
            FallbackIcon: item.FallbackIcon,
            Header: header);
    }

    /// <summary>"şimdi" (1 dk altı), "{n} dk önce" (1 saat altı), "{n} sa önce".</summary>
    public static string FormatAge(DateTimeOffset time, DateTimeOffset now, RelativeTimeLabels labels)
    {
        var age = now - time;
        if (age < TimeSpan.FromMinutes(1))
            return labels.Now;

        if (age < TimeSpan.FromHours(1))
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, labels.MinutesAgoFormat, (int)age.TotalMinutes);

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, labels.HoursAgoFormat, (int)age.TotalHours);
    }

    /// <summary>Satır sonlarını ve art arda boşlukları tek boşluğa indirir, uzunsa sondan "…" ile keser.</summary>
    public static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var sb = new System.Text.StringBuilder(Math.Min(text.Length, maxLength + 1));
        var lastWasSpace = false;
        foreach (var ch in text.Trim())
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                if (!lastWasSpace)
                    sb.Append(' ');
                lastWasSpace = true;
                continue;
            }

            lastWasSpace = false;
            sb.Append(ch);
        }

        var normalized = sb.ToString();
        if (normalized.Length <= maxLength)
            return normalized;

        // Vekil çiftin ortasından kesme.
        var cut = maxLength - 1;
        if (cut > 0 && char.IsHighSurrogate(normalized[cut - 1]))
            cut--;
        return normalized[..cut].TrimEnd() + "…";
    }
}
