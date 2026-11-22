namespace Koy.Core.Dock;

/// <summary>Sabitli uygulamaların sırasını değiştirebilen kaynak (dock'ta sürükleyerek sıralama).</summary>
public interface IReorderablePinnedApps : IPinnedAppsService
{
    /// <summary>Kimliği verilen sabitli uygulamayı yeni dizine taşır ve sırayı kalıcı yapar.</summary>
    void MoveTo(string id, int newIndex);

    /// <summary>Sıra başka yoldan (ek öğeli düzende sürükleme) kalıcı yazıldı: bellekteki kopya eşitlenir, yazma yapılmaz.</summary>
    void SetOrder(IReadOnlyList<string> order);
}

/// <summary>
/// Klasör sırasındaki sabitli uygulamaları Koy'un kendi kayıtlı sırasına göre dizer.
/// Kayıt boşsa (ilk açılış) Windows'un sırası tohum alınır; sonra Koy'un listesi esastır: yeni sabitlenen sona eklenir,
/// kaldırılan listeden düşer. G/Ç dışarıdan verilir (ayar dosyası, kayıt defteri), böylece mantık testlenir.
/// </summary>
public sealed class OrderedPinnedAppsService(
    IPinnedAppsService inner,
    Func<IReadOnlyList<string>> loadOrder,
    Action<IReadOnlyList<string>> saveOrder,
    Func<byte[]?>? readTaskbandFavorites = null) : IReorderablePinnedApps
{
    private List<string>? _order;

    public IReadOnlyList<PinnedApp> GetPinnedApps()
    {
        var apps = inner.GetPinnedApps();
        var ids = apps.Select(a => PinnedOrder.IdOf(a.ShortcutPath)).ToList();

        _order ??= [.. loadOrder()];

        // Klasör okunamadıysa (boş liste) kayıt olduğu gibi kalır; boş sonuçla sıra silinmez.
        if (ids.Count == 0)
            return apps;

        List<string> effective;
        if (_order.Count == 0)
        {
            byte[]? favorites = null;
            try
            {
                favorites = readTaskbandFavorites?.Invoke();
            }
            catch
            {
                // Belgesiz kaynak okunamazsa klasör sırasına düşülür.
            }

            effective = PinnedOrder.SeedFromTaskband(favorites, ids);
        }
        else
        {
            effective = PinnedOrder.Reconcile(_order, ids);
        }

        if (!effective.SequenceEqual(_order, StringComparer.OrdinalIgnoreCase))
        {
            _order = effective;
            Save();
        }

        return PinnedOrder.Apply(apps, _order);
    }

    public void MoveTo(string id, int newIndex)
    {
        _order ??= [.. loadOrder()];
        var moved = PinnedOrder.Move(_order, id, newIndex);
        if (moved.SequenceEqual(_order, StringComparer.OrdinalIgnoreCase))
            return;

        _order = moved;
        Save();
    }

    public void SetOrder(IReadOnlyList<string> order) => _order = [.. order];

    private void Save()
    {
        try
        {
            saveOrder(_order!);
        }
        catch
        {
            // Ayar dosyası yazılamazsa sıra bu oturumda bellekte geçerli kalır.
        }
    }
}
