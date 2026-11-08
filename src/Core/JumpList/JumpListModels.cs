namespace Koy.Core.JumpList;

/// <summary>Atlama listesi bölümü türü. Sıra, menüdeki sıradır değil; menü sırası <see cref="JumpListLayout"/> içindedir.</summary>
public enum JumpListSectionKind
{
    /// <summary>Uygulamanın kendi adlandırdığı kategori (ör. VS Code'un son klasörleri).</summary>
    Custom,
    Frequent,
    Recent,
    Tasks,
}

/// <summary>
/// Bir girdi tıklanınca çalıştırılacak şey; yalnız dizelerden oluşur (WinRT / Win32 tipi yok), bu yüzden önbelleğe
/// alınabilir ve COM nesnesi taşımaz.
/// </summary>
/// <param name="Target">Çalıştırılacak yol, URL ya da kabuk ayrıştırma adı.</param>
/// <param name="Arguments">Görev kısayolunun komut satırı (belge girdilerinde boş).</param>
/// <param name="WorkingDirectory">Görev kısayolunun çalışma klasörü.</param>
/// <param name="IsDocument">Hedef bir belge / klasör / URL (son ve sık kullanılanlar); görev kısayolu değil.</param>
public sealed record JumpListLaunch(
    string Target,
    string? Arguments = null,
    string? WorkingDirectory = null,
    bool IsDocument = false);

/// <summary>Menüde bir satır: başlık ve çalıştırma bilgisi. <see cref="Launch"/> boşsa satır bir ayırıcıdır.</summary>
public sealed record JumpListEntry(string Title, JumpListLaunch? Launch)
{
    public static JumpListEntry Separator { get; } = new(string.Empty, null);

    public bool IsSeparator => Launch is null;
}

/// <summary>Menüde başlıklı bir bölüm. <see cref="Name"/> yalnız <see cref="JumpListSectionKind.Custom"/> için doludur.</summary>
public sealed record JumpListSection(JumpListSectionKind Kind, string? Name, IReadOnlyList<JumpListEntry> Entries);
