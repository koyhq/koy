namespace Koy.UiSnapshots;

/// <summary>
/// Örnek verinin dili: --lang en ile çizilen görüntülerde arayüzle birlikte örnek veriler de İngilizce olur
/// (README.md ve site İngilizce görüntüleri kullanır; karışık dil olmasın).
/// </summary>
internal static class Sample
{
    public static bool English =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    public static string T(string tr, string en) => English ? en : tr;
}
