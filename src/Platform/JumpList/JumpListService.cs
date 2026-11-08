using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Koy.Core.JumpList;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace Koy.Platform.JumpList;

/// <summary>
/// Windows görev çubuğundaki uygulama bazlı sağ tık menüsünün (atlama listesi) verisini okur.
/// <list type="bullet">
/// <item>Görevler ve uygulamanın kendi kategorileri: <c>CustomDestinations\&lt;hash&gt;.customDestinations-ms</c>
/// (<see cref="CustomDestinationsReader"/>) ve her öğe Windows'un kendi ShellLink'iyle (<c>IPersistStream.Load</c>) çözülür.</item>
/// <item>Son / sık kullanılanlar: belgeli <c>IApplicationDocumentLists</c>.</item>
/// </list>
/// Boşta hiçbir şey çalışmaz: izleyici, zamanlayıcı, yoklama, kalıcı iş parçacığı yoktur. Her okuma, bitince biten kısa
/// ömürlü bir <b>STA</b> iş parçacığında yapılır (kabuk nesneleri apartman ister; iş parçacığı havuzu MTA'dır) ve çağıran
/// iş parçacığı beklemez. Sonuçlar iki kaynak dosyanın boyut + yazılma zamanı damgasıyla, en çok
/// <see cref="JumpListCache.DefaultCapacity"/> uygulama için önbelleğe alınır.
/// </summary>
public sealed class JumpListService : IJumpListService
{
    private static readonly string RecentRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Windows\Recent");

    private static readonly Guid ApplicationDocumentListsClsid = new("86bec222-30f2-47e0-9f25-60d11cd75c28");
    private static readonly Guid ShellItemIid = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private static readonly Guid ShellLinkIid = new("000214F9-0000-0000-C000-000000000046");
    private static readonly Guid ObjectArrayIid = new("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");

    private readonly JumpListCache _cache = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, Task<IReadOnlyList<JumpListSection>>> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<JumpListSection>> GetAsync(string appUserModelId, CancellationToken cancellationToken = default)
    {
        var hash = JumpListAppId.HashName(appUserModelId);
        if (hash is null)
            return Task.FromResult<IReadOnlyList<JumpListSection>>([]);

        var customPath = Path.Combine(RecentRoot, "CustomDestinations", hash + ".customDestinations-ms");
        var automaticPath = Path.Combine(RecentRoot, "AutomaticDestinations", hash + ".automaticDestinations-ms");
        var custom = Stamp(customPath);
        var automatic = Stamp(automaticPath);

        if (_cache.TryGet(appUserModelId, custom, automatic, out var cached))
            return Task.FromResult(cached);

        Task<IReadOnlyList<JumpListSection>> task;
        lock (_gate)
        {
            if (!_inFlight.TryGetValue(appUserModelId, out task!))
            {
                task = RunOnStaThread(() =>
                {
                    var sections = Read(appUserModelId, customPath);
                    _cache.Set(appUserModelId, custom, automatic, sections);
                    return sections;
                });
                _inFlight[appUserModelId] = task;
                // Devam eden işi, bittiğinde (başarısız olsa da) listeden çıkar.
                _ = task.ContinueWith(_ =>
                {
                    lock (_gate)
                        _inFlight.Remove(appUserModelId);
                }, TaskScheduler.Default);
            }
        }

        return cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task;
    }

    public void Launch(JumpListEntry entry, string? appExePath)
    {
        if (entry.Launch is null)
            return;

        try
        {
            var plan = JumpListLaunchPlan.For(entry.Launch, appExePath);
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ExpandEnvironmentVariables(plan.File),
                UseShellExecute = true,
            };
            if (!string.IsNullOrEmpty(plan.Arguments))
                psi.Arguments = plan.Arguments;
            if (!string.IsNullOrEmpty(plan.WorkingDirectory) && Directory.Exists(plan.WorkingDirectory))
                psi.WorkingDirectory = plan.WorkingDirectory;

            Process.Start(psi)?.Dispose();
        }
        catch
        {
            // Hedef silinmiş / yetki yok / kullanıcı iptali: menü öğesi sessizce işlemsiz kalır.
        }
    }

    public void Clear()
    {
        _cache.Clear();
    }

    private static FileStamp Stamp(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(info.Length, info.LastWriteTimeUtc.Ticks) : FileStamp.Missing;
        }
        catch
        {
            return FileStamp.Missing;
        }
    }

    private static Task<IReadOnlyList<JumpListSection>> RunOnStaThread(Func<IReadOnlyList<JumpListSection>> work)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<JumpListSection>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(work());
            }
            catch
            {
                tcs.SetResult([]); // hata menüyü bozmaz; sonuç önbelleğe alınmaz
            }
        })
        {
            IsBackground = true,
            Name = "Koy.JumpList",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private static IReadOnlyList<JumpListSection> Read(string appId, string customPath)
    {
        var file = ReadCustomDestinations(customPath);
        var raw = new List<JumpListSection>();
        var customSections = 0;

        foreach (var category in file.Categories)
        {
            switch (category.Kind)
            {
                case CustomDestinationsCategoryKind.Custom when customSections < JumpListLimits.MaxCustomSections:
                    customSections++;
                    raw.Add(new JumpListSection(
                        JumpListSectionKind.Custom,
                        ShellLinkEntryReader.Resolve(category.Name),
                        ResolveLinks(category.Items, JumpListLayout.CapFor(JumpListSectionKind.Custom))));
                    break;
                case CustomDestinationsCategoryKind.Tasks:
                    raw.Add(new JumpListSection(
                        JumpListSectionKind.Tasks, null,
                        ResolveLinks(category.Items, JumpListLayout.CapFor(JumpListSectionKind.Tasks))));
                    break;
            }
        }

        if (JumpListLayout.ShowsFrequent(file))
            raw.Add(new JumpListSection(JumpListSectionKind.Frequent, null,
                ReadDocumentList(appId, APPDOCLISTTYPE.ADLT_FREQUENT, JumpListLimits.MaxFrequent)));
        if (JumpListLayout.ShowsRecent(file))
            raw.Add(new JumpListSection(JumpListSectionKind.Recent, null,
                ReadDocumentList(appId, APPDOCLISTTYPE.ADLT_RECENT, JumpListLimits.MaxRecent)));

        return JumpListLayout.Arrange(raw);
    }

    private static CustomDestinationsFile ReadCustomDestinations(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > JumpListLimits.MaxFileBytes)
                return CustomDestinationsFile.Empty;

            // Uygulama dosyayı yazıyor olabilir: paylaşımlı aç. Üst sınır yukarıda denetlendi.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[(int)Math.Min(stream.Length, JumpListLimits.MaxFileBytes)];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = stream.Read(buffer, read, buffer.Length - read);
                if (n == 0)
                    break;
                read += n;
            }

            return CustomDestinationsReader.Parse(buffer.AsSpan(0, read));
        }
        catch
        {
            return CustomDestinationsFile.Empty;
        }
    }

    /// <summary>Kategori öğelerini sırayla çözer; <paramref name="cap"/> girdiye ulaşınca kalanını hiç açmaz.</summary>
    private static IReadOnlyList<JumpListEntry> ResolveLinks(IReadOnlyList<byte[]> items, int cap)
    {
        var entries = new List<JumpListEntry>();
        var count = 0;
        foreach (var item in items)
        {
            if (count >= cap)
                break;

            var entry = ShellLinkEntryReader.FromStream(item);
            if (entry is null)
                continue;

            if (!entry.IsSeparator)
                count++;
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>IApplicationDocumentLists ile bir AppID'nin son ya da sık kullanılanları.</summary>
    private static unsafe IReadOnlyList<JumpListEntry> ReadDocumentList(string appId, APPDOCLISTTYPE type, int max)
    {
        var entries = new List<JumpListEntry>();
        object? lists = null;
        object? array = null;
        try
        {
            var clsType = Type.GetTypeFromCLSID(ApplicationDocumentListsClsid);
            if (clsType is null)
                return entries;

            lists = Activator.CreateInstance(clsType);
            if (lists is not IApplicationDocumentLists adl)
                return entries;

            fixed (char* id = appId)
                adl.SetAppID(id);

            var iid = ObjectArrayIid;
            adl.GetList(type, (uint)max, &iid, out array);
            if (array is not IObjectArray objects)
                return entries;

            objects.GetCount(out var count);
            for (uint i = 0; i < Math.Min(count, (uint)max); i++)
            {
                var entry = ReadDocumentEntry(objects, i);
                if (entry is not null)
                    entries.Add(entry);
            }
        }
        catch
        {
            // Liste yok / AppID tanınmıyor: bölüm boş kalır.
        }
        finally
        {
            ShellLinkEntryReader.Release(array);
            ShellLinkEntryReader.Release(lists);
        }

        return entries;
    }

    private static unsafe JumpListEntry? ReadDocumentEntry(IObjectArray objects, uint index)
    {
        object? element = null;
        try
        {
            // Öğeler IShellItem olarak gelir; kısayol (IShellLink) gelirse onu da çözeriz.
            var shellItem = ShellItemIid;
            try
            {
                objects.GetAt(index, &shellItem, out element);
                if (element is IShellItem item)
                    return ShellLinkEntryReader.FromShellItem(item);
            }
            catch (COMException)
            {
                ShellLinkEntryReader.Release(element);
                element = null;
            }

            var shellLink = ShellLinkIid;
            objects.GetAt(index, &shellLink, out element);
            // Kısayol olarak gelen son kullanılan, olduğu gibi çalışır.
            return element is IShellLinkW && ShellLinkEntryReader.FromLink(element) is { IsSeparator: false } link ? link : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            ShellLinkEntryReader.Release(element);
        }
    }
}
