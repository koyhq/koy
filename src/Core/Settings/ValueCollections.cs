namespace Koy.Core.Settings;

/// <summary>
/// İçeriğe göre eşitlik gösteren liste. Ayar bölümleri record olduğundan eşitlikleri alanlarının eşitliğinden gelir;
/// düz <see cref="List{T}"/> referansla karşılaştırılırdı. JSON'da dizi olarak okunur/yazılır (List'ten türediği için).
/// Sıra anlamlıdır.
/// </summary>
public sealed class ValueList<T> : List<T>, IEquatable<ValueList<T>>
{
    public ValueList() { }

    public ValueList(IEnumerable<T> items) : base(items) { }

    public bool Equals(ValueList<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return this.SequenceEqual(other);
    }

    public override bool Equals(object? obj) => Equals(obj as ValueList<T>);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in this)
            hash.Add(item);
        return hash.ToHashCode();
    }
}

/// <summary>
/// İçeriğe göre eşitlik gösteren, anahtarları büyük/küçük harf duyarsız sözlük (monitör kimlikleri gibi).
/// Parametresiz yapıcı karşılaştırıcıyı kurar; JSON okuyucusu da bunu kullanır, bu yüzden okunan sözlükte de duyarsızdır.
/// Sıradan bağımsız eşitlik: aynı kayıtlar farklı sırada yazılsa da eşittir.
/// </summary>
public sealed class ValueDictionary<TValue> : Dictionary<string, TValue>, IEquatable<ValueDictionary<TValue>>
{
    public ValueDictionary() : base(StringComparer.OrdinalIgnoreCase) { }

    public ValueDictionary(IEnumerable<KeyValuePair<string, TValue>> items) : base(StringComparer.OrdinalIgnoreCase)
    {
        foreach (var (key, value) in items)
            this[key] = value;
    }

    public bool Equals(ValueDictionary<TValue>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Count != other.Count) return false;

        foreach (var (key, value) in this)
        {
            if (!other.TryGetValue(key, out var otherValue) || !EqualityComparer<TValue>.Default.Equals(value, otherValue))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ValueDictionary<TValue>);

    public override int GetHashCode()
    {
        var sum = 0;
        foreach (var (key, value) in this)
            sum += HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key), value);
        return sum;
    }
}
