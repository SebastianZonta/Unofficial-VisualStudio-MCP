namespace VsDebugMcp;

/// <summary>Selects which running VS instance to bind. Pure function over ROT display names; testable without COM.</summary>
internal static class VsDiscovery
{
    private const string DtePrefix = "!VisualStudio.DTE";

    /// <returns>The winning ROT display name, or null when nothing matches.</returns>
    public static string? SelectCandidate(IEnumerable<string> displayNames, string? instanceOverride)
    {
        var names = displayNames.Where(IsDteEntry).ToList();
        if (instanceOverride is not null)
        {
            var wanted = instanceOverride.StartsWith('!') ? instanceOverride : "!" + instanceOverride;
            return names.FirstOrDefault(n => n == wanted || n.StartsWith(wanted + ":", StringComparison.Ordinal));
        }
        return names.OrderByDescending(VersionOf).ThenBy(n => n, StringComparer.Ordinal).FirstOrDefault();
    }

    public static bool IsDteEntry(string displayName)
        => displayName.StartsWith(DtePrefix, StringComparison.Ordinal);

    /// <summary>ROT display names look like "!VisualStudio.DTE.18.0:1234"; pid is the suffix after ':'.</summary>
    public static int TryParsePid(string displayName)
    {
        var idx = displayName.LastIndexOf(':');
        return idx >= 0 && int.TryParse(displayName[(idx + 1)..], out var pid) ? pid : 0;
    }

    public static string VersionTextOf(string displayName)
    {
        var rest = displayName.StartsWith(DtePrefix, StringComparison.Ordinal)
            ? displayName[DtePrefix.Length..].TrimStart('.')
            : displayName.TrimStart('!');
        var versionText = rest.Split(':')[0];
        return Version.TryParse(versionText, out _) ? versionText : "";
    }

    /// <summary>DTE entries only, newest-version-first (same order as the attach default).</summary>
    public static IReadOnlyList<string> OrderForListing(IEnumerable<string> displayNames)
        => displayNames.Where(IsDteEntry)
            .OrderByDescending(VersionOf).ThenBy(n => n, StringComparer.Ordinal).ToList();

    private static Version VersionOf(string displayName)
        => Version.TryParse(VersionTextOf(displayName), out var v) ? v : new Version(0, 0);
}
