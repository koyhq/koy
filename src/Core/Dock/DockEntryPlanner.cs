namespace Koy.Core.Dock;

/// <summary>Planlanan dock girdisi: tek simge ya da klasör.</summary>
public abstract record PlannedEntry;

/// <summary>
/// Tek simge. <paramref name="Model"/> yeni görünüm oluşturulurken, <paramref name="UpdateModel"/> aynı kimlikli
/// görünüm zaten varsa onu güncellemek için kullanılır (kimlikler çakışmadıkça ikisi aynıdır).
/// </summary>
public sealed record PlannedItem(DockItem Model, DockItem UpdateModel) : PlannedEntry;

/// <summary>Klasörün bir üyesi. Pencere/kısayol listesinde yoksa <paramref name="IsFallback"/> ile dosya adından üretilmiş modeldir.</summary>
public sealed record PlannedFolderMember(string ItemId, DockItem Model, bool IsFallback);

public sealed record PlannedFolder(DockFolder Folder, IReadOnlyList<PlannedFolderMember> Members) : PlannedEntry;

/// <summary>Dock şeridinin planı: etkin sabit sıra ve sırayla girdiler.</summary>
public sealed record DockEntryPlan(IReadOnlyList<string> EffectiveOrder, IReadOnlyList<PlannedEntry> Entries);

/// <summary>
/// DockViewModel.UpdateItems'in karar kısmı (saf): hangi simge hangi sırayla, hangileri klasörde, hangi klasör üyesi
/// listede yok. ViewModel yalnız planı mevcut görünümlerle eşler (var olanı günceller, yoksa üretir).
/// </summary>
public static class DockEntryPlanner
{
    /// <param name="models">Görünür modeller (sabitli + açık pencereler).</param>
    /// <param name="pinnedOrder">Kayıtlı sabit sıra (kısayol dosya adları ve klasör belirteçleri).</param>
    /// <param name="folders">Kayıtlı klasörler.</param>
    public static DockEntryPlan Plan(
        IReadOnlyList<DockItem> models,
        IReadOnlyList<string> pinnedOrder,
        IReadOnlyList<DockFolder> folders)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(pinnedOrder);
        ArgumentNullException.ThrowIfNull(folders);

        // 1. Klasörlerdeki uygulamalar ana dock sırasında ayrıca görünmez.
        var inFolder = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            foreach (var app in folder.Items)
                inFolder.Add(app);
        }

        // 2. Modeller kimliğe ya da kısayol adına göre aranır (sonraki aynı anahtar öncekini ezer).
        var byKey = new Dictionary<string, DockItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in models)
        {
            byKey[m.Id] = m;
            byKey[PinnedOrder.IdOf(m.Id)] = m;
        }

        // 3. Hedef sıra: sabitliler ve klasörler kayıtlı sırayla, ardından sabitlenmemiş açık pencereler.
        var availableLnkIds = models.Where(m => m.IsPinned).Select(m => PinnedOrder.IdOf(m.Id)).ToList();
        var (effectiveOrder, reconciledFolders) = PinnedOrder.Reconcile(pinnedOrder, availableLnkIds, folders);
        var folderById = reconciledFolders.ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);

        var entries = new List<PlannedEntry>();
        foreach (var token in effectiveOrder)
        {
            if (PinnedOrder.IsFolderId(token))
            {
                if (folderById.TryGetValue(PinnedOrder.ExtractFolderId(token), out var folder))
                    entries.Add(new PlannedFolder(folder, folder.Items.Select(id => Member(id, byKey)).ToList()));
            }
            else if (!inFolder.Contains(token) && byKey.TryGetValue(token, out var model))
            {
                entries.Add(new PlannedItem(model, model));
            }
        }

        foreach (var m in models.Where(m => !m.IsPinned))
        {
            if (inFolder.Contains(PinnedOrder.IdOf(m.Id)) || inFolder.Contains(m.Id))
                continue;

            if (byKey.TryGetValue(m.Id, out var cached))
                entries.Add(new PlannedItem(cached, m));
        }

        return new DockEntryPlan(effectiveOrder, entries);
    }

    private static PlannedFolderMember Member(string itemId, Dictionary<string, DockItem> byKey) =>
        byKey.TryGetValue(itemId, out var model)
            ? new PlannedFolderMember(itemId, model, IsFallback: false)
            : new PlannedFolderMember(itemId, FallbackModel(itemId), IsFallback: true);

    /// <summary>Klasörde kayıtlı ama listede olmayan öğe için dosya adından üretilen, çalışmayan sabitli model.</summary>
    public static DockItem FallbackModel(string itemId) => new(
        Id: itemId,
        Title: Path.GetFileNameWithoutExtension(itemId),
        FilePath: itemId,
        TargetPath: null,
        IconData: null,
        IsPinned: true,
        IsRunning: false,
        IsActive: false,
        WindowHandles: []);
}

public enum DockListOpKind
{
    /// <summary><see cref="DockListOp.Index"/> konumundaki öğeyi çıkar.</summary>
    Remove,
    /// <summary><see cref="DockListOp.Index"/> konumundaki öğeyi <see cref="DockListOp.ToIndex"/> konumuna taşı.</summary>
    Move,
    /// <summary>İstenen listenin <see cref="DockListOp.ToIndex"/> numaralı öğesini aynı konuma ekle.</summary>
    Insert,
    /// <summary>İstenen listenin <see cref="DockListOp.ToIndex"/> numaralı öğesini sona ekle.</summary>
    Add
}

public readonly record struct DockListOp(DockListOpKind Kind, int Index, int ToIndex);

/// <summary>
/// Dock şeridinin mevcut sırasını istenen sıraya yerinde dönüştüren işlem listesi (saf; kimlikler büyük/küçük harf
/// duyarsız). Koleksiyon baştan kurulmaz: yalnız çıkan, taşınan ve eklenen öğeler değişir, böylece yaylı hareket sürer.
/// </summary>
public static class DockListSync
{
    public static IReadOnlyList<DockListOp> Plan(IReadOnlyList<string> currentIds, IReadOnlyList<string> desiredIds)
    {
        ArgumentNullException.ThrowIfNull(currentIds);
        ArgumentNullException.ThrowIfNull(desiredIds);

        var ids = currentIds.ToList();
        var ops = new List<DockListOp>();
        var desired = new HashSet<string>(desiredIds, StringComparer.OrdinalIgnoreCase);

        for (var i = ids.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(ids[i]))
            {
                ops.Add(new DockListOp(DockListOpKind.Remove, i, -1));
                ids.RemoveAt(i);
            }
        }

        for (var target = 0; target < desiredIds.Count; target++)
        {
            var id = desiredIds[target];
            var existing = ids.FindIndex(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                if (existing != target && existing < ids.Count && target < ids.Count)
                {
                    ops.Add(new DockListOp(DockListOpKind.Move, existing, target));
                    var moved = ids[existing];
                    ids.RemoveAt(existing);
                    ids.Insert(target, moved);
                }
            }
            else if (target < ids.Count)
            {
                ops.Add(new DockListOp(DockListOpKind.Insert, -1, target));
                ids.Insert(target, id);
            }
            else
            {
                ops.Add(new DockListOp(DockListOpKind.Add, -1, target));
                ids.Add(id);
            }
        }

        return ops;
    }
}
