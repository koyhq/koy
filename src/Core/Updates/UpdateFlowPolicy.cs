namespace Koy.Core.Updates;

/// <summary>
/// Güncelleme akışı, sürüm karşılaştırması ve "Neler Geldi" gösterim kararları.
/// Saf C#, Win32 ve UI bağımlılığı yoktur.
/// </summary>
public static class UpdateFlowPolicy
{
    /// <summary>
    /// İki sürüm dizesini semantik olarak karşılaştırır.
    /// versionA > versionB ise pozitif, eşitse 0, küçükse negatif döner.
    /// </summary>
    public static int CompareVersions(string? versionA, string? versionB)
    {
        var va = ParseVersion(versionA);
        var vb = ParseVersion(versionB);

        if (va is null && vb is null) return 0;
        if (va is null) return -1;
        if (vb is null) return 1;

        var core = va.CompareTo(vb);
        if (core != 0)
            return core;

        // SemVer: çekirdek eşitse ön sürüm (0.3.0-rc.2) son sürümden (0.3.0) küçüktür.
        return ComparePrerelease(Prerelease(versionA), Prerelease(versionB));
    }

    private static string? Prerelease(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return null;

        var s = v.Trim();
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        var dash = s.IndexOf('-');
        return dash >= 0 && dash < s.Length - 1 ? s[(dash + 1)..] : null;
    }

    private static int ComparePrerelease(string? a, string? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return 1;
        if (b is null) return -1;

        var pa = a.Split('.');
        var pb = b.Split('.');
        for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            var na = int.TryParse(pa[i], out var ia);
            var nb = int.TryParse(pb[i], out var ib);
            var c = (na, nb) switch
            {
                (true, true) => ia.CompareTo(ib),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(pa[i], pb[i])
            };
            if (c != 0)
                return Math.Sign(c);
        }

        return pa.Length.CompareTo(pb.Length);
    }

    /// <summary>
    /// Mevcut sürüme göre yeni bir sürümün daha ileri olup olmadığını denetler.
    /// </summary>
    public static bool IsNewerVersion(string currentVersion, string availableVersion)
        => CompareVersions(availableVersion, currentVersion) > 0;

    /// <summary>
    /// Yeni sürüm sonrası "Neler Geldi" (What's New) ekranı gösterilmeli mi?
    /// İlk kurulumda (firstRunCompleted = false) kurulum sihirbazı çalıştığı için gösterilmez.
    /// Daha önce sürüm kaydı yoksa (eski sürümden geçiş) veya mevcut sürüm kayıtlı sürümden büyükse gösterilir.
    /// </summary>
    public static bool ShouldShowWhatsNew(string currentVersion, string? lastSeenVersion, bool firstRunCompleted = true)
    {
        if (!firstRunCompleted)
            return false;

        if (string.IsNullOrWhiteSpace(lastSeenVersion))
            return true;

        return CompareVersions(currentVersion, lastSeenVersion) > 0;
    }

    /// <summary>
    /// Sürüm numarasını "0.x.y" veya "v0.x.y" formatına temizler.
    /// </summary>
    public static string FormatDisplayVersion(string? version, bool prefixWithV = false)
    {
        if (string.IsNullOrWhiteSpace(version))
            return string.Empty;

        var s = version.Trim().TrimStart('v', 'V');
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];

        return prefixWithV ? "v" + s : s;
    }

    private static Version? ParseVersion(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return null;

        var s = v.Trim().TrimStart('v', 'V');
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        var dash = s.IndexOf('-');
        if (dash >= 0) s = s[..dash];

        if (Version.TryParse(s, out var parsed))
            return parsed;

        var parts = s.Split('.');
        if (parts.Length == 2 && int.TryParse(parts[0], out var maj) && int.TryParse(parts[1], out var min))
            return new Version(maj, min, 0);

        return null;
    }
}
