using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Koy.Core.Search;

namespace Koy.Platform.Search;

/// <summary>
/// Firefox yer imlerini (<c>places.sqlite</c>) Windows 11'de yerleşik <c>winsqlite3.dll</c> üzerinden salt okunur okur.
/// Yeni NuGet paketi gerektirmez; DLL yoksa veya Firefox kurulu değilse sessizce boş döner.
/// </summary>
public static class FirefoxBookmarks
{
    private const long MaxFileBytes = 64 * 1024 * 1024;
    private const long MaxWalBytes = 16 * 1024 * 1024;
    private const int MaxBookmarks = 5000;
    private const int SqliteOpenReadOnly = 1;
    private const int SqliteOk = 0;
    private const int SqliteRow = 100;

    private static readonly FileStampCache<IReadOnlyList<BrowserBookmark>> Cache = new();
    private static readonly byte[] QueryUtf8 = Encoding.UTF8.GetBytes(
        "SELECT COALESCE(b.title, p.title, ''), p.url " +
        "FROM moz_bookmarks b JOIN moz_places p ON b.fk = p.id " +
        "WHERE b.type = 1 AND p.url IS NOT NULL AND (p.url LIKE 'http://%' OR p.url LIKE 'https://%') " +
        "LIMIT 5000\0");

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int SqliteOpenV2(byte[] filename, out IntPtr ppDb, int flags, IntPtr zVfs);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int SqlitePrepareV2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int SqliteStep(IntPtr pStmt);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SqliteColumnText(IntPtr pStmt, int iCol);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int SqliteFinalize(IntPtr pStmt);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int SqliteCloseV2(IntPtr db);

    public static IReadOnlyList<BrowserBookmark> Load()
    {
        var profilesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(profilesRoot))
            return [];

        if (!SqliteApi.TryLoad(out var api))
            return [];

        var bookmarks = new List<BrowserBookmark>();
        try
        {
            foreach (var profile in Directory.EnumerateDirectories(profilesRoot))
                bookmarks.AddRange(ReadProfile(profile, api));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return bookmarks;
    }

    private static IReadOnlyList<BrowserBookmark> ReadProfile(string profileDirectory, SqliteApi api)
    {
        var path = Path.Combine(profileDirectory, "places.sqlite");
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                Cache.Remove(path);
                return [];
            }

            return Cache.GetOrLoad(path, info.LastWriteTimeUtc.Ticks, info.Length, () => ReadWithTempCopy(path, api));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static List<BrowserBookmark> ReadWithTempCopy(string sourcePath, SqliteApi api)
    {
        var tempBase = Path.Combine(Path.GetTempPath(), $"koy-ff-{Guid.NewGuid():N}.sqlite");
        var tempWal = tempBase + "-wal";
        var tempShm = tempBase + "-shm";
        try
        {
            CopyShared(sourcePath, tempBase);
            TryCopyCompanion(sourcePath + "-wal", tempWal, MaxWalBytes);
            TryCopyCompanion(sourcePath + "-shm", tempShm, MaxWalBytes);
            return QueryDatabase(tempBase, api);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
        finally
        {
            TryDelete(tempBase);
            TryDelete(tempWal);
            TryDelete(tempShm);
        }
    }

    private static List<BrowserBookmark> QueryDatabase(string dbPath, SqliteApi api)
    {
        var result = new List<BrowserBookmark>();
        var pathBytes = Encoding.UTF8.GetBytes(dbPath + "\0");
        if (api.OpenV2(pathBytes, out var db, SqliteOpenReadOnly, IntPtr.Zero) != SqliteOk || db == IntPtr.Zero)
        {
            if (db != IntPtr.Zero)
                api.CloseV2(db);
            return result;
        }

        try
        {
            if (api.PrepareV2(db, QueryUtf8, -1, out var stmt, IntPtr.Zero) != SqliteOk || stmt == IntPtr.Zero)
                return result;

            try
            {
                while (result.Count < MaxBookmarks && api.Step(stmt) == SqliteRow)
                {
                    var title = Marshal.PtrToStringUTF8(api.ColumnText(stmt, 0)) ?? "";
                    var url = Marshal.PtrToStringUTF8(api.ColumnText(stmt, 1));
                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    result.Add(new BrowserBookmark(string.IsNullOrWhiteSpace(title) ? url : title, url));
                }
            }
            finally
            {
                api.Finalize(stmt);
            }
        }
        finally
        {
            api.CloseV2(db);
        }

        return result;
    }

    private static void CopyShared(string source, string destination)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
    }

    private static void TryCopyCompanion(string source, string destination, long maxBytes)
    {
        try
        {
            var info = new FileInfo(source);
            if (info.Exists && info.Length <= maxBytes)
                CopyShared(source, destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class SqliteApi
    {
        public required SqliteOpenV2 OpenV2 { get; init; }
        public required SqlitePrepareV2 PrepareV2 { get; init; }
        public required SqliteStep Step { get; init; }
        public required SqliteColumnText ColumnText { get; init; }
        public required SqliteFinalize Finalize { get; init; }
        public required SqliteCloseV2 CloseV2 { get; init; }

        public static bool TryLoad(out SqliteApi api)
        {
            api = null!;
            if (!NativeLibrary.TryLoad("winsqlite3.dll", out var handle))
                return false;

            if (!TryGet<SqliteOpenV2>(handle, "sqlite3_open_v2", out var open) ||
                !TryGet<SqlitePrepareV2>(handle, "sqlite3_prepare_v2", out var prepare) ||
                !TryGet<SqliteStep>(handle, "sqlite3_step", out var step) ||
                !TryGet<SqliteColumnText>(handle, "sqlite3_column_text", out var columnText) ||
                !TryGet<SqliteFinalize>(handle, "sqlite3_finalize", out var finalize) ||
                !TryGet<SqliteCloseV2>(handle, "sqlite3_close_v2", out var close))
            {
                return false;
            }

            api = new SqliteApi
            {
                OpenV2 = open,
                PrepareV2 = prepare,
                Step = step,
                ColumnText = columnText,
                Finalize = finalize,
                CloseV2 = close
            };
            return true;
        }

        private static bool TryGet<TDelegate>(IntPtr handle, string name, out TDelegate fn) where TDelegate : Delegate
        {
            if (NativeLibrary.TryGetExport(handle, name, out var address))
            {
                fn = Marshal.GetDelegateForFunctionPointer<TDelegate>(address);
                return true;
            }

            fn = null!;
            return false;
        }
    }
}
