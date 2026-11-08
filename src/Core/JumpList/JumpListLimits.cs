namespace Koy.Core.JumpList;

/// <summary>
/// Atlama listesi verisi güvenilmeyen ikili veridir (her uygulama kendi dosyasını yazar). Okuyucu ve menü bu üst
/// sınırların dışına çıkmaz; aşan girdi bozuk sayılır.
/// </summary>
public static class JumpListLimits
{
    /// <summary><c>customDestinations-ms</c> dosyası için en büyük boyut. Gerçek dosyalar onlarca KB'dir.</summary>
    public const int MaxFileBytes = 2 * 1024 * 1024;

    public const int MaxCategories = 16;
    public const int MaxItemsPerCategory = 64;
    public const int MaxTotalItems = 256;

    /// <summary>Tek bir ShellLink akışı için en büyük boyut.</summary>
    public const int MaxItemBytes = 64 * 1024;

    /// <summary>Kategori adı için en çok UTF-16 karakter (bunu aşan dosya bozuk sayılır).</summary>
    public const int MaxCategoryNameChars = 128;

    /// <summary>Menüde görünen başlık en çok bu kadar karakterdir; fazlası "…" ile kesilir.</summary>
    public const int MaxTitleChars = 120;

    public const int MaxTargetChars = 4096;
    public const int MaxArgumentsChars = 8192;

    // Menü ekranı taşmasın: bölüm başına ve toplam sınır (ayırıcılar sayılmaz).
    public const int MaxCustomSections = 3;
    public const int MaxPerCustomSection = 6;
    public const int MaxFrequent = 3;
    public const int MaxRecent = 5;
    public const int MaxTasks = 6;

    /// <summary>Bütün bölümlerdeki toplam girdi sınırı; aşılırsa önce sık, sonra son kullanılanlar kırpılır.</summary>
    public const int MaxTotalEntries = 14;

    /// <summary>Toplam sınır yüzünden kırpılırken bir bölümde en az bu kadar girdi kalır.</summary>
    public const int MinPerSectionWhenTrimming = 2;
}
