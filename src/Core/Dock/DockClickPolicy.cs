namespace Koy.Core.Dock;

/// <summary>Sol tıkın sonucu: hangi pencere ne yapılacak. Saf karar; uygulaması <see cref="DockController"/>'dadır.</summary>
public enum DockClickKind
{
    None,
    Activate,
    Minimize,

    /// <summary>Pencere önizlemesini aç. Arayüz açamıyorsa (önizleme kapalı) öne getirme olarak uygulanır.</summary>
    ShowThumbnails
}

public readonly record struct DockClickPlan(DockClickKind Kind, nint Handle);

public static class DockClickPolicy
{
    /// <summary>
    /// Çalışan bir simgeye sol tık kararı. Çalışmayan simge başlatılır (burada değil).
    /// </summary>
    /// <param name="handles">Simgenin pencereleri (girdi sırası).</param>
    /// <param name="foreground">Şu an ön plandaki pencere (0 = yok).</param>
    /// <param name="isActive">Simgenin pencerelerinden biri ön planda mı.</param>
    public static DockClickPlan DecideLeftClick(DockLeftClickAction action, IReadOnlyList<nint> handles, nint foreground, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(handles);
        if (handles.Count == 0)
            return new DockClickPlan(DockClickKind.None, 0);

        switch (action)
        {
            case DockLeftClickAction.BringToFront:
                return new DockClickPlan(DockClickKind.Activate, handles[0]);

            case DockLeftClickAction.CycleWindows:
                if (!isActive)
                    return new DockClickPlan(DockClickKind.Activate, handles[0]);
                var current = IndexOf(handles, foreground);
                // Ön plan pencere listede yoksa (isActive ile çelişir) baştan başlanır.
                var next = current < 0 ? 0 : (current + 1) % handles.Count;
                return new DockClickPlan(DockClickKind.Activate, handles[next]);

            case DockLeftClickAction.ShowThumbnails:
                return new DockClickPlan(DockClickKind.ShowThumbnails, handles[0]);

            // MinimizeIfActive ve tanımsız (dosyadan gelen) değerler: bugünkü dock davranışı.
            default:
                return isActive
                    ? new DockClickPlan(DockClickKind.Minimize, handles[0])
                    : new DockClickPlan(DockClickKind.Activate, handles[0]);
        }
    }

    private static int IndexOf(IReadOnlyList<nint> handles, nint handle)
    {
        for (var i = 0; i < handles.Count; i++)
            if (handles[i] == handle)
                return i;
        return -1;
    }
}
