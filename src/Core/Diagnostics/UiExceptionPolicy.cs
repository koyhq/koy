using System.Reflection;

namespace Koy.Core.Diagnostics;

/// <summary>
/// Arayüz iş parçacığındaki yakalanmamış hata sürecin kapanmasına mı yol açsın, yoksa Koy açık mı kalsın.
/// Ölümcül sınıf (bellek bitti, bozuk yerel bellek) ve aynı hatanın hızla tekrarı (kısır döngü) kapanır;
/// geri kalanı loglanıp yutulur. StackOverflow zaten yakalanamaz.
/// </summary>
public sealed class UiExceptionPolicy(int maxRepeats = 5, TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromSeconds(10);
    private readonly Dictionary<string, Queue<DateTime>> _recent = [];
    private readonly object _gate = new();

    /// <summary>Süreç bu hatadan sonra güvenle yaşayamaz.</summary>
    public static bool IsFatal(Exception ex)
    {
        for (var current = ex; current is not null; current = Inner(current))
        {
            if (current is OutOfMemoryException or AccessViolationException or InvalidProgramException
                or BadImageFormatException or AppDomainUnloadedException or InsufficientExecutionStackException)
                return true;
        }
        return false;
    }

    private static Exception? Inner(Exception ex) => ex switch
    {
        TargetInvocationException t => t.InnerException,
        _ => ex.InnerException
    };

    /// <summary>true: hatayı yut (Koy açık kalsın); false: kapanmaya izin ver.</summary>
    public bool ShouldHandle(Exception ex, DateTime now)
    {
        if (IsFatal(ex))
            return false;

        var key = ex.GetType().FullName + "|" + ex.Message;
        lock (_gate)
        {
            if (!_recent.TryGetValue(key, out var times))
                _recent[key] = times = new Queue<DateTime>();

            while (times.Count > 0 && now - times.Peek() > _window)
                times.Dequeue();
            times.Enqueue(now);

            // Sözlük sınırsız büyümesin: boşalan anahtarlar atılır.
            if (_recent.Count > 64)
                foreach (var stale in _recent.Where(p => p.Value.Count == 0).Select(p => p.Key).ToList())
                    _recent.Remove(stale);

            return times.Count <= maxRepeats;
        }
    }
}
