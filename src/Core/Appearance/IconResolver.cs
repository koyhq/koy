namespace Koy.Core.Appearance;

/// <summary>Simgeyi hangi kaynağın verdiği.</summary>
public enum IconSourceKind
{
    Default,
    Pack,
    AppOverride
}

/// <summary>Çözümlenen simge: kaynak türü ve (yerleşik simgede null) doğrulanmış dosya yolu ya da adı.</summary>
public readonly record struct IconChoice(IconSourceKind Kind, string? Value);

/// <summary>
/// Hangi simgenin kazandığı: önce uygulamanın kendi değişimi, sonra simge paketi, en son yerleşik simge.
/// Dosya okumaz; paketin o anahtar için sunduğu dosya çağıran tarafından verilir.
/// </summary>
public static class IconResolver
{
    /// <param name="appKey">Kısayol adı (dock öğesi); null ise uygulama değişimi denenmez.</param>
    /// <param name="appOverrides">Uygulama başına değişimler (<see cref="AppIconOverrides"/>).</param>
    /// <param name="pack">Seçili simge paketi.</param>
    /// <param name="packIcon">Paketin bu anahtar için sunduğu dosya adı (yalnız ad, yol değil); paket yoksa ya da dosya yoksa null.</param>
    public static IconChoice Resolve(
        string? appKey,
        IReadOnlyDictionary<string, string>? appOverrides,
        IconPack pack,
        string? packIcon)
    {
        var overridden = AppIconOverrides.Lookup(appOverrides, appKey);
        if (overridden is not null) return new IconChoice(IconSourceKind.AppOverride, overridden);

        if (pack == IconPack.Folder && AppIconOverrides.TryValidatePackFile(packIcon, out var packFile))
            return new IconChoice(IconSourceKind.Pack, packFile);

        return new IconChoice(IconSourceKind.Default, null);
    }
}
