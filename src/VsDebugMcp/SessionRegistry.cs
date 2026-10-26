using EnvDTE;
using System.Runtime.InteropServices;

namespace VsDebugMcp;

/// <summary>One live binding: a VS instance held by exactly one debug session.
/// Dte is null once the instance is known dead; the record stays until detach.</summary>
internal sealed class SessionEntry
{
    public DTE? Dte
    {
        get; set;
    }
    public string DisplayName { get; set; } = "";
    public int Pid
    {
        get; set;
    }
    public bool Dead
    {
        get; set;
    }
    /// <summary>Breakpoints this session created, in creation order, under stable ids
    /// (never positions, never reused). The single record for remove, list and detach
    /// cleanup (deleted on detach so no stale conditional breakpoint surprises a later
    /// session). Guarded by the registry lock; RCWs touched on the dispatcher only.</summary>
    public List<OwnedBreakpoint> Owned { get; } = new();
    /// <summary>Mints bp&lt;N&gt; ids; only goes up.</summary>
    public int BreakpointCounter
    {
        get; set;
    }
    /// <summary>Logpoints are emulated evaluate-then-decide (native log output lands in
    /// the VS Output window, which automation cannot read), so the Resume loop needs
    /// the template per breakpoint id. Guarded by the registry lock.</summary>
    public List<LogpointSpec> Logpoints { get; } = new();
    public List<BreakpointLogEntry> Log { get; } = new();
    /// <summary>Break-when-thrown value per exception type as this session first found it,
    /// so a reset to default restores it. Guarded by the registry lock.</summary>
    public Dictionary<string, bool> FoundExceptionModes { get; } = new(StringComparer.Ordinal);
}

internal sealed record OwnedBreakpoint(string Id, Breakpoint Breakpoint);

internal sealed record LogpointSpec(string Id, string File, int Line, string LogMessage)
{
    // A hit is still recognised by file:line against lastHit: two logpoints at the
    // same location share the template match (first wins per hit).
    public string Key => $"{File}:{Line}";
}

/// <summary>Single owner of the live debug-session table and its lock: idempotent
/// re-attach convergence, dead-binding marking, session-less inventory, detach,
/// and per-session breakpoint/exception records borrowed by the bridge.
/// Behavior is verbatim what the live bridge did inline.</summary>
internal sealed class SessionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SessionEntry> _sessions = new();

    /// <summary>Resolves the live DTE for a session. Unknown id is no-session;
    /// a dead binding is per-session vs-not-running (siblings unaffected).</summary>
    internal static DebugDomainException NoSession() =>
        new(ErrorCodes.NoSession, "no active session", "call attach first");

    internal static DebugDomainException VsGone(string sessionId) =>
        new(ErrorCodes.VsNotRunning, $"the VS instance bound to session {sessionId} is gone", "call attach again");

    public DTE SessionDte(string sessionId)
    {
        lock (_gate)
        {
            return RequireAlive(LookupEntryLocked(sessionId), sessionId);
        }
    }

    // Call under _gate; single TryGetValue so the lookup stays atomic with the caller.
    private SessionEntry LookupEntryLocked(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
            throw NoSession();
        return entry;
    }

    private static DTE RequireAlive(SessionEntry entry, string sessionId)
    {
        if (entry is { Dead: true } or { Dte: null })
            throw VsGone(sessionId);
        return entry.Dte!;
    }

    public (string Id, SessionEntry Entry)? FindByDisplayName(string displayName)
    {
        lock (_gate)
        {
            return ScanByDisplayName(displayName);
        }
    }

    private (string Id, SessionEntry Entry)? ScanByDisplayName(string displayName)
    {
        var hit = _sessions.FirstOrDefault(kv => kv.Value.DisplayName == displayName);
        return hit.Key is null ? null : (hit.Key, hit.Value);
    }

    /// <summary>Atomic raced-insert for attach: returns the existing session id when a
    /// parallel attach won the race (caller releases its duplicate handle), otherwise
    /// inserts and returns the new session id.</summary>
    public string GetOrAdd(string displayName, DTE dte, int pid, out bool won)
    {
        lock (_gate)
        {
            var raced = ScanByDisplayName(displayName);
            if (raced is not null)
            {
                won = false;
                return raced.Value.Id;
            }
            var sessionId = Guid.NewGuid().ToString("N");
            _sessions[sessionId] = new SessionEntry
            {
                Dte = dte,
                DisplayName = displayName,
                Pid = pid,
            };
            won = true;
            return sessionId;
        }
    }

    public static bool ProbeAlive(DTE? dte)
    {
        if (dte is null)
            return false;
        return IsResponsive(dte);
    }

    private static bool IsResponsive(DTE dte)
    {
        try
        {
            _ = dte.Name;
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidComObjectException) { return false; }
    }

    /// <summary>Marks one session dead on a disconnect HResult; the caller releases
    /// the RCW on the dispatcher, never the caller thread.</summary>
    public void MarkDead(string sessionId, DTE dte)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
                ClearIfOwned(entry, dte);
        }
    }

    private static void ClearIfOwned(SessionEntry entry, DTE dte)
    {
        if (!ReferenceEquals(entry.Dte, dte))
            return;
        entry.Dte = null;
        entry.Dead = true;
    }

    public List<SessionInfo> ListLiveSessions()
    {
        lock (_gate)
        {
            // Inventory lists only live sessions, like the fake bridge.
            return _sessions.Where(kv => !kv.Value.Dead).Select(kv => new SessionInfo(kv.Key, kv.Value.DisplayName)).ToList();
        }
    }

    /// <summary>Atomically forgets one session; the returned entry is exclusively the
    /// caller's (breakpoint cleanup reads it lock-free).</summary>
    public bool TryRemove(string sessionId, out SessionEntry? entry)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var found))
            {
                entry = null;
                return false;
            }
            _sessions.Remove(sessionId);
            entry = found;
            return true;
        }
    }

    /// <summary>Forgets every session, returning live bindings for one
    /// best-effort release-all on the dispatcher.</summary>
    public List<DTE> Drain()
    {
        lock (_gate)
        {
            var live = _sessions.Values
                .Where(e => e.Dte is not null)
                .Select(e => e.Dte!)
                .ToList();
            _sessions.Clear();
            return live;
        }
    }

    /// <summary>Copies of the session's breakpoint records; live handles stay owned by the session.</summary>
    public (List<OwnedBreakpoint> Owned, List<LogpointSpec> Specs, List<BreakpointLogEntry> Log) SnapshotOwned(string sessionId)
    {
        lock (_gate)
        {
            _sessions.TryGetValue(sessionId, out var entry);
            return (entry?.Owned.ToList() ?? new List<OwnedBreakpoint>(),
                entry?.Logpoints.ToList() ?? new List<LogpointSpec>(),
                entry?.Log.ToList() ?? new List<BreakpointLogEntry>());
        }
    }

    public OwnedBreakpoint? FindOwned(string sessionId, string breakpointId)
    {
        lock (_gate)
            return _sessions.TryGetValue(sessionId, out var entry)
                ? entry.Owned.FirstOrDefault(o => o.Id == breakpointId) : null;
    }

    /// <summary>Mints the next stable id and records ownership plus the logpoint
    /// template inside one lock; throws no-session/vs-not-running when the
    /// session is gone, so the caller can delete the untracked breakpoint.</summary>
    public string MintOwned(string sessionId, Breakpoint bp, string file, int line, string? logMessage)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry) || entry.Dead || entry.Dte is null)
                throw entry is null ? NoSession() : VsGone(sessionId);
            var id = BreakpointOptions.OwnedId(++entry.BreakpointCounter);
            entry.Owned.Add(new OwnedBreakpoint(id, bp));
            if (logMessage is not null)
                // Key on the VS snapshot path (full path), never the user-passed file:
                // the Resume loop matches specs against lastHit, which is full-path.
                entry.Logpoints.Add(new LogpointSpec(id, file, line, logMessage));
            return id;
        }
    }

    /// <summary>Drops owned ids (and their logpoint specs) from the session record.</summary>
    public void Forget(string sessionId, IEnumerable<string> ids)
    {
        var dead = ids.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
                return;
            entry.Owned.RemoveAll(o => dead.Contains(o.Id));
            entry.Logpoints.RemoveAll(s => dead.Contains(s.Id));
        }
    }

    public (LogpointSpec? Spec, OwnedBreakpoint? Owner) FindLogpointOwner(string sessionId, string? lastHit)
    {
        if (lastHit is null)
            return (null, null);
        lock (_gate)
        {
            _sessions.TryGetValue(sessionId, out var entry);
            var spec = entry?.Logpoints.FirstOrDefault(s => s.Key.Equals(lastHit, StringComparison.OrdinalIgnoreCase));
            var owner = spec is null ? null : entry!.Owned.FirstOrDefault(o => o.Id == spec.Id);
            return (spec, owner);
        }
    }

    public void AppendLog(string sessionId, BreakpointLogEntry logEntry)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
                BreakpointOptions.AppendLog(entry.Log, logEntry);
        }
    }

    /// <summary>Stored break-when-thrown value, recording the live observation on
    /// first use; the observer runs only when unrecorded, exactly as before.</summary>
    public bool GetOrObserveFoundMode(string sessionId, string exceptionType, Func<bool> observe)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
                throw NoSession();
            var modes = entry.FoundExceptionModes;
            if (!modes.TryGetValue(exceptionType, out var found))
                modes[exceptionType] = found = observe();
            return found;
        }
    }
}