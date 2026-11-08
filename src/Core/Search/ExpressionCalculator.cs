using System.Globalization;
using System.Text;

namespace Koy.Core.Search;

/// <summary>
/// Dört işlem, üs, parantez ve temel işlevleri (sqrt, sin...) hesaplayan küçük ayrıştırıcı. Yazılan metin kod gibi
/// çalıştırılmaz; uzunluk ve iç içe parantez sayısı sınırlıdır.
/// </summary>
public static class ExpressionCalculator
{
    public const int MaxLength = 128;

    private const int MaxDepth = 24;
    private const int MaxExponent = 1024;

    /// <summary>Hesap gibi mi görünüyor: rakam ve işlem, işlev çağrısı ya da 0x. Tek başına "123" hesap değildir.</summary>
    public static bool LooksLikeExpression(string text)
    {
        var body = Body(text);
        if (body.Length == 0 || body.Length > MaxLength || !body.Any(char.IsAsciiDigit))
            return false;

        return body.Skip(1).Any(c => c is '+' or '-' or '*' or '/' or '^' or '×' or '÷' or 'x' or 'X' or '(');
    }

    /// <summary>Sonucu ekran ve pano biçiminde verir. "hex=255", "bin=5", "oct=8" sonucu o tabanda yazar.</summary>
    public static bool TryCalculate(string text, CultureInfo culture, out string display, out string copy)
    {
        display = copy = "";
        var body = text.Trim();
        var radix = 0;
        foreach (var (prefix, value) in new[] { ("hex=", 16), ("bin=", 2), ("oct=", 8) })
        {
            if (body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                radix = value;
                body = "=" + body[prefix.Length..];
                break;
            }
        }

        // Taban öneki varsa tek sayı da hesaptır ("hex=255").
        if (!(radix != 0 ? TryParseBody(body, culture, out var result) : TryEvaluate(body, culture, out result)))
            return false;

        if (radix != 0)
        {
            if (result != decimal.Truncate(result) || result < 0 || result > long.MaxValue)
                return false;

            var digits = Convert.ToString((long)result, radix);
            display = copy = radix switch
            {
                16 => "0x" + digits.ToUpperInvariant(),
                2 => "0b" + digits,
                _ => "0" + digits
            };
            return true;
        }

        display = FormatForDisplay(result, culture);
        copy = FormatForCopy(result, culture);
        return true;
    }

    public static bool TryEvaluate(string text, CultureInfo culture, out decimal result)
    {
        result = 0;
        return LooksLikeExpression(text) && TryParseBody(text, culture, out result);
    }

    private static bool TryParseBody(string text, CultureInfo culture, out decimal result)
    {
        result = 0;
        var body = Body(text);
        if (body.Length == 0 || body.Length > MaxLength)
            return false;

        try
        {
            return new Parser(body, culture).TryParse(out result);
        }
        catch (Exception e) when (e is OverflowException or DivideByZeroException)
        {
            return false;
        }
    }

    /// <summary>Ekranda gösterilen sonuç (binlik ayırıcılı).</summary>
    public static string FormatForDisplay(decimal value, CultureInfo culture) => value.ToString("#,0.##########", culture);

    /// <summary>Panoya kopyalanan sonuç (ayırıcısız, başka yere yapıştırılabilir).</summary>
    public static string FormatForCopy(decimal value, CultureInfo culture) => value.ToString("0.##########", culture);

    private static string Body(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith('='))
            trimmed = trimmed[1..].Trim();
        return trimmed.EndsWith('=') ? trimmed[..^1].Trim() : trimmed;
    }

    private sealed class Parser(string text, CultureInfo culture)
    {
        private readonly string _groupSeparator = culture.NumberFormat.NumberGroupSeparator;
        private int _position;
        private int _depth;

        public bool TryParse(out decimal value)
        {
            if (!TryExpression(out value))
                return false;

            SkipSpaces();
            return _position == text.Length;
        }

        private bool TryExpression(out decimal value)
        {
            if (!TryTerm(out value))
                return false;

            while (true)
            {
                var op = Peek();
                if (op is not ('+' or '-'))
                    return true;

                _position++;
                if (!TryTerm(out var right))
                    return false;

                value = checked(op == '+' ? value + right : value - right);
            }
        }

        private bool TryTerm(out decimal value)
        {
            if (!TryUnary(out value))
                return false;

            while (true)
            {
                var op = Peek();
                if (op is not ('*' or '/' or '×' or '÷' or 'x' or 'X'))
                    return true;

                _position++;
                if (!TryUnary(out var right))
                    return false;

                if (op is '/' or '÷')
                {
                    if (right == 0)
                        return false;
                    value /= right;
                }
                else
                {
                    value = checked(value * right);
                }
            }
        }

        private bool TryUnary(out decimal value)
        {
            var op = Peek();
            if (op is not ('-' or '+'))
                return TryPower(out value);

            if (!Enter())
            {
                value = 0;
                return false;
            }

            _position++;
            var ok = TryUnary(out value);
            _depth--;
            if (ok && op == '-')
                value = -value;
            return ok;
        }

        private bool TryPower(out decimal value)
        {
            if (!TryPrimary(out value))
                return false;

            if (Peek() != '^')
                return true;

            _position++;
            if (!Enter())
                return false;

            var ok = TryUnary(out var exponent);
            _depth--;
            return ok && TryRaise(value, exponent, out value);
        }

        private static bool TryRaise(decimal value, decimal exponent, out decimal result)
        {
            result = 0;
            if (exponent != decimal.Truncate(exponent))
            {
                var power = Math.Pow((double)value, (double)exponent);
                if (double.IsNaN(power) || double.IsInfinity(power))
                    return false;

                result = (decimal)power;
                return true;
            }

            if (Math.Abs(exponent) > MaxExponent)
                return false;

            result = 1;
            for (var i = 0; i < Math.Abs(exponent); i++)
                result = checked(result * value);

            if (exponent >= 0)
                return true;

            if (result == 0)
                return false;

            result = 1 / result;
            return true;
        }

        private bool TryPrimary(out decimal value)
        {
            value = 0;
            var next = Peek();
            if (char.IsAsciiLetter(next))
                return TryFunction(out value);

            if (next != '(')
                return TryNumber(out value);

            if (!Enter())
                return false;

            _position++;
            var ok = TryExpression(out value);
            _depth--;
            if (!ok || Peek() != ')')
                return false;

            _position++;
            return true;
        }

        private bool TryFunction(out decimal value)
        {
            value = 0;
            var start = _position;
            while (_position < text.Length && char.IsAsciiLetterOrDigit(text[_position]))
                _position++;

            var name = text[start.._position].ToLowerInvariant();
            if (name == "pi")
            {
                value = (decimal)Math.PI;
                return true;
            }

            if (name == "e")
            {
                value = (decimal)Math.E;
                return true;
            }

            if (Peek() != '(' || !Enter())
                return false;

            _position++;
            var ok = TryExpression(out var argument);
            _depth--;
            if (!ok || Peek() != ')')
                return false;

            _position++;
            return TryApply(name, argument, out value);
        }

        private static bool TryApply(string name, decimal argument, out decimal value)
        {
            value = 0;
            var x = (double)argument;
            double? result = name switch
            {
                "sqrt" => Math.Sqrt(x),
                "abs" => Math.Abs(x),
                "sin" => Math.Sin(x),
                "cos" => Math.Cos(x),
                "tan" => Math.Tan(x),
                "ln" => Math.Log(x),
                "log" => Math.Log10(x),
                "exp" => Math.Exp(x),
                "floor" => Math.Floor(x),
                "ceil" => Math.Ceiling(x),
                "round" => Math.Round(x),
                _ => null
            };

            if (result is not { } number || double.IsNaN(number) || double.IsInfinity(number))
                return false;

            value = (decimal)number;
            return true;
        }

        private bool TryHexNumber(out decimal value)
        {
            value = 0;
            var start = _position + 2;
            var end = start;
            while (end < text.Length && char.IsAsciiHexDigit(text[end]))
                end++;

            if (end == start || !long.TryParse(text[start..end], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number))
                return false;

            value = number;
            _position = end;
            return true;
        }

        private bool TryNumber(out decimal value)
        {
            value = 0;
            SkipSpaces();

            if (_position + 2 < text.Length && text[_position] == '0' && text[_position + 1] is 'x' or 'X' && char.IsAsciiHexDigit(text[_position + 2]))
                return TryHexNumber(out value);

            var digits = new StringBuilder();
            var hasDecimal = false;
            while (_position < text.Length)
            {
                var c = text[_position];
                if (char.IsAsciiDigit(c))
                {
                    digits.Append(c);
                    _position++;
                    continue;
                }

                if (c is not ('.' or ',') || !IsDigitAt(_position + 1))
                    break;

                // Yerel binlik ayırıcı ancak ardından tam üç rakam gelirse binliktir: Türkçede "1.000" bin, "3.5" üç buçuk.
                _position++;
                if (!hasDecimal && digits.Length > 0 && IsGroupSeparator(c) && IsGroupOfThree(_position))
                    continue;

                if (hasDecimal)
                    return false;

                hasDecimal = true;
                digits.Append('.');
            }

            return digits.Length > 0 && decimal.TryParse(digits.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
        }

        private bool IsGroupSeparator(char c) => _groupSeparator.Length == 1 && _groupSeparator[0] == c;

        private bool IsGroupOfThree(int start) =>
            IsDigitAt(start) && IsDigitAt(start + 1) && IsDigitAt(start + 2) && !IsDigitAt(start + 3);

        private bool IsDigitAt(int index) => index < text.Length && char.IsAsciiDigit(text[index]);

        private bool Enter() => ++_depth <= MaxDepth;

        private char Peek()
        {
            SkipSpaces();
            return _position < text.Length ? text[_position] : '\0';
        }

        private void SkipSpaces()
        {
            while (_position < text.Length && text[_position] == ' ')
                _position++;
        }
    }
}
