using System.Globalization;

namespace Koy.Core.Settings;

/// <summary>Uygulama dilinin kodları ve ilk açılıştaki dil kararı.</summary>
public static class AppLanguage
{
    public const string English = "en";
    public const string Turkish = "tr";

    /// <summary>Windows'un görüntü dili. Koy kendi kültürünü kurmadan önce okunur; sonrası kullanıcının ayarını yansıtırdı.</summary>
    public static string SystemDefault { get; } = FromCulture(CultureInfo.CurrentUICulture);

    /// <summary>Türkçe sistemde Türkçe, diğer her dilde İngilizce.</summary>
    public static string FromCulture(CultureInfo culture) => IsTurkish(culture) ? Turkish : English;

    public static bool IsTurkish(CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, Turkish, StringComparison.OrdinalIgnoreCase);
}
