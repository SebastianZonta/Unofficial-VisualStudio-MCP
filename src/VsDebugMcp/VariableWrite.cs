using System.Globalization;
using System.Numerics;

namespace VsDebugMcp;

/// <summary>Shared set_variable rules for both bridges. Pure functions: consent gate, strict
/// coercion of the agent's value to an evaluator literal, and the structured errors.</summary>
internal static class VariableWrite
{
    /// <summary>Fail-closed: checked before any debugger state is touched. Never reads attach consent.</summary>
    public static void RequireConsent(bool consent, string frameId, string path)
    {
        if (!consent)
            throw new DebugDomainException(
                ErrorCodes.ConsentRequired, $"writing {path} in frame {frameId} needs explicit consent",
                "re-call set_variable with consent: true; every write re-states consent");
    }

    public static DebugDomainException PathNotFound(string frameId, string path) => new(
        ErrorCodes.PathNotFound, $"no variable {path} in frame {frameId}",
        "re-read the frame with read_variables and pass a listed name or path");

    public static DebugDomainException ReadOnly(string frameId, string path, string why, Exception? inner = null) => new(
        ErrorCodes.ReadOnlyTarget, $"{path} in frame {frameId} is read-only ({why}); nothing was written",
        "write a field or local instead; constants, get-only properties and optimized-away locals cannot change", inner);

    private static DebugDomainException Mismatch(string type, string value, string expected) => new(
        ErrorCodes.TypeMismatch, $"expected {expected} for type {type}, got {value}; nothing was written",
        "pass the value as read_variables shows it for that type");

    /// <summary>Strict, culture-invariant conversion to a C# literal for <paramref name="type"/>
    /// as the evaluator reports it. Out-of-range, lossy or unsupported targets are rejected, never truncated.</summary>
    public static string Coerce(string type, string value)
    {
        var raw = value.Trim();
        var t = type.Trim();
        var nullable = t.EndsWith('?');
        if (nullable)
            t = t[..^1];
        if (raw == "null")
            return nullable || t is "string" or "System.String" ? "null" : throw Mismatch(type, value, "a non-null value (the target is not nullable)");
        return _coercers.TryGetValue(t, out var coerce)
            ? coerce(raw, type, value)
            : throw Mismatch(type, value, "a supported type (bool, char, string, integral, float, double or decimal, or null for a nullable)");
    }

    private const NumberStyles IntegerStyle = NumberStyles.AllowLeadingSign;
    private const NumberStyles RealStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
    private const NumberStyles DecimalStyle = RealStyle & ~NumberStyles.AllowExponent;
    private const NumberStyles UnsignedStyle = NumberStyles.None;

    private static readonly Dictionary<string, Func<string, string, string, string>> _coercers = new(StringComparer.Ordinal)
    {
        ["bool"] = CoerceBool,
        ["System.Boolean"] = CoerceBool,
        ["int"] = CoerceInt,
        ["System.Int32"] = CoerceInt,
        ["long"] = CoerceLong,
        ["System.Int64"] = CoerceLong,
        ["short"] = CoerceShort,
        ["System.Int16"] = CoerceShort,
        ["byte"] = CoerceByte,
        ["System.Byte"] = CoerceByte,
        ["sbyte"] = CoerceSByte,
        ["System.SByte"] = CoerceSByte,
        ["uint"] = CoerceUInt,
        ["System.UInt32"] = CoerceUInt,
        ["ulong"] = CoerceULong,
        ["System.UInt64"] = CoerceULong,
        ["ushort"] = CoerceUShort,
        ["System.UInt16"] = CoerceUShort,
        ["double"] = CoerceDouble,
        ["System.Double"] = CoerceDouble,
        ["float"] = CoerceFloat,
        ["System.Single"] = CoerceFloat,
        ["decimal"] = CoerceDecimal,
        ["System.Decimal"] = CoerceDecimal,
        ["char"] = CoerceChar,
        ["System.Char"] = CoerceChar,
        ["string"] = CoerceString,
        ["System.String"] = CoerceString,
    };

    private static string CoerceBool(string raw, string type, string value) => raw switch
    {
        "true" or "True" => "true",
        "false" or "False" => "false",
        _ => throw Mismatch(type, value, "true or false"),
    };

    private static string CoerceInt(string raw, string type, string value) =>
        CoerceNumber<int>(raw, type, value, IntegerStyle, static v => v.ToString(CultureInfo.InvariantCulture), "an int in range");

    private static string CoerceLong(string raw, string type, string value) =>
        CoerceNumber<long>(raw, type, value, IntegerStyle, static v => v.ToString(CultureInfo.InvariantCulture) + "L", "a long in range");

    private static string CoerceShort(string raw, string type, string value) =>
        CoerceNumber<short>(raw, type, value, IntegerStyle, static v => "(short)" + v.ToString(CultureInfo.InvariantCulture), "a short in range");

    private static string CoerceByte(string raw, string type, string value) =>
        CoerceNumber<byte>(raw, type, value, UnsignedStyle, static v => "(byte)" + v.ToString(CultureInfo.InvariantCulture), "a byte 0-255");

    private static string CoerceSByte(string raw, string type, string value) =>
        CoerceNumber<sbyte>(raw, type, value, IntegerStyle, static v => "(sbyte)" + v.ToString(CultureInfo.InvariantCulture), "an sbyte in range");

    private static string CoerceUInt(string raw, string type, string value) =>
        CoerceNumber<uint>(raw, type, value, UnsignedStyle, static v => v.ToString(CultureInfo.InvariantCulture) + "U", "a uint in range");

    private static string CoerceULong(string raw, string type, string value) =>
        CoerceNumber<ulong>(raw, type, value, UnsignedStyle, static v => v.ToString(CultureInfo.InvariantCulture) + "UL", "a ulong in range");

    private static string CoerceUShort(string raw, string type, string value) =>
        CoerceNumber<ushort>(raw, type, value, UnsignedStyle, static v => "(ushort)" + v.ToString(CultureInfo.InvariantCulture), "a ushort in range");

    private static string CoerceDouble(string raw, string type, string value) =>
        CoerceNumber<double>(raw, type, value, RealStyle, static v => v.ToString("R", CultureInfo.InvariantCulture) + "D", static v => double.IsFinite(v), "a finite double");

    private static string CoerceFloat(string raw, string type, string value) =>
        CoerceNumber<float>(raw, type, value, RealStyle, static v => v.ToString("R", CultureInfo.InvariantCulture) + "F", static v => float.IsFinite(v), "a finite float");

    private static string CoerceDecimal(string raw, string type, string value) =>
        CoerceNumber<decimal>(raw, type, value, DecimalStyle, static v => v.ToString(CultureInfo.InvariantCulture) + "M", "a decimal in range");

    private static string CoerceChar(string raw, string type, string value) =>
        CharLiteral(raw) ?? throw Mismatch(type, value, "a single-character literal like 'a' (or 97 'a' as shown)");

    private static string CoerceString(string raw, string type, string value) =>
        IsStringLiteral(raw) ? raw : throw Mismatch(type, value, "a double-quoted string literal or null");

    private static string CoerceNumber<T>(string raw, string type, string value, NumberStyles style, Func<T, string> format, string expected)
        where T : INumberBase<T> =>
        CoerceNumber(raw, type, value, style, format, static _ => true, expected);

    private static string CoerceNumber<T>(string raw, string type, string value, NumberStyles style, Func<T, string> format, Func<T, bool> ok, string expected)
        where T : INumberBase<T>
    {
        var inv = CultureInfo.InvariantCulture;
        return T.TryParse(raw, style, inv, out var parsed) && ok(parsed) ? format(parsed) : throw Mismatch(type, value, expected);
    }

    /// <summary>A coerced literal as VS displays it: narrowing cast and type suffix dropped ("41.80M" to "41.80").</summary>
    public static string Shown(string literal)
    {
        if (literal.StartsWith('(') && literal.IndexOf(')') is var close and > 0)
            return literal[(close + 1)..];
        return literal.Length > 1 && char.IsDigit(literal[0] == '-' ? literal[1] : literal[0])
            ? literal.TrimEnd('L', 'U', 'D', 'F', 'M')
            : literal;
    }

    /// <summary>True when the re-read already showed the coerced value (41.8 vs 41.80, 'a' vs 97 'a'),
    /// so an unchanged re-read is not mistaken for a refused write.</summary>
    public static bool IsSameShownValue(string? shown, string literal)
    {
        if (shown is null)
            return false;
        var want = Shown(literal);
        var have = shown.Trim();
        var inv = CultureInfo.InvariantCulture;
        const NumberStyles real = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
        return decimal.TryParse(have, real, inv, out var a) && decimal.TryParse(want, real, inv, out var b)
            ? a == b
            : have == want || (want.StartsWith('\'') && have.EndsWith(" " + want, StringComparison.Ordinal));
    }

    /// <summary>'a', or VS's char display 97 'a' (code must match the glyph); escapes are not accepted.</summary>
    private static string? CharLiteral(string raw)
    {
        // Split at the first separator: the space glyph itself (' ') contains a space,
        // so LastIndexOf would split inside the quotes and reject a valid round-trip.
        var space = raw.IndexOf(' ');
        uint code = 0;
        var shownForm = space > 0 && uint.TryParse(raw[..space], NumberStyles.None, CultureInfo.InvariantCulture, out code);
        var quoted = shownForm ? raw[(space + 1)..] : raw;
        if (quoted is not ['\'', not '\'' and not '\\', '\''])
            return null;
        return shownForm && code != quoted[1] ? null : quoted;
    }

    /// <summary>A well-formed regular literal: quotes at both ends, no bare inner quote or newline.</summary>
    private static bool IsStringLiteral(string raw)
    {
        if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"')
            return false;
        for (var i = 1; i < raw.Length - 1; i++)
        {
            if (raw[i] is '\r' or '\n' or '"')
                return false;
            if (raw[i] == '\\' && ++i >= raw.Length - 1)
                return false;
        }
        return true;
    }
}