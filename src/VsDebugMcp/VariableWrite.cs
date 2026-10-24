using System.Globalization;

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

    public static DebugDomainException ReadOnly(string frameId, string path, string why) => new(
        ErrorCodes.ReadOnlyTarget, $"{path} in frame {frameId} is read-only ({why}); nothing was written",
        "write a field or local instead; constants, get-only properties and optimized-away locals cannot change");

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
            return nullable || t is "string" or "System.String"
                ? "null"
                : throw Mismatch(type, value, "a non-null value (the target is not nullable)");
        const NumberStyles integer = NumberStyles.AllowLeadingSign;
        const NumberStyles real = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
        var inv = CultureInfo.InvariantCulture;
        return t switch
        {
            "bool" or "System.Boolean" => raw switch
            {
                "true" or "True" => "true",
                "false" or "False" => "false",
                _ => throw Mismatch(type, value, "true or false"),
            },
            "int" or "System.Int32" => int.TryParse(raw, integer, inv, out var i) ? i.ToString(inv) : throw Mismatch(type, value, "an int in range"),
            "long" or "System.Int64" => long.TryParse(raw, integer, inv, out var l) ? l.ToString(inv) + "L" : throw Mismatch(type, value, "a long in range"),
            "short" or "System.Int16" => short.TryParse(raw, integer, inv, out var s) ? $"(short){s.ToString(inv)}" : throw Mismatch(type, value, "a short in range"),
            "byte" or "System.Byte" => byte.TryParse(raw, NumberStyles.None, inv, out var b) ? $"(byte){b.ToString(inv)}" : throw Mismatch(type, value, "a byte 0-255"),
            "sbyte" or "System.SByte" => sbyte.TryParse(raw, integer, inv, out var sb) ? $"(sbyte){sb.ToString(inv)}" : throw Mismatch(type, value, "an sbyte in range"),
            "uint" or "System.UInt32" => uint.TryParse(raw, NumberStyles.None, inv, out var ui) ? ui.ToString(inv) + "U" : throw Mismatch(type, value, "a uint in range"),
            "ulong" or "System.UInt64" => ulong.TryParse(raw, NumberStyles.None, inv, out var ul) ? ul.ToString(inv) + "UL" : throw Mismatch(type, value, "a ulong in range"),
            "ushort" or "System.UInt16" => ushort.TryParse(raw, NumberStyles.None, inv, out var us) ? $"(ushort){us.ToString(inv)}" : throw Mismatch(type, value, "a ushort in range"),
            "double" or "System.Double" => double.TryParse(raw, real, inv, out var d) && double.IsFinite(d) ? d.ToString("R", inv) + "D" : throw Mismatch(type, value, "a finite double"),
            "float" or "System.Single" => float.TryParse(raw, real, inv, out var f) && float.IsFinite(f) ? f.ToString("R", inv) + "F" : throw Mismatch(type, value, "a finite float"),
            "decimal" or "System.Decimal" => decimal.TryParse(raw, real & ~NumberStyles.AllowExponent, inv, out var m) ? m.ToString(inv) + "M" : throw Mismatch(type, value, "a decimal in range"),
            "char" or "System.Char" => CharLiteral(raw) ?? throw Mismatch(type, value, "a single-character literal like 'a' (or 97 'a' as shown)"),
            "string" or "System.String" => IsStringLiteral(raw) ? raw : throw Mismatch(type, value, "a double-quoted string literal or null"),
            _ => throw Mismatch(type, value, "a supported type (bool, char, string, integral, float, double or decimal, or null for a nullable)"),
        };
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
        var space = raw.LastIndexOf(' ');
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
