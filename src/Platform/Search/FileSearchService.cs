using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Koy.Core.Diagnostics;
using Koy.Core.Search;

namespace Koy.Platform.Search;

/// <summary>
/// Windows Arama dizininde dosya adı arar. Yeni paket gerektirmesin diye dizine Windows'un kendi ADO COM nesnesiyle
/// bağlanılır. Dizin kapalıysa boş sonuç döner.
/// </summary>
public sealed class FileSearchService : IFileSearchService
{
    private const string ConnectionString = "Provider=Search.CollatorDSO;Extended Properties='Application=Windows';";
    private const string DirectoryItemType = "Directory";

    public Task<IReadOnlyList<IndexedItem>> SearchAsync(string term, int limit, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<IndexedItem>>(() => Query(term, limit, cancellationToken), cancellationToken);

    /// <summary>Kullanıcının yazdığı metni dizin sözdiziminden arındırır; SQL'e yalnız harf, rakam ve güvenli işaretler girer.</summary>
    public static string BuildSql(string term, int limit)
    {
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Sanitize).Where(w => w.Length > 0).ToList();
        if (words.Count == 0)
            return "";

        var conditions = string.Join(" AND ", words.Select(w => $"CONTAINS(System.FileName, '\"{w}*\"')"));
        return $"SELECT TOP {limit} System.ItemPathDisplay, System.ItemType FROM SYSTEMINDEX " +
               $"WHERE SCOPE='file:' AND {conditions} ORDER BY System.Search.Rank DESC";
    }

    private static string Sanitize(string word)
    {
        var sb = new StringBuilder(word.Length);
        foreach (var c in word)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
                sb.Append(c);
        }

        return sb.ToString();
    }

    private static List<IndexedItem> Query(string term, int limit, CancellationToken cancellationToken)
    {
        var sql = BuildSql(term, limit);
        var connectionType = Type.GetTypeFromProgID("ADODB.Connection");
        if (sql.Length == 0 || connectionType is null)
            return [];

        var items = new List<IndexedItem>();
        object? connection = null;
        object? records = null;
        object? fields = null;
        try
        {
            connection = Activator.CreateInstance(connectionType);
            if (connection is null)
                return [];

            Call(connection, "Open", ConnectionString);
            records = Call(connection, "Execute", sql);
            if (records is null)
                return [];

            fields = Get(records, "Fields");
            while (Get(records, "EOF") is false)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Value(fields!, 0) is string { Length: > 0 } path)
                    items.Add(new IndexedItem(path, string.Equals(Value(fields!, 1) as string, DirectoryItemType, StringComparison.OrdinalIgnoreCase)));

                Call(records, "MoveNext");
            }
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or InvalidCastException)
        {
            // Dizin servisi kapalı ya da sorgu reddedildi: arama sonuçsuz kalır, diğer kaynaklar çalışır.
            CrashLog.Write("Dosya dizini", ex);
            return [];
        }
        finally
        {
            Release(fields);
            Release(records);
            Release(connection);
        }

        return items;
    }

    private static object? Value(object fields, int index)
    {
        var field = fields.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, fields, [index], System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            var value = field?.GetType().InvokeMember("Value", BindingFlags.GetProperty, null, field, null, System.Globalization.CultureInfo.InvariantCulture);
            return value is DBNull ? null : value;
        }
        finally
        {
            Release(field);
        }
    }

    private static object? Get(object target, string property) =>
        target.GetType().InvokeMember(property, BindingFlags.GetProperty, null, target, null, System.Globalization.CultureInfo.InvariantCulture);

    private static object? Call(object target, string method, params object[] arguments) =>
        target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, arguments, System.Globalization.CultureInfo.InvariantCulture);

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.ReleaseComObject(value);
    }
}
