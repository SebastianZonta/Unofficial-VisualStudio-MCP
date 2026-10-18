namespace VsDebugMcp;

/// <summary>Selects which running VS instance to bind. Pure function over ROT display names; testable without COM.</summary>
internal static class VsDiscovery
{
    private const string DtePrefix = "!VisualStudio.DTE";

    /// <returns>The winning ROT display name, or null when nothing matches.</returns>
    public static string? SelectCandidate(IEnumerable<string> displayNames, string? instanceOverride)
    {
        var names = displayNames.Where(n => n.StartsWith(DtePrefix, StringComparison.Ordinal)).ToList();
        if (instanceOverride is not null)
        {
            var wanted = instanceOverride.StartsWith('!') ? instanceOverride : "!" + instanceOverride;
            return names.FirstOrDefault(n => n == wanted || n.StartsWith(wanted + ":", StringComparison.Ordinal));
        }
        return names.OrderByDescending(VersionOf).ThenBy(n => n, StringComparer.Ordinal).FirstOrDefault();
    }

    private static Version VersionOf(string displayName)
    {
        // Format: "!VisualStudio.DTE.18.0:1234" — version between the prefix and the pid suffix.
        var rest = displayName[DtePrefix.Length..].TrimStart('.');
        var versionText = rest.Split(':')[0];
        return Version.TryParse(versionText, out var v) ? v : new Version(0, 0);
    }
}
