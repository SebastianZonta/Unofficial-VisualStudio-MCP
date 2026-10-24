namespace VsDebugMcp;

/// <summary>Debugger-reported symbol load state per module. Unknown means "no information", never "fine".</summary>
public static class SymbolStates
{
    public const string Loaded = "loaded";
    public const string Missing = "missing";
    public const string Failed = "failed";
    public const string Unknown = "unknown";
}

/// <summary>One module of a debug session's target process. SymbolDetail is the symbol path when
/// loaded, the failure reason when failed or unreadable, empty when unknowable.</summary>
public sealed record ModuleInfo(string Name, string Path, bool Loaded, string SymbolState, string SymbolDetail);

public sealed record ListModulesResult(IReadOnlyList<ModuleInfo> Modules, string Mode, string? LastHit);

/// <summary>Shared list_modules rules for both bridges. Pure functions over snapshots.</summary>
internal static class ModuleListing
{
    /// <summary>Design mode has no target process: refuse instead of returning a misleading empty list.</summary>
    public static void RequireDebugging(string mode)
    {
        if (mode is not ("break" or "run"))
            throw new DebugDomainException(
                ErrorCodes.NotRunning, $"not debugging: debugger is {mode}",
                "attach_process or launch_process first; list_modules needs break or run mode");
    }

    /// <summary>Name substring matches name or path, case-insensitive; empty means no filtering.
    /// Ordering is by name, then path, both ascending and deterministic.</summary>
    public static IReadOnlyList<ModuleInfo> FilterAndOrder(IEnumerable<ModuleInfo> modules, string? nameFilter)
    {
        var filter = nameFilter?.Trim();
        return modules
            .Where(m => string.IsNullOrEmpty(filter) ||
                m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                m.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ThenBy(m => m.Path, StringComparer.Ordinal)
            .ToList();
    }
}
