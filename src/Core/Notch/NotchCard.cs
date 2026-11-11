namespace Koy.Core.Notch;

/// <summary>Çentik kartının içerik türü. Kart yüzeyi türden bağımsızdır; yeni tür eklemek yeni kart modeli gerektirmez.</summary>
public enum NotchCardKind
{
    Update,
    Notification
}

/// <summary>
/// Çentikte HUD'dan (260×40) büyük, düğmeli kartın içeriği. Güncelleme kartı ve (sonraki pakette) Windows bildirimleri
/// aynı kartı kullanır. Metinler çağıran tarafından çevrilmiş olarak verilir (Core resx'e erişemez).
/// </summary>
/// <param name="PrimaryActionLabel">Birincil düğmenin yazısı; boşsa düğme gösterilmez.</param>
/// <param name="AppName">Bildirimi gönderen uygulama; güncellemede boş.</param>
/// <param name="Icon">Bildirim için uygulama simgesi (kodlanmış resim); güncellemede null.</param>
public sealed record NotchCard(
    NotchCardKind Kind,
    string Title,
    string Subtitle,
    string PrimaryActionLabel = "",
    string AppName = "",
    DateTimeOffset? Timestamp = null,
    byte[]? Icon = null)
{
    public bool HasPrimaryAction => !string.IsNullOrEmpty(PrimaryActionLabel);
}
