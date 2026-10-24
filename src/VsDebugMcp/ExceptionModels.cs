namespace VsDebugMcp;

/// <summary>Per-type exception-break modes. Listing reports first-chance or unhandled-only;
/// default is a change-only value restoring the setting this session found.</summary>
public static class ExceptionBreakModes
{
    public const string FirstChance = "first-chance";
    public const string UnhandledOnly = "unhandled-only";
    public const string Default = "default";
}

public sealed record ExceptionSettingInfo(string Name, string Mode);

public sealed record ListExceptionSettingsResult(IReadOnlyList<ExceptionSettingInfo> Settings, string Mode, string? LastHit);

public sealed record SetExceptionSettingResult(ExceptionSettingInfo Setting, string Mode, string? LastHit);

/// <summary>Exception break payload: Path drills via read_variables/evaluate; Message is null
/// when unreadable or refused under nse (the result's note then says so).</summary>
public sealed record ExceptionBreakInfo(string Type, string Path, string? Message);

/// <summary>Shared exception-config rules for both bridges. Pure functions over snapshots.</summary>
internal static class ExceptionConfig
{
    public const string ExceptionPath = "$exception";
    public const string MessageUnreadableNote = "exception message unreadable without side effects; probe $exception via read_variables or evaluate";
    private const int MaxValueLength = 500;

    /// <summary>Settings edits happen in break or design mode only; there is no queued apply.</summary>
    public static void RequireNotRunning(string mode)
    {
        if (mode == "run")
            throw new DebugDomainException(
                ErrorCodes.AlreadyRunning, $"exception settings cannot change while the debuggee is {mode}",
                "pause first, or change settings in design mode");
    }

    public static DebugDomainException NotFound(string name) => new(
        ErrorCodes.ExceptionNotFound, $"no exception type {name}",
        "pass a fully qualified CLR type name as listed by list_exception_settings");

    public static IReadOnlyList<ExceptionSettingInfo> FilterAndOrder(IEnumerable<ExceptionSettingInfo> settings, string? nameFilter)
    {
        var filter = nameFilter?.Trim();
        return settings
            .Where(s => string.IsNullOrEmpty(filter) || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static string? Truncate(string? message) =>
        message is { Length: > MaxValueLength } ? message[..MaxValueLength] + " [truncated]" : message;

    public static string BreakNote(string reason, ExceptionBreakInfo info) => info.Message is null
        ? $"{reason} {info.Type}; {MessageUnreadableNote}"
        : $"{reason} {info.Type}";
}
