namespace Koy.Core.Extensions.Store;

/// <summary>
/// Mağazadaki eklenti türü. Küme kapalıdır ve hepsi veri paketidir: kod çalıştıran tür yoktur (AGENTS #4).
/// JSON'daki adlar küçük harf: theme, iconpack, background, profile.
/// </summary>
public enum ExtensionKind
{
    Theme,
    IconPack,
    Background,
    Profile,
}

public static class ExtensionKinds
{
    public static IReadOnlyList<ExtensionKind> All { get; } =
        [ExtensionKind.Theme, ExtensionKind.IconPack, ExtensionKind.Background, ExtensionKind.Profile];

    /// <summary>Birebir eşleşme (büyük/küçük harfe duyarlı): "Theme" ya da "theme " kabul edilmez.</summary>
    public static bool TryParse(string? value, out ExtensionKind kind)
    {
        switch (value)
        {
            case "theme":
                kind = ExtensionKind.Theme;
                return true;
            case "iconpack":
                kind = ExtensionKind.IconPack;
                return true;
            case "background":
                kind = ExtensionKind.Background;
                return true;
            case "profile":
                kind = ExtensionKind.Profile;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    public static string ToJsonName(this ExtensionKind kind) => kind switch
    {
        ExtensionKind.Theme => "theme",
        ExtensionKind.IconPack => "iconpack",
        ExtensionKind.Background => "background",
        ExtensionKind.Profile => "profile",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
