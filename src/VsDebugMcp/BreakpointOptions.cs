namespace VsDebugMcp;

/// <summary>Shared condition/hit-count/logpoint rules: validation plus log-template
/// parsing and rendering. Pure; each layer throws its own exception type.</summary>
internal static class BreakpointOptions
{
    /// <summary>One location rule for the tool boundary and both bridges: file/line
    /// validated before anything is created.</summary>
    public static void ValidateLocation(string file, int line)
    {
        if (string.IsNullOrWhiteSpace(file))
            throw DebugDomainException.Fail(ErrorCodes.InvalidArgument, "file must not be empty", "pass a source file path");
        if (line < 1)
            throw DebugDomainException.Fail(ErrorCodes.InvalidArgument, "line must be >= 1", "pass a 1-based line number");
    }

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
        var mode = (hitCount, hitCountMode) switch
        {
            (null, _) => null,
            (_, string m) when !string.IsNullOrWhiteSpace(m) => m,
            _ => HitCountModes.Exact,
        };
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

    /// <summary>Single home for the tool-boundary code choice: only a pure invocation
    /// failure is invalid-expression, anything tripping an earlier text gate is
    /// invalid-argument. ArgumentValidator delegates here.</summary>
    public static string CodeForExpression(string expression) =>
        CheckTextRules(expression) is null ? ErrorCodes.InvalidExpression : ErrorCodes.InvalidArgument;

    /// <summary>Empty/statement/line/assignment gates: everything before the invocation gate.</summary>
    internal static (string Detail, string Remediation)? CheckTextRules(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ("must not be empty", "pass a read-only expression");
        if (expression.Contains(';'))
            return ("must not contain ';' (statements are not supported)", "pass a single read-only expression");
        if (expression.Contains('\r') || expression.Contains('\n')
            || expression.Contains('\v') || expression.Contains('\f')
            || expression.Contains('\u0085') || expression.Contains('\u2028') || expression.Contains('\u2029'))
            return ("must be single-line", "pass a single read-only expression");
        if (expression.Contains("/*", StringComparison.Ordinal) || expression.Contains("*/", StringComparison.Ordinal)
            || expression.Contains("//", StringComparison.Ordinal))
            return ("must not contain comments", "pass a read-only expression without /* */ or // comments");
        var code = StripBlockComments(expression);
        if (ContainsAssignment(code))
            return ("must be read-only (assignment, ++ and -- are not supported)", "pass an expression without '=', '++' or '--'");
        return null;
    }

    internal static (string Detail, string Remediation)? CheckInvocationRule(string expression) =>
        ContainsInvocation(StripBlockComments(expression))
            ? ("must be read-only (method calls are not supported)", "pass field, property and indexer reads or comparisons without '(...)'")
            : null;

    /// <summary>Block comments are whitespace for the gate: foo /*c*/ (1) is still a call,
    /// while x =&gt; x.Id&gt;0 and (i==3) have no comments and still pass.</summary>
    internal static string StripBlockComments(string expression)
    {
        if (!expression.Contains("/*", StringComparison.Ordinal))
            return expression;
        var sb = new System.Text.StringBuilder(expression.Length);
        var i = 0;
        while (i < expression.Length)
        {
            if (i + 1 < expression.Length && expression[i] == '/' && expression[i + 1] == '*')
            {
                var close = expression.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    sb.Append(' ');
                    break;
                }
                sb.Append(' ');
                i = close + 2;
            }
            else
            {
                sb.Append(expression[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    /// <summary>Fake/live-emulated log rendering: each {expression} hole becomes its evaluated value.</summary>
    public static string FormatLogMessage(string template, Func<string, string> evaluate)
    {
        var rendered = new System.Text.StringBuilder(template.Length);
        var next = 0;
        foreach (var (open, close) in BraceSpans(template))
        {
            rendered.Append(template, next, open - next);
            rendered.Append(evaluate(template.Substring(open + 1, close - open - 1)));
            next = close + 1;
        }
        rendered.Append(template, next, template.Length - next);
        return rendered.ToString();
    }

    public static IEnumerable<string> Holes(string template)
    {
        foreach (var (open, close) in BraceSpans(template))
            yield return template.Substring(open + 1, close - open - 1);
    }

    /// <summary>Single brace scan: each {expression} hole as (open, close) indices.
    /// Stops at the first unclosed '{'; stray '}' is left for the caller to judge.</summary>
    private static IEnumerable<(int Open, int Close)> BraceSpans(string template)
    {
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{')
                continue;
            var close = template.IndexOf('}', i + 1);
            if (close < 0)
                yield break;
            yield return (i, close);
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

    /// <summary>A location already holding a breakpoint this debug session did not create. VS would
    /// hand that breakpoint back from Add, and owning it would let remove/detach delete it.</summary>
    public static DebugDomainException Exists(string foreignId, string file, int line) =>
        DebugDomainException.Fail(ErrorCodes.BreakpointExists,
            $"{file}:{line} already has breakpoint {foreignId} (not created by this debug session)",
            "use it as is (it still stops the target), or remove it in VS and set again");

    /// <summary>Repeat set on a location this debug session already owns returns that id; it never
    /// changes the existing breakpoint's options.</summary>
    public static void RequireSameOptions(BreakpointInfo existing,
        string? condition, int? hitCount, string? hitCountMode, string? logMessage)
    {
        if (existing.Condition != condition || existing.HitCountTarget != hitCount
            || existing.HitCountMode != hitCountMode || existing.LogMessage != logMessage)
            throw DebugDomainException.Fail(ErrorCodes.InvalidArgument,
                $"{existing.File}:{existing.Line} already has {existing.Id} with different options",
                $"remove_breakpoint {existing.Id} first, then set again with the new options");
    }

    /// <summary>Listed paths are VS full paths; the caller may pass the path as shown in the solution.
    /// shortcut: suffix match, so a same-named file elsewhere also counts; upgrade if that bites.</summary>
    public static bool SameLocation(string listedFile, int listedLine, string file, int line)
    {
        if (listedLine != line)
            return false;
        var tail = file.Replace('/', '\\').TrimStart('\\');
        return listedFile.Equals(file, StringComparison.OrdinalIgnoreCase)
            || listedFile.Replace('/', '\\').EndsWith("\\" + tail, StringComparison.OrdinalIgnoreCase);
    }

    public static DebugDomainException NotFound(string breakpointId) =>
        breakpointId.StartsWith(ForeignPrefix, StringComparison.Ordinal)
            ? DebugDomainException.Fail(ErrorCodes.BreakpointNotFound,
                $"breakpoint {breakpointId} was not created by this debug session",
                "it belongs to the user or another debug session; remove it in VS, or pass an id set_breakpoint returned")
            : DebugDomainException.Fail(ErrorCodes.BreakpointNotFound, $"no breakpoint {breakpointId}",
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
        var next = 0;
        foreach (var (open, close) in BraceSpans(template))
        {
            if (template.AsSpan(next, open - next).Contains('}'))
                return false;
            if (template.AsSpan(open + 1, close - open - 1).IndexOfAny('{', '}') >= 0)
                return false;
            next = close + 1;
        }
        var tail = template.AsSpan(next);
        return !tail.Contains('{') && !tail.Contains('}');
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
    // stay on the live NSE backstop. A generic-arity call foo<T>(x) ends its type
    // argument list with '>', so '>' before '(' counts when a '<' precedes it;
    // lambdas (a => (b)) and comparisons (a > (b)) have no '<' and still pass.
    // shortcut: string literals are not parsed out, same coarseness as ContainsAssignment.
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
            if (prev == '>' && expression.AsSpan(0, j).Contains('<'))
                return true;
        }
        return false;
    }
}
