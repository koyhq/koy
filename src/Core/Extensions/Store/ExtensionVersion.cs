namespace Koy.Core.Extensions.Store;

/// <summary>
/// Üç parçalı sayısal sürüm ("1.2.3"). Biçim kuralı manifest doğrulayıcısıyla aynıdır (parça en çok 6 hane, baştaki
/// sıfır yok); bu yüzden iki yazım aynı sürümü anlatmaz. Karşılaştırma sayısaldır: "1.10.0" > "1.9.0".
/// </summary>
public readonly record struct ExtensionVersion(int Major, int Minor, int Patch) : IComparable<ExtensionVersion>
{
    public static bool TryParse(string? text, out ExtensionVersion version)
    {
        version = default;
        if (!ExtensionManifestValidator.IsValidVersion(text))
        {
            return false;
        }

        var parts = text!.Split('.');
        version = new ExtensionVersion(
            int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
        return true;
    }

    public int CompareTo(ExtensionVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public static bool operator <(ExtensionVersion left, ExtensionVersion right) => left.CompareTo(right) < 0;
    public static bool operator <=(ExtensionVersion left, ExtensionVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >(ExtensionVersion left, ExtensionVersion right) => left.CompareTo(right) > 0;
    public static bool operator >=(ExtensionVersion left, ExtensionVersion right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// İki metni karşılaştırır. Geçersiz sürüm her zaman geçerliden küçük sayılır; ikisi de geçersizse eşittir.
    /// </summary>
    public static int Compare(string? left, string? right)
    {
        var leftOk = TryParse(left, out var l);
        var rightOk = TryParse(right, out var r);
        if (leftOk && rightOk)
        {
            return l.CompareTo(r);
        }

        return leftOk == rightOk ? 0 : (leftOk ? 1 : -1);
    }

    /// <summary>Aday, kurulu sürümden kesin yeni mi. Biri geçersizse false (güncelleme önerilmez).</summary>
    public static bool IsNewer(string? candidate, string? installed) =>
        TryParse(candidate, out var c) && TryParse(installed, out var i) && c.CompareTo(i) > 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
