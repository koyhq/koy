namespace Koy.Core.Dock;

/// <summary>
/// Görev çubuğuna sabitlenmiş bir kısayolun saf modeli.
/// </summary>
public sealed record PinnedApp(
    string Title,
    string ShortcutPath,
    string? TargetPath,
    byte[]? IconData,
    string? AppUserModelId = null);

/// <summary>
/// Kullanıcının görev çubuğuna sabitlediği uygulamaları okuma sözleşmesi.
/// </summary>
public interface IPinnedAppsService
{
    IReadOnlyList<PinnedApp> GetPinnedApps();
}
