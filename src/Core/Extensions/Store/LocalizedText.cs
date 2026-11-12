namespace Koy.Core.Extensions.Store;

/// <summary>
/// Dile göre değişen mağaza metni (özet, açıklama). İndekste düz metin (her dilde aynı, "en" sayılır) ya da
/// <c>{ "en": "...", "tr": "..." }</c> nesnesi olarak gelir. <see cref="Resolve"/> önce istenen dili, yoksa
/// <see cref="FallbackLanguage"/> dilini, o da yoksa ilk değeri verir.
/// </summary>
public sealed class LocalizedText
{
    public const string FallbackLanguage = "en";

    public static LocalizedText Empty { get; } = new(new Dictionary<string, string>());

    private readonly IReadOnlyDictionary<string, string> _values;

    public LocalizedText(IReadOnlyDictionary<string, string> values) => _values = values;

    /// <summary>Her dilde aynı olan düz metin.</summary>
    public static LocalizedText Plain(string text) => new(new Dictionary<string, string> { [FallbackLanguage] = text });

    public static implicit operator LocalizedText(string text) => Plain(text);

    /// <summary>Dil kodu → metin (küçük harfli iki harfli kodlar).</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <param name="language">İki harfli ISO dil kodu (ör. <c>CultureInfo.TwoLetterISOLanguageName</c>); büyük/küçük harf duyarsız, null olabilir.</param>
    public string Resolve(string? language)
    {
        if (!string.IsNullOrEmpty(language) && _values.TryGetValue(language.ToLowerInvariant(), out var exact))
        {
            return exact;
        }

        if (_values.TryGetValue(FallbackLanguage, out var fallback))
        {
            return fallback;
        }

        foreach (var value in _values.Values)
        {
            return value;
        }

        return "";
    }
}
