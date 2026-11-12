namespace Koy.Core.Extensions.Store;

/// <summary>Eklenti dosyalarının yerleri: <c>%AppData%\Koy\extensions.json</c> ve <c>%AppData%\Koy\extensions\&lt;id&gt;\</c>.</summary>
public static class ExtensionPaths
{
    public static string KoyFolder { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy");

    public static string RegistryPath { get; } = System.IO.Path.Combine(KoyFolder, "extensions.json");

    public static string ExtensionsFolder { get; } = System.IO.Path.Combine(KoyFolder, "extensions");

    /// <summary>Kimliğin kurulum klasörü. Kimlik doğrulanmış olmalı (küçük harf, rakam, tire); aksi halde null.</summary>
    public static string? FolderFor(string? id, string? extensionsFolder = null)
    {
        if (!ExtensionManifestValidator.IsValidId(id))
        {
            return null;
        }

        return System.IO.Path.Combine(extensionsFolder ?? ExtensionsFolder, id!);
    }

    /// <summary>
    /// <paramref name="path"/> tam çözüldükten sonra <paramref name="root"/>'un kesin altında mı (kökün kendisi sayılmaz).
    /// Silmeden önceki son güvenlik denetimi: ".." ya da simge bağlantısıyla köke çıkan yol yanlış klasörü silmesin.
    /// </summary>
    public static bool IsStrictlyUnder(string root, string path)
    {
        try
        {
            var fullRoot = System.IO.Path.GetFullPath(root).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
            var fullPath = System.IO.Path.GetFullPath(path);
            return fullPath.Length > fullRoot.Length && fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or System.IO.PathTooLongException)
        {
            return false;
        }
    }
}
