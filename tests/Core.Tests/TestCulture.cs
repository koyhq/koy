using System.Globalization;
using System.Runtime.CompilerServices;

namespace Koy.Core.Tests;

/// <summary>
/// KOY_TEST_CULTURE ortam değişkeni doluysa testleri o kültürle (ör. tr-TR, de-DE) çalıştırır.
/// Böylece kültüre bağlı hatalar (ondalık ayracı, büyük/küçük harf, tarih biçimi) CI'da yakalanır.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
        Justification = "Test derlemesi; kültür derleme yüklenir yüklenmez, her testten önce ayarlanmalı.")]
    internal static void Apply()
    {
        var name = Environment.GetEnvironmentVariable("KOY_TEST_CULTURE");
        if (string.IsNullOrWhiteSpace(name))
            return;

        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
