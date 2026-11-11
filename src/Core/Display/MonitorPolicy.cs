namespace Koy.Core.Display;

/// <summary>Bir monitör için çözümlenmiş sonuç: çentik ve dock orada açık mı.</summary>
/// <param name="Monitor">Şu an bağlı monitör.</param>
/// <param name="NotchEnabled">Bu monitörde çentik açık.</param>
/// <param name="DockEnabled">Bu monitörde dock açık.</param>
/// <param name="IsKnown">Ayar sözlüğünde bu monitör için kayıt var (false: yeni ekran, varsayılan uygulandı).</param>
public sealed record MonitorAssignment(DisplayMonitor Monitor, bool NotchEnabled, bool DockEnabled, bool IsKnown);

/// <summary>
/// "Hangi monitörde ne açık" kararı ve ayar sözlüğü düzenlemeleri. Saf mantık: monitör listesi ve ayarlar girer, karar çıkar.
/// </summary>
public static class MonitorPolicy
{
    /// <summary>
    /// Yeni takılan (kaydı olmayan) ekranın varsayılanı: çentik ve dock AÇIK. Ayarlar ekran başına baştan yapılmaz;
    /// genel ayarlar (dock biçimi, çentik görünürlüğü…) her ekrana aynen uygulanır. Bir ekranı kapatmak isteyen
    /// Ekranlar sayfasından o ekranı seçip kapatır; bu yalnız o ekranın kaydını yazar, öbürlerine dokunmaz.
    /// </summary>
    public const NewScreenMode DefaultNewScreenMode = NewScreenMode.NotchAndDock;

    /// <summary>Yeni ekran varsayılanının monitör ayarı karşılığı.</summary>
    public static MonitorSettings DefaultFor(NewScreenMode mode) => mode switch
    {
        NewScreenMode.NotchAndDock => new MonitorSettings { NotchEnabled = true, DockEnabled = true },
        NewScreenMode.NotchOnly => new MonitorSettings { NotchEnabled = true, DockEnabled = false },
        _ => new MonitorSettings { NotchEnabled = false, DockEnabled = false }
    };

    /// <summary>
    /// Kayıtlı anahtar bu monitöre ait mi? Kararlı kimlik (<see cref="DisplayMonitor.Id"/>) ya da eski sürümlerin
    /// yazdığı Windows aygıt adı (<c>\\.\DISPLAY2</c>, <see cref="DisplayMonitor.DeviceName"/>) eşleşir.
    /// Çözünürlük, konum ya da sıra değişince kimlik değişmez; aygıt adı değişebilir, o yüzden yalnız eski kayıtlar için yedek.
    /// </summary>
    public static bool KeyMatches(string key, DisplayMonitor monitor) =>
        string.Equals(key, monitor.Id, StringComparison.OrdinalIgnoreCase) ||
        (monitor.DeviceName.Length > 0 && string.Equals(key, monitor.DeviceName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Monitör için kayıtlı anahtarı döner; önce tam kimlik, sonra eski aygıt adı. Yoksa null.</summary>
    public static string? FindKey(IReadOnlyDictionary<string, MonitorSettings>? saved, DisplayMonitor monitor)
    {
        if (saved is null || saved.Count == 0)
            return null;

        foreach (var key in saved.Keys)
            if (string.Equals(key, monitor.Id, StringComparison.OrdinalIgnoreCase))
                return key;

        foreach (var key in saved.Keys)
            if (KeyMatches(key, monitor))
                return key;

        return null;
    }

    /// <summary>Her bağlı monitör için çentik/dock durumunu çözümler. Kaydı olmayan ekrana yeni ekran varsayılanı uygulanır.</summary>
    public static IReadOnlyList<MonitorAssignment> Resolve(
        IReadOnlyDictionary<string, MonitorSettings>? saved,
        NewScreenMode newScreenDefault,
        IReadOnlyList<DisplayMonitor> monitors)
    {
        var result = new List<MonitorAssignment>(monitors.Count);
        var fallback = DefaultFor(newScreenDefault);

        foreach (var monitor in monitors)
        {
            var key = FindKey(saved, monitor);
            if (key is not null)
            {
                var s = saved![key];
                result.Add(new MonitorAssignment(monitor, s.NotchEnabled, s.DockEnabled, IsKnown: true));
            }
            else
            {
                result.Add(new MonitorAssignment(monitor, fallback.NotchEnabled, fallback.DockEnabled, IsKnown: false));
            }
        }

        return result;
    }

    /// <summary>Çentiğin açık olduğu monitörler.</summary>
    public static IReadOnlyList<DisplayMonitor> NotchMonitors(IReadOnlyList<MonitorAssignment> assignments) =>
        assignments.Where(a => a.NotchEnabled).Select(a => a.Monitor).ToList();

    /// <summary>Dock'un açık olduğu monitörler.</summary>
    public static IReadOnlyList<DisplayMonitor> DockMonitors(IReadOnlyList<MonitorAssignment> assignments) =>
        assignments.Where(a => a.DockEnabled).Select(a => a.Monitor).ToList();

    /// <summary>
    /// Bir monitörün ayarını değiştirilmiş yeni sözlük döner (eskisine dokunmaz). Anahtar kararlı kimliğe çevrilir:
    /// eski aygıt adıyla kayıtlı ayar varsa devralınır ve eski anahtar silinir. Bağlı olmayan monitörlerin kayıtları korunur.
    /// </summary>
    public static Settings.ValueDictionary<MonitorSettings> Update(
        IReadOnlyDictionary<string, MonitorSettings>? saved,
        NewScreenMode newScreenDefault,
        DisplayMonitor monitor,
        Func<MonitorSettings, MonitorSettings> change)
    {
        var copy = saved is null ? new Settings.ValueDictionary<MonitorSettings>() : new Settings.ValueDictionary<MonitorSettings>(saved);

        var oldKey = FindKey(copy, monitor);
        var current = oldKey is not null ? copy[oldKey] : DefaultFor(newScreenDefault);
        if (oldKey is not null)
            copy.Remove(oldKey);

        var updated = change(current with { });
        copy[monitor.Id] = updated with { Name = monitor.Name };
        return copy;
    }
}
