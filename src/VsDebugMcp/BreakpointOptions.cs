namespace VsDebugMcp;

/// <summary>Shared condition/hit-count/logpoint rules: validation plus log-template
/// parsing and rendering. Pure; each layer throws its own exception type.</summary>
internal static class BreakpointOptions
{
    /// <summary>Error naming the offending input, or null when valid.</summary>
    public static (string Detail, string Remediation)? Check(string? condition, int? hitCount, string? hitCountMode, string? logMessage)
    {
        var cond = string.IsNullOrWhiteSpace(condition) ? null : condition;
        var hasLog = !string.IsNullOrWhiteSpace(logMessage);
        if (logMessage is not null && !hasLog)
            return ("logMessage must not be empty", "pass a message template with optional {expression} holes");
        if (hasLog && (cond is not null || hitCount is not null || !string.IsNullOrWhiteSpace(hitCountMode)))
            return ("logMessage is incompatible with condition/hitCount", "set a logpoint or a stopping breakpoint, not both");
        if (cond is not null)
        {
            var reason = CheckExpression(cond);
            if (reason is not null)
                return ($"condition {reason.Value.Detail}", reason.Value.Remediation);
        }
        if (hitCount is not null && hitCount < 1)
            return ("hitCount must be >= 1", "pass a positive hit count");
        if (!string.IsNullOrWhiteSpace(hitCountMode) && hitCountMode is not (HitCountModes.Exact or HitCountModes.Every))
            return ("hitCountMode must be exact or every", "pass one of the two modes");
        if (hitCount is null && !string.IsNullOrWhiteSpace(hitCountMode))
            return ("hitCountMode needs hitCount", "pass hitCount together with the mode");
        if (hasLog && !BracesBalanced(logMessage!))
            return ("logMessage has unbalanced braces", "pair every { with a } around a read-only expression");
        if (hasLog)
        {
            foreach (var hole in Holes(logMessage!))
            {
                if (string.IsNullOrWhiteSpace(hole))
                    return ("logMessage has an empty {expression} hole", "put a read-only expression inside every pair of braces");
                var reason = CheckExpression(hole);
                if (reason is not null)
                    return ($"logMessage expression {reason.Value.Detail}", reason.Value.Remediation);
            }
        }
        return null;
    }

    /// <summary>Normalized storage: blank condition becomes null; a bare hitCount defaults to exact.</summary>
    public static (string? Condition, int? HitCount, string? HitCountMode, string? LogMessage) Normalize(
        string? condition, int? hitCount, string? hitCountMode, string? logMessage)
    {
        var cond = string.IsNullOrWhiteSpace(condition) ? null : condition.Trim();
        var mode = hitCount is null ? null
            : string.IsNullOrWhiteSpace(hitCountMode) ? HitCountModes.Exact : hitCountMode;
        return (cond, hitCount, mode, logMessage);
    }

    /// <summary>Same read-only rules as evaluate: no statements, single line, no assignment,
    /// increment or decrement.
    /// Deeper parse errors (e.g. a truncated expression) surface from the debugger
    /// itself at set/evaluate time, where nothing is created on failure.</summary>
    public static (string Detail, string Remediation)? CheckExpression(string expression)
    {
        return CheckTextRules(expression) ?? CheckInvocationRule(expression);
    }

    /// <summary>Empty/statement/line/assignment gates: everything before the invocation gate.</summary>
    internal static (string Detail, string Remediation)? CheckTextRules(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ("must not be empty", "pass a read-only expression");
        if (expression.Contains(';'))
            return ("must not contain ';' (statements are not supported)", "pass a single read-only expression");
        if (expression.Contains('\r') || expression.Contains('\n'))
            return ("must be single-line", "pass a single read-only expression");
        if (ContainsAssignment(expression))
            return ("must be read-only (assignment, ++ and -- are not supported)", "pass an expression without '=', '++' or '--'");
        return null;
    }

    internal static (string Detail, string Remediation)? CheckInvocationRule(string expression) =>
        ContainsInvocation(expression)
            ? ("must be read-only (method calls are not supported)", "pass field, property and indexer reads or comparisons without '(...)'")
            : null;

    /// <summary>Fake/live-emulated log rendering: each {expression} hole becomes its evaluated value.</summary>
    public static string FormatLogMessage(string template, Func<string, string> evaluate)
    {
        var rendered = new System.Text.StringBuilder(template.Length);
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '{')
            {
                var close = template.IndexOf('}', i + 1);
                rendered.Append(evaluate(template.Substring(i + 1, close - i - 1)));
                i = close;
            }
            else
            {
                rendered.Append(template[i]);
            }
        }
        return rendered.ToString();
    }

    public static IEnumerable<string> Holes(string template)
    {
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{')
                continue;
            var close = template.IndexOf('}', i + 1);
            if (close < 0)
                yield break;
            yield return template.Substring(i + 1, close - i - 1);
            i = close;
        }
    }

    private const string ForeignPrefix = "other";

    /// <summary>Stable id for the Nth breakpoint a debug session created (N only goes up).</summary>
    public static string OwnedId(int n) => $"bp{n}";

    /// <summary>Id for a foreign breakpoint (one this debug session did not create): its
    /// position in the VS instance's list, recomputed per listing and never stored or removable.</summary>
    public static string ForeignId(int position) => $"{ForeignPrefix}{position}";

    public const string ForeignWarning =
        "not created by this debug session (set by the user or another debug session); list-only, remove it in VS";

    public static DebugDomainException NotFound(string breakpointId) =>
        breakpointId.StartsWith(ForeignPrefix, StringComparison.Ordinal)
            ? new DebugDomainException(ErrorCodes.BreakpointNotFound,
                $"breakpoint {breakpointId} was not created by this debug session",
                "it belongs to the user or another debug session; remove it in VS, or pass an id set_breakpoint returned")
            : new DebugDomainException(ErrorCodes.BreakpointNotFound, $"no breakpoint {breakpointId}",
                "call list_breakpoints for valid ids");

    /// <summary>Session log buffer (capped): drops the oldest entry when full.</summary>
    public static void AppendLog(List<BreakpointLogEntry> log, BreakpointLogEntry item)
    {
        const int cap = 500;
        if (log.Count >= cap)
            log.RemoveAt(0);
        log.Add(item);
    }

    private static bool BracesBalanced(string template)
    {
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '{')
            {
                var close = template.IndexOf('}', i + 1);
                if (close < 0 || template.Substring(i + 1, close - i - 1).IndexOfAny(['{', '}']) >= 0)
                    return false;
                i = close;
            }
            else if (template[i] == '}')
            {
                return false;
            }
        }
        return true;
    }

    // Single home for the read-only gate: ArgumentValidator delegates here.
    internal static bool ContainsAssignment(string expression)
    {
        // Increment/decrement mutate as surely as '=': x++, ++x, x--, --x.
        if (expression.Contains("++", StringComparison.Ordinal) || expression.Contains("--", StringComparison.Ordinal))
            return true;
        for (var i = 0; i < expression.Length; i++)
        {
            if (expression[i] != '=')
                continue;
            var prev = i > 0 ? expression[i - 1] : '\0';
            var prevPrev = i > 1 ? expression[i - 2] : '\0';
            var next = i + 1 < expression.Length ? expression[i + 1] : '\0';
            // Compound shift-assignment (<<=, >>=, >>>=) is not a <= / >= comparison.
            if ((prev == '<' || prev == '>') && prevPrev == prev)
                return true;
            // Allow ==, !=, <=, >=, => comparisons/lambdas; anything else is assignment.
            if (next == '=' || prev == '=' || prev == '!' || prev == '<' || prev == '>' || next == '>')
                continue;
            return true;
        }
        return false;
    }

    // Any invocation can run code, and the text gate cannot judge purity, so a
    // call-looking '(' (word char, ')', ']' or quote before it) is refused. Bare
    // grouping/casts, member reads, comparisons and indexers have no such '('
    // and still pass; side-effecting getters without parens (counter.NextTicket)
    // stay on the live NSE backstop. shortcut: string literals and generic
    // arity (foo<T>(x)) are not parsed out, same coarseness as ContainsAssignment.
    internal static bool ContainsInvocation(string expression)
    {
        for (var i = 0; i < expression.Length; i++)
        {
            if (expression[i] != '(')
                continue;
            var j = i - 1;
            while (j >= 0 && char.IsWhiteSpace(expression[j]))
                j--;
            if (j < 0)
                continue; // leading '(' groups or casts; it calls nothing
            var prev = expression[j];
            if (prev == '_' || prev == ')' || prev == ']' || prev == '"' || prev == '\'' || char.IsLetterOrDigit(prev))
                return true;
        }
        return false;
    }
}
