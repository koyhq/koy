using Koy.Core.Dictation;

namespace Koy.Platform.Dictation;

/// <summary>
/// Başarısız yazıların sesi (yeniden deneme için). Yalnız bu klasördeki <c>.wav</c> dosyaları okunur ve silinir:
/// geçmiş satırı elle başka bir yolu gösterecek şekilde değiştirilse bile o dosyaya dokunulmaz.
/// </summary>
public sealed class RecordingStore(string? directory = null) : IRecordingStore
{
    private readonly string _directory = Path.GetFullPath(directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Koy", "dictation", "recordings"));

    public string Save(byte[] wav)
    {
        ArgumentNullException.ThrowIfNull(wav);
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.wav");
        File.WriteAllBytes(path, wav);
        return path;
    }

    public byte[]? Load(string path)
    {
        var safe = Owned(path);
        if (safe is null || !File.Exists(safe))
            return null;

        try
        {
            return File.ReadAllBytes(safe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Delete(string path)
    {
        var safe = Owned(path);
        if (safe is null)
            return;

        try
        {
            File.Delete(safe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Silinemeyen ses bir sonraki temizlikte gider; yazının kendisi bozulmaz.
        }
    }

    private string? Owned(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            return null;

        var full = Path.GetFullPath(path);
        var root = _directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
