using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Koy.Core.Search;

namespace Koy.Platform.Search;

/// <summary>
/// Windows yerleşik <c>ISpellCheckerFactory</c> COM arayüzüyle yazım denetimi yapar.
/// Panel kapalıyken bellekte COM nesnesi tutmaz ve boşta sıfır CPU kullanır.
/// </summary>
public sealed class WindowsSpellCheckerService : ISpellCheckerService
{
    private const int MaxSuggestions = 8;
    private static readonly Guid FactoryClsid = new("7AB36653-1796-484B-BDFA-E74F1DB7C1DC");

    private readonly object _gate = new();
    private ISpellCheckerFactory? _factory;
    private ISpellChecker? _primary;
    private ISpellChecker? _fallbackEn;
    private bool _initialized;

    public SpellCheckResult Check(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new SpellCheckResult(true, true, []);

        lock (_gate)
        {
            EnsureInitializedLocked();
            if (_primary is null)
                return new SpellCheckResult(false, true, []);

            var primaryResult = CheckWith(_primary, text);
            if (primaryResult.IsCorrect)
                return primaryResult;

            if (_fallbackEn is not null)
            {
                var enResult = CheckWith(_fallbackEn, text);
                if (enResult.IsCorrect)
                    return enResult;

                if (primaryResult.Suggestions.Count == 0 && enResult.Suggestions.Count > 0)
                    return enResult;
            }

            return primaryResult;
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            ReleaseCom(ref _primary);
            ReleaseCom(ref _fallbackEn);
            ReleaseCom(ref _factory);
            _initialized = false;
        }
    }

    private void EnsureInitializedLocked()
    {
        if (_initialized)
            return;

        _initialized = true;
        try
        {
            var type = Type.GetTypeFromCLSID(FactoryClsid, throwOnError: false);
            if (type is null)
                return;

            _factory = Activator.CreateInstance(type) as ISpellCheckerFactory;
            if (_factory is null)
                return;

            var uiTag = CultureInfo.CurrentUICulture.Name;
            _primary = TryCreateChecker(_factory, uiTag) ?? TryCreateChecker(_factory, "en-US");
            if (_primary is not null && !string.Equals(uiTag, "en-US", StringComparison.OrdinalIgnoreCase))
                _fallbackEn = TryCreateChecker(_factory, "en-US");
        }
        catch (COMException)
        {
            ReleaseCom(ref _primary);
            ReleaseCom(ref _fallbackEn);
            ReleaseCom(ref _factory);
        }
    }

    private static ISpellChecker? TryCreateChecker(ISpellCheckerFactory factory, string languageTag)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(languageTag))
                return null;
            if (factory.IsSupported(languageTag, out var supported) == 0 && supported != 0)
            {
                if (factory.CreateSpellChecker(languageTag, out var checker) == 0)
                    return checker;
            }
        }
        catch (COMException)
        {
        }

        return null;
    }

    private static SpellCheckResult CheckWith(ISpellChecker checker, string text)
    {
        try
        {
            if (checker.Check(text, out var errors) != 0 || errors is null)
                return new SpellCheckResult(true, true, []);

            try
            {
                var suggestions = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasError = false;

                while (errors.Next(out var error) == 0 && error is not null)
                {
                    hasError = true;
                    try
                    {
                        error.get_StartIndex(out var start);
                        error.get_Length(out var length);
                        error.get_CorrectiveAction(out var action);
                        error.get_Replacement(out var replacement);

                        if (action == CorrectiveAction.Replace && !string.IsNullOrWhiteSpace(replacement))
                        {
                            var replaced = ApplyReplacement(text, (int)start, (int)length, replacement);
                            if (seen.Add(replaced))
                                suggestions.Add(replaced);
                        }
                        else if (action == CorrectiveAction.GetSuggestions && start + length <= text.Length)
                        {
                            var word = text.Substring((int)start, (int)length);
                            CollectSuggestions(checker, word, text, (int)start, (int)length, suggestions, seen);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(error);
                    }

                    if (suggestions.Count >= MaxSuggestions)
                        break;
                }

                return new SpellCheckResult(true, !hasError, suggestions);
            }
            finally
            {
                Marshal.ReleaseComObject(errors);
            }
        }
        catch (COMException)
        {
            return new SpellCheckResult(false, true, []);
        }
    }

    private static void CollectSuggestions(
        ISpellChecker checker,
        string word,
        string fullText,
        int start,
        int length,
        List<string> suggestions,
        HashSet<string> seen)
    {
        if (checker.Suggest(word, out var enumString) != 0 || enumString is null)
            return;

        try
        {
            var buffer = new string[MaxSuggestions];
            var fetchedPtr = Marshal.AllocCoTaskMem(sizeof(int));
            try
            {
                while (suggestions.Count < MaxSuggestions)
                {
                    var hr = enumString.Next(buffer.Length, buffer, fetchedPtr);
                    var count = Marshal.ReadInt32(fetchedPtr);
                    if (count <= 0)
                        break;

                    for (var i = 0; i < count && suggestions.Count < MaxSuggestions; i++)
                    {
                        var candidate = ApplyReplacement(fullText, start, length, buffer[i]);
                        if (seen.Add(candidate))
                            suggestions.Add(candidate);
                    }

                    if (hr != 0)
                        break;
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(fetchedPtr);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumString);
        }
    }

    private static string ApplyReplacement(string fullText, int start, int length, string replacement)
    {
        if (start == 0 && length == fullText.Length)
            return replacement;
        if (start < 0 || start + length > fullText.Length)
            return replacement;
        return string.Concat(fullText.AsSpan(0, start), replacement, fullText.AsSpan(start + length));
    }

    private static void ReleaseCom<T>(ref T? instance) where T : class
    {
        if (instance is not null)
        {
            try
            {
                Marshal.ReleaseComObject(instance);
            }
            catch (ArgumentException)
            {
            }

            instance = null;
        }
    }

    private enum CorrectiveAction
    {
        None = 0,
        GetSuggestions = 1,
        Replace = 2,
        Delete = 3
    }

    [ComImport]
    [Guid("8E018A9D-2415-4677-BF08-794EA61F94BB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellCheckerFactory
    {
        [PreserveSig]
        int get_SupportedLanguages(out IEnumString value);

        [PreserveSig]
        int IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag, out int value);

        [PreserveSig]
        int CreateSpellChecker([MarshalAs(UnmanagedType.LPWStr)] string languageTag, out ISpellChecker value);
    }

    [ComImport]
    [Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellChecker
    {
        [PreserveSig]
        int get_LanguageTag([MarshalAs(UnmanagedType.LPWStr)] out string value);

        [PreserveSig]
        int Check([MarshalAs(UnmanagedType.LPWStr)] string text, out IEnumSpellingError value);

        [PreserveSig]
        int Suggest([MarshalAs(UnmanagedType.LPWStr)] string word, out IEnumString value);
    }

    [ComImport]
    [Guid("803E3BD4-2828-4410-8290-418D1D73C762")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumSpellingError
    {
        [PreserveSig]
        int Next(out ISpellingError value);
    }

    [ComImport]
    [Guid("B7C82D61-FBE8-4B47-9B27-6C0D2E0DE0A3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellingError
    {
        [PreserveSig]
        int get_StartIndex(out uint value);

        [PreserveSig]
        int get_Length(out uint value);

        [PreserveSig]
        int get_CorrectiveAction(out CorrectiveAction value);

        [PreserveSig]
        int get_Replacement([MarshalAs(UnmanagedType.LPWStr)] out string value);
    }
}
