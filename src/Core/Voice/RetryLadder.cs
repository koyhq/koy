namespace Koy.Core.Voice;

/// <summary>
/// Konuşma bir daha söylenemez; geçici hatada (özellikle hız sınırı) yazıya çevirme isteği bekleyerek yeniden denenir.
/// Bekleme her başarısız denemeden sonra uzar, merdiven biterse iş başarısız sayılır ve ses dosyası saklanır.
/// </summary>
public static class RetryLadder
{
    private static readonly TimeSpan[] Waits =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(120),
    ];

    /// <summary>İlk deneme dahil en çok deneme sayısı.</summary>
    public static int MaxAttempts => Waits.Length + 1;

    /// <summary>
    /// <paramref name="failedAttempts"/> deneme başarısız olduktan sonra beklenecek süre; deneme hakkı bittiyse null.
    /// </summary>
    public static TimeSpan? NextDelay(int failedAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempts, 1);
        return failedAttempts <= Waits.Length ? Waits[failedAttempts - 1] : null;
    }
}
