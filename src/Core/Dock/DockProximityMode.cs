namespace Koy.Core.Dock;

/// <summary>
/// Pencere dock alanına yaklaştığında uygulanacak davranış modu (M6.3 / DESIGN.md).
/// </summary>
public enum DockProximityMode
{
    /// <summary>
    /// Aşağı yapış: Normal bir pencere dock alanına yaklaştığında dock ekranın en altına iner,
    /// yüksekliği 60 px'den 46 px'e düşer ve yalnızca üst köşeleri yuvarlatılır (14,14,0,0).
    /// Pencere uzaklaşınca eski yüzen haline (60 px, alttan 10 px, 18 px yarıçap) geri döner.
    /// </summary>
    DockDown,

    /// <summary>
    /// Yer ayır (Varsayılan): Dock çalışma alanından (AppBar / SHAppBarMessage) 46 px ayırır.
    /// Büyütülmüş pencereler tam olarak dock'un üstünde biter, arada boşluk kalmaz.
    /// </summary>
    ReserveSpace,

    /// <summary>
    /// Çekil: Büyütülmüş veya çakışan bir pencere dock alanını kapladığında dock ekran dışına kayar
    /// ve altta ince bir tutamak (120×4 px) bırakır. Fare tutamağa gelince (150 ms) dock geri çıkar.
    /// </summary>
    Dodge,

    /// <summary>
    /// Hep gizli: Fare ekranın alt kenarına inene kadar dock tamamen gizli kalır.
    /// </summary>
    AlwaysHidden
}

/// <summary>
/// Dock'un görsel yerleşim stili (Yüzen Kapsül veya Tam Genişlik).
/// </summary>
public enum DockStyleMode
{
    /// <summary>
    /// Yüzen kapsül (varsayılan): 60 px yükseklik, alttan 10 px boşluk, 18 px köşe yuvarlama.
    /// </summary>
    FloatingCapsule,

    /// <summary>
    /// Tam Genişlik Modu: Ekranın en altına yapışık 48 px (DockPositionResolver.FullWidthHeightDip) yükseklik. Solda müzik, ortada uygulamalar, sağda Koy butonu.
    /// </summary>
    FullWidth
}
