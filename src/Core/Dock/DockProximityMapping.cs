namespace Koy.Core.Dock;

/// <summary>
/// Yakınlık modunun dock stiline göre geçerli karşılığı. Windows görev çubuğunun iki hâli vardır (hep görünür /
/// otomatik gizle); tam genişlikte yalnız bunlar sunulur. "Aşağı yapış", "Çekil" ve "Hep havada" yüzen kapsüle özgüdür.
/// Ayar dosyasındaki değer değişmez; eşleme çalışma anında uygulanır, kapsüle dönünce eski seçim geçerli olur.
/// </summary>
public static class DockProximityMapping
{
    /// <summary>Verilen stilde bu mod seçilebilir mi (ayarlar sayfası yalnız bunları gösterir).</summary>
    public static bool IsAvailable(DockStyleMode style, DockProximityMode mode) =>
        style != DockStyleMode.FullWidth || mode is DockProximityMode.ReserveSpace or DockProximityMode.AlwaysHidden;

    /// <summary>
    /// Stile göre uygulanacak mod. Tam genişlikte: Aşağı yapış → Yer ayır (çubuk hep görünür, yer ayrılır);
    /// Çekil → Hep gizli (otomatik gizle: fare alt kenara gelince çıkar);
    /// Hep havada → Yer ayır (havada yüzmek tam genişlikte anlamsız, çubuk hep görünür). Kapsülde değişmez.
    /// </summary>
    public static DockProximityMode Effective(DockStyleMode style, DockProximityMode mode)
    {
        if (style != DockStyleMode.FullWidth)
            return mode;

        return mode switch
        {
            DockProximityMode.DockDown => DockProximityMode.ReserveSpace,
            DockProximityMode.Dodge => DockProximityMode.AlwaysHidden,
            DockProximityMode.AlwaysFloating => DockProximityMode.ReserveSpace,
            _ => mode
        };
    }
}
