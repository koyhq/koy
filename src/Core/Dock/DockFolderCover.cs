namespace Koy.Core.Dock;

/// <summary>
/// Klasör kapağı: dock'taki klasör simgesi 2×2 ızgaradır. İlk dört uygulama sırayla (soldan sağa, yukarıdan aşağı) görünür;
/// beşinci ve sonrası yalnız klasör açılınca görünür; kullanılmayan hücreler boş kalır. Kapsül ve tam genişlik aynı kuralı kullanır.
/// </summary>
public static class DockFolderCover
{
    /// <summary>Kapaktaki hücre sayısı (2×2).</summary>
    public const int CellCount = 4;

    /// <summary>
    /// Dört hücrenin her biri için gösterilecek üyenin sırası (satır öncelikli); boş hücre null.
    /// 2 uygulama: [0, 1, boş, boş]; 3 uygulama: [0, 1, 2, boş]; 4 ve üstü: [0, 1, 2, 3].
    /// </summary>
    public static IReadOnlyList<int?> Cells(int memberCount)
    {
        var cells = new int?[CellCount];
        for (var i = 0; i < CellCount && i < memberCount; i++)
            cells[i] = i;
        return cells;
    }
}
