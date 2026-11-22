namespace Koy.Core.Dock;

/// <summary>Sürükleme anının kararı: imlecin altındaki yuva, bölgesi, birleştirilebilir mi ve komşuların hangi hedefe göre kayacağı.</summary>
/// <param name="HoveredIndex">İmlecin (sürüklenen simgenin merkezi) üstünde olduğu yuva.</param>
/// <param name="Zone">Yuvanın içindeki bölge: sol %25 Before, orta %50 Merge, sağ %25 After.</param>
/// <param name="CanMerge">İmleç orta bantta ve yuva birleştirilebilir bir hedef: bekleme sonunda klasör oluşur.</param>
/// <param name="ShiftTarget">Komşu kaymasının hesaplandığı hedef yuva (<see cref="DockReorderMath.ShiftFor"/>); bırakınca sıra budur.</param>
public readonly record struct DockDragPlan(int HoveredIndex, DockDropZone Zone, bool CanMerge, int ShiftTarget);

/// <summary>Dock'ta simge sürüklerken sıra ve kayma hesapları (saf mantık; yay animasyonu hedefleri bunlardan gelir).</summary>
public static class DockReorderMath
{
    /// <summary>
    /// Sürüklenen simgenin merkezi, başlangıç yuvasından <paramref name="dragOffset"/> kadar kaydıysa hangi yuvaya düşer.
    /// Yuvalar eşit genişliktedir (<paramref name="slotWidth"/>); sonuç [0, count) aralığına kırpılır.
    /// </summary>
    public static int TargetIndex(int count, int originalIndex, double dragOffset, double slotWidth)
    {
        if (count <= 0 || slotWidth <= 0)
            return Math.Max(0, originalIndex);

        var shifted = (int)Math.Round(dragOffset / slotWidth, MidpointRounding.AwayFromZero);
        return Math.Clamp(originalIndex + shifted, 0, count - 1);
    }

    /// <summary>Orta bant genişliği (yuvanın oranı): bu bantta hedef yuva ve komşuları kaymaz, hedef yakalanır.</summary>
    public const double MergeBandRatio = 0.5;

    /// <summary>
    /// Yuvaların merkezleri (şerit koordinatı). Yuvalar eşit genişlikte olmak zorunda değil: saat, pil, ayırıcı gibi ek
    /// öğeler uygulama simgesinden farklı genişliktedir.
    /// </summary>
    public static double[] Centers(IReadOnlyList<double> widths)
    {
        var centers = new double[widths.Count];
        var left = 0.0;
        for (var i = 0; i < widths.Count; i++)
        {
            centers[i] = left + widths[i] / 2;
            left += widths[i];
        }
        return centers;
    }

    /// <summary>Sürüklenen simgenin kayma ofseti için sınırlar: merkezi en çok ilk ve son yuvanın merkezine kadar gider.</summary>
    public static (double Min, double Max) OffsetRange(IReadOnlyList<double> widths, int originalIndex, int minIndex, int maxIndex)
    {
        if (originalIndex < 0 || originalIndex >= widths.Count)
            return (0, 0);

        var centers = Centers(widths);
        var lo = Math.Clamp(minIndex, 0, widths.Count - 1);
        var hi = Math.Clamp(maxIndex, lo, widths.Count - 1);
        return (centers[lo] - centers[originalIndex], centers[hi] - centers[originalIndex]);
    }

    /// <summary>
    /// Sürükleme anının kararı. İmleç (sürüklenen simgenin merkezi) şerit koordinatında özgün merkez + ofset.
    /// Birleştirilebilir bir yuvanın orta bandında kayma dondurulur: sürüklenen yönde komşu, ancak imleç yuvanın
    /// ötesindeki kenar bölgeye (%25) geçince kayar; böylece hedef bekleme başlamadan kaçmaz. Birleştirilemeyen yuvada
    /// (ek öğe, sabitsiz simge) eski davranış: yuvaya girince kayar.
    /// </summary>
    /// <param name="widths">Her yuvanın sürükleme yönündeki genişliği (kenar boşlukları dahil).</param>
    public static DockDragPlan Evaluate(int originalIndex, double dragOffset, IReadOnlyList<double> widths, int minIndex, int maxIndex, Func<int, bool> isMergeTarget)
    {
        if (originalIndex < 0 || originalIndex >= widths.Count || maxIndex < minIndex || widths.Any(w => w <= 0))
            return new DockDragPlan(originalIndex, DockDropZone.Merge, false, originalIndex);

        var centers = Centers(widths);
        var cursor = centers[originalIndex] + dragOffset;
        var lo = Math.Clamp(minIndex, 0, widths.Count - 1);
        var hi = Math.Clamp(maxIndex, lo, widths.Count - 1);

        var hovered = hi;
        for (var i = lo; i < hi; i++)
        {
            if (cursor < centers[i] + widths[i] / 2)
            {
                hovered = i;
                break;
            }
        }

        var fraction = Math.Clamp((cursor - centers[hovered]) / widths[hovered], -0.5, 0.5);
        var zone = DockDropZones.DetectZone(1.0, fraction + 0.5, MergeBandRatio);

        var mergeable = hovered != originalIndex && isMergeTarget(hovered);
        int shiftTarget;
        if (!mergeable)
            shiftTarget = hovered;
        else if (hovered > originalIndex)
            shiftTarget = zone == DockDropZone.After ? hovered : hovered - 1;
        else
            shiftTarget = zone == DockDropZone.Before ? hovered : hovered + 1;

        return new DockDragPlan(hovered, zone, mergeable && zone == DockDropZone.Merge, shiftTarget);
    }

    /// <summary>
    /// Bırakınca öğeler yeni sıraya taşınır; her öğenin ekrandaki konumu korunacak (zıplama olmasın) ofsetler.
    /// Dönen dizi YENİ sıraya göre indekslidir: [k] = yeni k'inci yuvadaki öğenin ofseti.
    /// </summary>
    /// <param name="widths">Eski sıradaki genişlikler.</param>
    /// <param name="currentOffsets">Eski sıradaki öğelerin o anki ofsetleri.</param>
    public static double[] CommitOffsets(IReadOnlyList<double> widths, IReadOnlyList<double> currentOffsets, int originalIndex, int target)
    {
        var count = widths.Count;
        var order = Enumerable.Range(0, count).ToList();
        order.RemoveAt(originalIndex);
        order.Insert(Math.Clamp(target, 0, count - 1), originalIndex);

        var oldLeft = new double[count];
        var left = 0.0;
        for (var i = 0; i < count; i++)
        {
            oldLeft[i] = left;
            left += widths[i];
        }

        var result = new double[count];
        var newLeft = 0.0;
        for (var k = 0; k < count; k++)
        {
            var old = order[k];
            result[k] = oldLeft[old] + currentOffsets[old] - newLeft;
            newLeft += widths[old];
        }
        return result;
    }

    /// <summary>Kayması gereken yuvalar (sürüklenen hariç): sağa taşınırken arada kalanlar, sola taşınırken de.</summary>
    public static IEnumerable<int> ShiftedIndices(int originalIndex, int shiftTarget)
    {
        if (shiftTarget > originalIndex)
            return Enumerable.Range(originalIndex + 1, shiftTarget - originalIndex);
        if (shiftTarget < originalIndex)
            return Enumerable.Range(shiftTarget, originalIndex - shiftTarget);
        return [];
    }

    /// <summary>
    /// Sürükleme sürerken bir öğenin (sürüklenen hariç) yatay kayması: sürüklenen sağa geçtikçe aradakiler sola,
    /// sola geçtikçe sağa birer yuva kayar.
    /// </summary>
    public static double ShiftFor(int index, int draggedIndex, int targetIndex, double slotWidth)
    {
        if (index == draggedIndex)
            return 0;

        if (draggedIndex < targetIndex && index > draggedIndex && index <= targetIndex)
            return -slotWidth;

        if (targetIndex < draggedIndex && index >= targetIndex && index < draggedIndex)
            return slotWidth;

        return 0;
    }

    /// <summary>Sürükleme eşiği aşıldı mı (tıklama ile sürüklemeyi ayırır).</summary>
    public static bool ExceedsThreshold(double dx, double dy, double threshold)
        => Math.Abs(dx) >= threshold || Math.Abs(dy) >= threshold;
}
