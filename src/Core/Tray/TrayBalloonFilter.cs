namespace Koy.Core.Tray;

/// <summary>
/// Tepsi balon bildirimlerini süzer: boş metinleri (balon kapatma çağrıları dahil) ve kısa aralıkla
/// yinelenen aynı balonları eler. Saf mantık, test edilir.
/// </summary>
public sealed class TrayBalloonFilter(TimeProvider? clock = null)
{
    public static readonly TimeSpan DuplicateCooldown = TimeSpan.FromSeconds(5);
    public const int MaxTrackedKeys = 64;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Dictionary<TrayIconKey, (string Title, string Body, DateTimeOffset SeenAt)> _recent = [];

    /// <summary>
    /// Mesaj gösterilebilir bir balon bildirimi taşıyorsa <see langword="true"/> döner.
    /// </summary>
    public bool TryAccept(TrayNotifyData data, out string title, out string body, out DateTimeOffset timestamp)
    {
        title = "";
        body = "";
        timestamp = default;

        if (data.Message is not (TrayMessage.Add or TrayMessage.Modify))
            return false;

        if ((data.Fields & TrayIconFields.Info) == 0)
            return false;

        title = data.InfoTitle.Trim();
        body = data.Info.Trim();
        if (title.Length == 0 && body.Length == 0)
            return false;

        var now = _clock.GetUtcNow();
        var key = TrayIconKey.From(data);

        if (_recent.TryGetValue(key, out var prev)
            && now - prev.SeenAt < DuplicateCooldown
            && string.Equals(prev.Title, title, StringComparison.Ordinal)
            && string.Equals(prev.Body, body, StringComparison.Ordinal))
        {
            return false;
        }

        PruneExpired(now);
        if (!_recent.ContainsKey(key) && _recent.Count >= MaxTrackedKeys)
            RemoveOldest();

        _recent[key] = (title, body, now);
        timestamp = now;
        return true;
    }

    public void Clear() => _recent.Clear();

    private void PruneExpired(DateTimeOffset now)
    {
        List<TrayIconKey>? expired = null;
        foreach (var (key, entry) in _recent)
        {
            if (now - entry.SeenAt >= DuplicateCooldown)
                (expired ??= []).Add(key);
        }

        if (expired is null)
            return;

        foreach (var key in expired)
            _recent.Remove(key);
    }

    private void RemoveOldest()
    {
        TrayIconKey oldestKey = default;
        var oldestTime = DateTimeOffset.MaxValue;
        var found = false;

        foreach (var (key, entry) in _recent)
        {
            if (!found || entry.SeenAt < oldestTime)
            {
                oldestKey = key;
                oldestTime = entry.SeenAt;
                found = true;
            }
        }

        if (found)
            _recent.Remove(oldestKey);
    }
}
