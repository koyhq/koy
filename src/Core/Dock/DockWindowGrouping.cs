using Koy.Core.Settings;

namespace Koy.Core.Dock;

/// <summary>
/// Pencere gruplama kararı (saf). Gruplama anahtarı (süreç yolu / pencere) ve sıra DockController'da kalır; burada yalnız
/// "gruplansın mı" kararı ve "yer yetmeyince" eşiği vardır.
/// </summary>
public static class DockWindowGrouping
{
    /// <summary>"Yer kalmayınca grupla" kipinde dock'un sığdırdığı en çok simge sayısı (sabitliler + açık pencereler).</summary>
    public const int SlotLimit = 16;

    /// <param name="mode">Ayardaki gruplama kipi.</param>
    /// <param name="totalEntryCount">Gruplanmazsa dock'ta görünecek simge sayısı (sabitli öğeler + sabitlenmemiş pencereler).</param>
    public static bool ShouldGroup(DockGroupingMode mode, int totalEntryCount) => mode switch
    {
        DockGroupingMode.Always => true,
        // Yer varken gruplamak pencereleri gereksiz yere saklar; bu kip yalnız sığmayınca birleştirir.
        DockGroupingMode.WhenSpaceRunsOut => totalEntryCount > SlotLimit,
        // Dosyadan gelen tanımsız sayı (örn. 7) gruplamasıza düşer.
        _ => false
    };
}
