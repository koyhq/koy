using System.Runtime.InteropServices;
using Koy.Core.Search;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Koy.Platform.Search;

/// <summary>
/// Klasik ve Store uygulamalarını Windows'un Uygulamalar klasöründen okur. Kabuk nesneleri apartman ister; iş bitince
/// biten kısa ömürlü bir STA iş parçacığı kullanılır, boşta kalıcı iş parçacığı yoktur.
/// </summary>
public sealed class InstalledAppsService : IInstalledAppsService
{
    public const string AppsFolder = "shell:AppsFolder";

    public Task<IReadOnlyList<InstalledApp>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<InstalledApp>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(Enumerate(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Koy.InstalledApps"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>Uygulamanın simgesi ve başlatma yolu için kabuk adı.</summary>
    public static string ShellPath(InstalledApp app) => AppsFolder + "\\" + app.Id;

    private static unsafe List<InstalledApp> Enumerate(CancellationToken cancellationToken)
    {
        var apps = new List<InstalledApp>();
        object? folderObject = null;
        object? enumeratorObject = null;
        try
        {
            PInvoke.SHCreateItemFromParsingName(AppsFolder, null, typeof(IShellItem).GUID, out folderObject).ThrowOnFailure();
            var folder = (IShellItem)folderObject;

            var handler = PInvoke.BHID_EnumItems;
            var enumeratorId = typeof(IEnumShellItems).GUID;
            folder.BindToHandler(null, &handler, &enumeratorId, out enumeratorObject);
            var enumerator = (IEnumShellItems)enumeratorObject;

            var item = new IShellItem[1];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                uint fetched = 0;
                enumerator.Next(1, item, &fetched);
                if (fetched == 0)
                    break;

                try
                {
                    var name = DisplayName(item[0], SIGDN.SIGDN_NORMALDISPLAY);
                    var id = DisplayName(item[0], SIGDN.SIGDN_PARENTRELATIVEPARSING);
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                        apps.Add(new InstalledApp(id, name));
                }
                finally
                {
                    Marshal.ReleaseComObject(item[0]);
                }
            }
        }
        finally
        {
            if (enumeratorObject is not null)
                Marshal.ReleaseComObject(enumeratorObject);
            if (folderObject is not null)
                Marshal.ReleaseComObject(folderObject);
        }

        return apps;
    }

    private static unsafe string? DisplayName(IShellItem item, SIGDN kind)
    {
        item.GetDisplayName(kind, out PWSTR value);
        try
        {
            return value.ToString();
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)value.Value);
        }
    }
}
