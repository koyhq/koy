namespace Koy.Core.Appearance;

/// <summary>
/// Simge paketi klasöründeki dosyalardan bir uygulamanın simgesini seçer. Dosya adı (uzantısız) uygulama anahtarının
/// uzantısız hâliyle eşleşir: "Firefox.lnk" için "Firefox.png". Dosya okumaz; ad listesini çağıran verir.
/// </summary>
public static class IconPackFiles
{
    // Aynı ad birden çok uzantıyla varsa sıra: PNG, ICO, SVG.
    private static readonly string[] ExtensionOrder = [".png", ".ico", ".svg"];
    private const int SvgRank = 2;

    /// <param name="fileNames">Paket klasöründeki dosya adları (yol değil).</param>
    /// <param name="appKey">Uygulama anahtarı (<see cref="AppIconKey"/>).</param>
    /// <param name="allowSvg">Çizici SVG gösteremiyorsa false: SVG dosyaları yok sayılır.</param>
    /// <returns>Seçilen dosya adı; eşleşme yoksa ya da ad geçersizse null.</returns>
    public static string? Find(IEnumerable<string> fileNames, string? appKey, bool allowSvg)
    {
        if (!AppIconOverrides.IsValidKey(appKey)) return null;

        var stem = StemOf(appKey!.Trim());
        if (stem.Length == 0) return null;

        string? best = null;
        var bestRank = int.MaxValue;
        foreach (var name in fileNames)
        {
            if (!AppIconOverrides.TryValidatePackFile(name, out var valid)) continue;

            var rank = Array.FindIndex(ExtensionOrder, e => string.Equals(e, Path.GetExtension(valid), StringComparison.OrdinalIgnoreCase));
            if (rank < 0 || (!allowSvg && rank == SvgRank)) continue;
            if (!string.Equals(Path.GetFileNameWithoutExtension(valid), stem, StringComparison.OrdinalIgnoreCase)) continue;

            // Eşit sırada ada göre sabit seçim: klasör sırası sonucu değiştirmesin.
            if (rank < bestRank || (rank == bestRank && string.CompareOrdinal(valid, best) < 0))
            {
                best = valid;
                bestRank = rank;
            }
        }

        return best;
    }

    // AUMID'de uzantı yoktur ("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"); yalnız bilinen kısayol/exe uzantısı atılır.
    private static string StemOf(string key)
    {
        var ext = Path.GetExtension(key);
        return ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".url", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(key)
            : key;
    }
}
