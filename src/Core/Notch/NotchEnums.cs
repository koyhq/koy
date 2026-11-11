namespace Koy.Core.Notch;

// Bu dosyadaki enum adları settings.json'a yazılır: bir kez yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Çentik boştayken (kapalı, müzik çalmıyor) ne gösterilir. Eski dosyalarda sayıydı (0–3), göçte ada çevrilir.</summary>
public enum IdleDisplayMode
{
    /// <summary>Saat ve pil (varsayılan).</summary>
    ClockAndBattery,

    /// <summary>Yalnız saat.</summary>
    ClockOnly,

    /// <summary>Hiçbiri.</summary>
    Nothing,

    /// <summary>Solda dairesel pil halkası ve yüzde, sağda kısa gün adı ve saat ("Sal 19:21").</summary>
    RingClockDay
}

/// <summary>Çentiğin açılması: fareyle üzerine gelince (varsayılan) ya da tıklayınca.</summary>
public enum NotchOpenBehavior
{
    Hover,
    Click
}

/// <summary>Çentik üstünde fare tekerleği: panel değiştir (varsayılan) ya da sistem sesini ayarla.</summary>
public enum WheelAction
{
    SwitchPanel,
    Volume
}

/// <summary>Çentik biçimi: ekrana yapışık (kulaklı) ya da yüzen ada (kulaksız kapsül, üstten boşluklu).</summary>
public enum NotchStyleMode
{
    Pinned,
    Floating
}
