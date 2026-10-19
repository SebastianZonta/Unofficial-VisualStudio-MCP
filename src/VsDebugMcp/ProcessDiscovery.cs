namespace VsDebugMcp;

/// <summary>Selects which target processes to list. Pure function over snapshots; testable without OS calls.</summary>
internal static class ProcessDiscovery
{
    /// <summary>Name substring match is case-insensitive; empty means no name filtering.
    /// Owned-only keeps mine entries (flagged ones included, so the flag stays visible);
    /// off keeps everything. Fail-closed holds because unknown owners are never mine.
    /// Ordering is by name, then process id, both ascending and deterministic.</summary>
    public static IReadOnlyList<TargetProcessInfo> FilterAndOrder(
        IEnumerable<TargetProcessInfo> candidates, string? nameFilter, bool ownedOnly)
    {
        var filter = nameFilter?.Trim();
        return candidates
            .Where(p => !ownedOnly || p.Mine)
            .Where(p => string.IsNullOrEmpty(filter) ||
                p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Pid)
            .ToList();
    }
}
