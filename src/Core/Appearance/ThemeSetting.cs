namespace Koy.Core.Appearance;

/// <summary>Çözümlenmiş tema: ayar "Sistem" olsa bile ekrana bunlardan biri uygulanır.</summary>
public enum ResolvedTheme
{
    Dark,
    Light
}

/// <summary>
/// Theme ayarının (<see cref="AppTheme"/>: Sistem / Koyu / Açık) çözümlenmesi. Eski sürümlerin yazdığı karışık değerler
/// ("Koyu", "sistem"…) ayar göçünde enum adına çevrilir.
/// </summary>
public static class ThemeSetting
{
    /// <param name="value">Ayardaki tema.</param>
    /// <param name="systemUsesLightTheme">Windows uygulama teması açık mı (yalnız "Sistem" seçiliyken kullanılır).</param>
    public static ResolvedTheme Resolve(AppTheme value, bool systemUsesLightTheme) =>
        value switch
        {
            AppTheme.System => systemUsesLightTheme ? ResolvedTheme.Light : ResolvedTheme.Dark,
            AppTheme.Light => ResolvedTheme.Light,
            _ => ResolvedTheme.Dark
        };
}
