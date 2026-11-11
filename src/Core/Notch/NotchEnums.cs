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

/// <summary>Çentiğin açılması: fareyle üzerine gelince (varsayılan), tıklayınca ya da sürüklenen öğe üstüne gelince.</summary>
public enum NotchOpenBehavior
{
    Hover,
    Click,
    DragOver
}

/// <summary>Sekme çubuğunun görünümü: yalnız simge, simge ve ad, ya da hiç.</summary>
public enum NotchTabBarStyle
{
    IconOnly,
    IconAndLabel,
    Hidden
}

/// <summary>Müzik görselleştiricisinin çizim biçimi.</summary>
public enum NotchVisualizerStyle
{
    Bars,
    Wave,
    Dots
}

/// <summary>Görselleştirici rengi: Windows vurgu rengi ya da albüm kapağından çıkarılan renk.</summary>
public enum NotchVisualizerColorSource
{
    Accent,
    Artwork
}

/// <summary>Ana sayfadaki kartlar. Sıra ve görünürlük <see cref="Settings.NotchSettings.HomeCards"/> listesindedir.</summary>
public enum HomeCard
{
    Music,
    HeadphoneBattery,
    SystemNetwork,
    QuickLaunch,
    Weather,
    Timers
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
