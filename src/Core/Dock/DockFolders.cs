namespace Koy.Core.Dock;

/// <summary>
/// Dock klasörleri üzerinde saf durum geçişleri ve kuralları yöneten etki alanı motoru.
/// Tüm metotlar saf C# çalışır; giriş koleksiyonlarını değiştirmeden yeni değişmez durumlar üretir.
/// </summary>
public static class DockFolders
{
    /// <summary>
    /// Bir uygulamanın diğerinin üzerine bırakılmasıyla yeni bir klasör oluşturur.
    /// Hedef zaten bir klasör ise <see cref="AddToFolder"/> metoduna yönlendirir.
    /// </summary>
    public static (List<string> Order, List<DockFolder> Folders) CreateFromDrop(
        IReadOnlyList<string> order,
        IReadOnlyList<DockFolder> folders,
        string dragged,
        string target,
        string defaultName)
    {
        if (string.Equals(dragged, target, StringComparison.OrdinalIgnoreCase))
            return ([.. order], [.. folders]);

        // Klasörler başka bir öğenin içine sürüklenemez (iç içe klasör yok)
        if (PinnedOrder.IsFolderId(dragged))
            return ([.. order], [.. folders]);

        // Hedef zaten bir klasör ise klasöre ekleme yap
        if (PinnedOrder.IsFolderId(target))
            return AddToFolder(order, folders, dragged, PinnedOrder.ExtractFolderId(target));

        // Yeni benzersiz klasör kimliği üret
        var folderId = Guid.NewGuid().ToString("N")[..8];
        var folderToken = PinnedOrder.ToFolderToken(folderId);

        var newOrder = new List<string>(order);
        var targetIndex = newOrder.FindIndex(x => string.Equals(x, target, StringComparison.OrdinalIgnoreCase));

        // Sürüklenen öğeyi ana listeden çıkar
        newOrder.RemoveAll(x => string.Equals(x, dragged, StringComparison.OrdinalIgnoreCase));

        // Hedefin yerine klasör belirtecini yerleştir
        targetIndex = newOrder.FindIndex(x => string.Equals(x, target, StringComparison.OrdinalIgnoreCase));
        if (targetIndex >= 0)
            newOrder[targetIndex] = folderToken;
        else
            newOrder.Add(folderToken);

        var newFolder = new DockFolder(folderId, defaultName, [target, dragged]);
        var newFolders = new List<DockFolder>(folders) { newFolder };

        return (newOrder, newFolders);
    }

    /// <summary>
    /// Sürüklenen uygulamayı mevcut bir klasöre ekler ve ana dock sırasından kaldırır.
    /// </summary>
    public static (List<string> Order, List<DockFolder> Folders) AddToFolder(
        IReadOnlyList<string> order,
        IReadOnlyList<DockFolder> folders,
        string dragged,
        string folderId)
    {
        if (PinnedOrder.IsFolderId(dragged))
            return ([.. order], [.. folders]);

        var fId = PinnedOrder.ExtractFolderId(folderId);
        var targetFolder = folders.FirstOrDefault(f => string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase));
        if (targetFolder is null)
            return ([.. order], [.. folders]);

        var newOrder = new List<string>(order);
        newOrder.RemoveAll(x => string.Equals(x, dragged, StringComparison.OrdinalIgnoreCase));

        if (targetFolder.Items.Contains(dragged, StringComparer.OrdinalIgnoreCase))
            return (newOrder, [.. folders]);

        var newFolders = folders.Select(f =>
        {
            if (!string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase))
                return f;

            var items = new List<string>(f.Items) { dragged };
            return f with { Items = items };
        }).ToList();

        return (newOrder, newFolders);
    }

    /// <summary>
    /// Bir uygulamayı klasörden çıkarıp dock sırasındaki belirtilen konuma yerleştirir.
    /// Klasörde 1 öğe kalırsa klasör otomatik dağılır (kalan öğe klasörün sırasına geçer).
    /// Klasörde 0 öğe kalırsa klasör silinir.
    /// </summary>
    public static (List<string> Order, List<DockFolder> Folders) RemoveFromFolder(
        IReadOnlyList<string> order,
        IReadOnlyList<DockFolder> folders,
        string folderId,
        string item,
        int insertIndex)
    {
        var fId = PinnedOrder.ExtractFolderId(folderId);
        var targetFolder = folders.FirstOrDefault(f => string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase));
        if (targetFolder is null || !targetFolder.Items.Contains(item, StringComparer.OrdinalIgnoreCase))
            return ([.. order], [.. folders]);

        var newOrder = new List<string>(order);
        var newFolders = new List<DockFolder>();
        var remainingItems = targetFolder.Items.Where(x => !string.Equals(x, item, StringComparison.OrdinalIgnoreCase)).ToList();
        var folderToken = PinnedOrder.ToFolderToken(targetFolder.Id);

        if (remainingItems.Count >= 2)
        {
            foreach (var f in folders)
            {
                newFolders.Add(string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase)
                    ? f with { Items = remainingItems }
                    : f);
            }
        }
        else if (remainingItems.Count == 1)
        {
            // Tek öğe kaldı: klasör dağılır, kalan tek öğe klasörün yuvasına geçer
            foreach (var f in folders)
            {
                if (!string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase))
                    newFolders.Add(f);
            }

            var singleItem = remainingItems[0];
            var fIdx = newOrder.FindIndex(x => string.Equals(x, folderToken, StringComparison.OrdinalIgnoreCase));
            if (fIdx >= 0)
                newOrder[fIdx] = singleItem;
            else
                newOrder.Add(singleItem);
        }
        else
        {
            // 0 öğe kaldı: klasör silinir
            foreach (var f in folders)
            {
                if (!string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase))
                    newFolders.Add(f);
            }

            newOrder.RemoveAll(x => string.Equals(x, folderToken, StringComparison.OrdinalIgnoreCase));
        }

        // Çıkarılan öğeyi dock sırasına ekle
        newOrder.RemoveAll(x => string.Equals(x, item, StringComparison.OrdinalIgnoreCase));
        var clampedIndex = Math.Clamp(insertIndex, 0, newOrder.Count);
        newOrder.Insert(clampedIndex, item);

        return (newOrder, newFolders);
    }

    /// <summary>
    /// Klasörü dağıtır; içindeki tüm uygulamalar klasörün dock'taki sırasına ardışık olarak dizilir.
    /// </summary>
    public static (List<string> Order, List<DockFolder> Folders) Dissolve(
        IReadOnlyList<string> order,
        IReadOnlyList<DockFolder> folders,
        string folderId)
    {
        var fId = PinnedOrder.ExtractFolderId(folderId);
        var targetFolder = folders.FirstOrDefault(f => string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase));
        if (targetFolder is null)
            return ([.. order], [.. folders]);

        var folderToken = PinnedOrder.ToFolderToken(fId);
        var newOrder = new List<string>();
        var replaced = false;

        foreach (var id in order)
        {
            if (string.Equals(id, folderToken, StringComparison.OrdinalIgnoreCase))
            {
                newOrder.AddRange(targetFolder.Items);
                replaced = true;
            }
            else
            {
                newOrder.Add(id);
            }
        }

        if (!replaced)
        {
            newOrder.AddRange(targetFolder.Items);
        }

        var newFolders = folders.Where(f => !string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase)).ToList();
        return (newOrder, newFolders);
    }

    /// <summary>
    /// Klasörün adını günceller. Boş veya salt boşluklu adlar yok sayılır ve eski ad korunur.
    /// </summary>
    public static List<DockFolder> Rename(
        IReadOnlyList<DockFolder> folders,
        string folderId,
        string newName)
    {
        var fId = PinnedOrder.ExtractFolderId(folderId);
        var trimmed = newName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return [.. folders];

        return folders.Select(f =>
            string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase)
                ? f with { Name = trimmed }
                : f).ToList();
    }

    /// <summary>
    /// Klasör içindeki öğelerin sırasını indekslere göre değiştirir.
    /// </summary>
    public static List<DockFolder> ReorderInFolder(
        IReadOnlyList<DockFolder> folders,
        string folderId,
        int oldIndex,
        int newIndex)
    {
        var fId = PinnedOrder.ExtractFolderId(folderId);
        return folders.Select(f =>
        {
            if (!string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase))
                return f;

            var items = new List<string>(f.Items);
            if (oldIndex < 0 || oldIndex >= items.Count)
                return f;

            var item = items[oldIndex];
            items.RemoveAt(oldIndex);
            var target = Math.Clamp(newIndex, 0, items.Count);
            items.Insert(target, item);
            return f with { Items = items };
        }).ToList();
    }

    /// <summary>
    /// Klasör içindeki öğelerin sırasını öğe adına göre değiştirir.
    /// </summary>
    public static List<DockFolder> ReorderInFolder(
        IReadOnlyList<DockFolder> folders,
        string folderId,
        string item,
        int newIndex)
    {
        var fId = PinnedOrder.ExtractFolderId(folderId);
        return folders.Select(f =>
        {
            if (!string.Equals(f.Id, fId, StringComparison.OrdinalIgnoreCase))
                return f;

            var items = new List<string>(f.Items);
            var from = items.FindIndex(x => string.Equals(x, item, StringComparison.OrdinalIgnoreCase));
            if (from < 0)
                return f;

            items.RemoveAt(from);
            var target = Math.Clamp(newIndex, 0, items.Count);
            items.Insert(target, item);
            return f with { Items = items };
        }).ToList();
    }

    /// <summary>
    /// Çakışmayan bir sonraki varsayılan klasör adını üretir ("Klasör", "Klasör 2", "Klasör 3"...).
    /// </summary>
    public static string NextDefaultName(IReadOnlyList<DockFolder> folders, string baseName)
    {
        var existing = new HashSet<string>(folders.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName))
            return baseName;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!existing.Contains(candidate))
                return candidate;
        }

        return $"{baseName} {Guid.NewGuid().ToString("N")[..4]}";
    }

    /// <summary>
    /// Klasörleri ve dock sırasını mevcut kısayol dosyalarına göre eşitler.
    /// </summary>
    public static (List<string> Order, List<DockFolder> Folders) Reconcile(
        IReadOnlyList<string> order,
        IReadOnlyList<DockFolder> folders,
        IReadOnlyList<string> currentAvailableLnkIds) =>
        PinnedOrder.Reconcile(order, currentAvailableLnkIds, folders);
}
