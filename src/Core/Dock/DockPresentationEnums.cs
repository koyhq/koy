namespace Koy.Core.Dock;

// Bu dosyadaki enum adları settings.json'a yazılır: yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Gösterge ya da rozet rengi: Windows vurgu rengi, nötr gri ya da kullanıcının seçtiği özel renk.</summary>
public enum DockColorSource
{
    Accent,
    Neutral,
    Custom
}

/// <summary>Rozetin simgenin hangi köşesinde durduğu.</summary>
public enum DockBadgeCorner
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft
}

/// <summary>Rozet biçimi: sayı (1–9, 9'dan büyükse 9+) ya da yalnız nokta.</summary>
public enum DockBadgeStyle
{
    Number,
    DotOnly
}

/// <summary>Dock'un gizliyken yeniden çıkma tetikleyicisi.</summary>
public enum DockRevealTrigger
{
    /// <summary>Fare alt kenara değer değmez (gecikme yok).</summary>
    EdgeTouch,

    /// <summary>Fare alt kenarda <c>RevealDelayMs</c> kadar basılı tutulunca.</summary>
    EdgeHold,

    /// <summary>Yalnız kısayol; kenar hareketi dock'u açmaz.</summary>
    ShortcutOnly
}
