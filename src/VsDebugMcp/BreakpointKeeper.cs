using EnvDTE;
using System.Runtime.InteropServices;

namespace VsDebugMcp;

/// <summary>Keeps breakpoint ownership for one debug session: stable bp&lt;N&gt; ids that
/// only go up, idempotent repeat sets, foreign refusal, and list-only foreign entries.
/// All live <see cref="Breakpoint"/> handles are touched here on the dispatcher thread only.</summary>
internal sealed class BreakpointKeeper
{
    private readonly SessionRegistry _registry;
    private readonly Func<Debugger, string> _modeOf;
    private readonly Func<Debugger, string?> _lastHitOf;

    public BreakpointKeeper(SessionRegistry registry, Func<Debugger, string> modeOf, Func<Debugger, string?> lastHitOf)
    {
        _registry = registry;
        _modeOf = modeOf;
        _lastHitOf = lastHitOf;
    }

    public (BreakpointInfo Info, string Mode, string? LastHit, bool Created) Set(
        Debugger debugger, string sessionId, string file, int line,
        string? cond, int? count, string? mode, string? log)
    {
        // VS hands back the existing breakpoint for an occupied location, so owning what Add
        // returns would let remove/detach delete the user's. Check first: an own repeat
        // returns its id unchanged, a foreign one is refused (positional id as in the listing).
        var live = new List<Breakpoint>();
        foreach (Breakpoint b in debugger.Breakpoints)
            live.Add(b);
        var snapshot = _registry.SnapshotOwned(sessionId);
        List<OwnedBreakpoint> owned = snapshot.Owned;
        List<LogpointSpec> specs = snapshot.Specs;
        (BreakpointInfo Info, string Mode, string? LastHit, bool Created) Occupied(int i)
        {
            var mine = owned.FirstOrDefault(o => SameBreakpoint(live[i], o.Breakpoint))
                ?? throw BreakpointOptions.Exists(BreakpointOptions.ForeignId(i + 1), file, line);
            var spec = specs.FirstOrDefault(s => s.Id == mine.Id);
            var info = SnapshotBreakpoint(live[i], mine.Id) with { LogMessage = spec?.LogMessage };
            BreakpointOptions.RequireSameOptions(info, cond, count, mode, log);
            return (info, _modeOf(debugger), _lastHitOf(debugger), false);
        }
        var occupied = live.FindIndex(b => BreakpointOptions.SameLocation(b.File, b.FileLine, file, line));
        if (occupied >= 0)
            return Occupied(occupied);
        var created = debugger.Breakpoints.Add(
            Function: "", File: file, Line: line, Column: 1,
            Condition: cond ?? "", ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue,
            Language: "",
            Data: "", DataCount: 0, Address: "", HitCount: count ?? 0,
            HitCountType: count is null ? dbgHitCountType.dbgHitCountTypeNone
                : mode == HitCountModes.Every ? dbgHitCountType.dbgHitCountTypeMultiple
                : dbgHitCountType.dbgHitCountTypeEqual);
        // Own exactly what Add created (its returned collection), never "the last item
        // of the global list", which could be a breakpoint the user set.
        var bp = created is { Count: > 0 } ? created.Item(1) : debugger.Breakpoints.Item(debugger.Breakpoints.Count);
        // Safety net for a path the location check missed: Add handed back an existing
        // breakpoint. Never delete or own it.
        var handedBack = live.FindIndex(b => SameBreakpoint(b, bp));
        if (handedBack >= 0)
            return Occupied(handedBack);
        try
        {
            // Automation cannot observe pending-vs-bound (Concord-only); a successful Add means VS accepted it.
            var info = SnapshotBreakpoint(bp, id: "");
            // Mint ownership inside the dispatch: the live handle never crosses
            // threads, and a detach racing the call leaves nothing behind.
            string id;
            try
            {
                id = _registry.MintOwned(sessionId, bp, info.File, info.Line, log);
            }
            catch (DebugDomainException)
            {
                // Nobody could ever remove this breakpoint (it would list as
                // other<N>), so delete it before the error surfaces, never afterwards.
                try { bp.Delete(); } catch { /* best effort */ }
                Release(bp);
                throw;
            }
            return (Info: info with { Id = id, Bound = true }, Mode: _modeOf(debugger), LastHit: _lastHitOf(debugger), Created: true);
        }
        catch (COMException)
        {
            // Add succeeded but the call fails (e.g. busy): no id will own it, so it would
            // linger as other<N>. Delete it best-effort before the error surfaces.
            try { bp.Delete(); } catch (COMException) { /* best effort */ }
            throw;
        }
    }

    public (List<BreakpointInfo> Items, string Mode, string? LastHit, List<BreakpointLogEntry> Log) List(
        Debugger debugger, string sessionId)
    {
        var snapshot = _registry.SnapshotOwned(sessionId);
        List<OwnedBreakpoint> owned = snapshot.Owned;
        List<LogpointSpec> specs = snapshot.Specs;
        List<BreakpointLogEntry> log = snapshot.Log;
        // Owned breakpoints under their stable ids (creation order), then every other
        // breakpoint in the VS instance under a positional, list-only id.
        var items = new List<BreakpointInfo>();
        var gone = new List<OwnedBreakpoint>();
        var live = new List<Breakpoint>();
        foreach (Breakpoint bp in debugger.Breakpoints)
            live.Add(bp);
        foreach (var o in owned)
        {
            if (!StillExists(o.Breakpoint))
            {
                // Deleted by hand in VS: the id is dead, never re-pointed.
                gone.Add(o);
                continue;
            }
            var spec = specs.FirstOrDefault(s => s.Id == o.Id);
            var info = SnapshotBreakpoint(o.Breakpoint, o.Id);
            items.Add(spec is null ? info : info with { LogMessage = spec.LogMessage });
        }
        for (var i = 0; i < live.Count; i++)
        {
            if (owned.Any(o => SameBreakpoint(live[i], o.Breakpoint)))
                continue;
            items.Add(SnapshotBreakpoint(live[i], BreakpointOptions.ForeignId(i + 1))
                with
            { Warning = BreakpointOptions.ForeignWarning });
        }
        if (gone.Count > 0)
            _registry.Forget(sessionId, gone.Select(g => g.Id));
        return (items, _modeOf(debugger), _lastHitOf(debugger), log);
    }

    public (string RemovedId, string Mode, string? LastHit) Remove(
        Debugger debugger, string sessionId, string breakpointId)
    {
        // Only ids this session minted resolve: a stale, positional or foreign id never
        // reaches a breakpoint the user (or another session) set.
        OwnedBreakpoint? target = _registry.FindOwned(sessionId, breakpointId);
        if (target is null)
            throw BreakpointOptions.NotFound(breakpointId);
        try
        {
            target.Breakpoint.Delete();
        }
        catch (COMException ex) when (!ComErrorMapper.IsCallRejected(ex))
        {
            // Already deleted by hand in VS (a deleted object can report a disconnect
            // HResult): gone either way. A dead VS still surfaces via ModeOf below.
            // A busy rejection deleted nothing: it propagates as vs-busy and the id stays owned.
        }
        _registry.Forget(sessionId, [breakpointId]);
        Release(target.Breakpoint);
        return (breakpointId, _modeOf(debugger), _lastHitOf(debugger));
    }

    /// <summary>Deletes one detached session's owned breakpoints; call on the dispatcher.</summary>
    public static void DeleteOwned(IEnumerable<OwnedBreakpoint> owned)
    {
        foreach (var bp in owned.Select(o => o.Breakpoint))
        {
            try { bp.Delete(); } catch { /* best effort */ }
            Release(bp);
        }
    }

    internal static int HitsOf(Breakpoint bp)
    {
        // Hits accrue on the bound children; the pending parent always reports 0.
        var hits = 0;
        foreach (Breakpoint child in bp.Children) hits += child.CurrentHits;
        return hits;
    }

    private static bool StillExists(Breakpoint bp)
    {
        try
        {
            _ = bp.File;
            return true;
        }
        // Any failure on our own handle means the breakpoint is gone (a deleted object can
        // report a disconnect HResult); a dead VS still surfaces via the caller's ModeOf read.
        catch (Exception ex) when (ex is COMException or InvalidComObjectException)
        {
            return false;
        }
    }

    /// <summary>COM identity: the same breakpoint object may surface through a fresh RCW
    /// on each enumeration, so compare IUnknown pointers, not references.</summary>
    private static bool SameBreakpoint(Breakpoint a, Breakpoint b)
    {
        if (ReferenceEquals(a, b))
            return true;
        return HaveSameIUnknown(a, b);
    }

    private static bool HaveSameIUnknown(Breakpoint a, Breakpoint b)
    {
        var ua = nint.Zero;
        var ub = nint.Zero;
        try
        {
            ua = Marshal.GetIUnknownForObject(a);
            ub = Marshal.GetIUnknownForObject(b);
            return ua == ub;
        }
        catch (Exception ex) when (ex is COMException or InvalidComObjectException)
        {
            return false;
        }
        finally
        {
            ReleasePointer(ua);
            ReleasePointer(ub);
        }
    }

    private static void ReleasePointer(nint p)
    {
        if (p != nint.Zero) Marshal.Release(p);
    }

    private static BreakpointInfo SnapshotBreakpoint(Breakpoint bp, string id)
    {
        // Bind state is eventual and invisible to automation: a successful Add means accepted.
        // Option reads are best-effort; a transient COM failure falls back to the plain shape.
        string? condition = null;
        int? target = null;
        string? mode = null;
        var hits = 0;
        try
        {
            (condition, target, mode, hits) = ReadBreakpointOptions(bp);
        }
        catch (COMException)
        {
            // Keep the plain shape; options stay null/zero rather than failing the list.
        }
        // LogMessage comes from the session's logpoint specs (see List); the VS-side
        // log action is not automation-readable.
        return new(id, bp.File, bp.FileLine, Bound: true, Warning: null,
            condition, target, mode, hits, LogMessage: null);
    }

    private static (string? Condition, int? Target, string? Mode, int Hits) ReadBreakpointOptions(Breakpoint bp)
    {
        var condition = ReadCondition(bp);
        var hits = HitsOf(bp);
        if (bp.HitCountType == dbgHitCountType.dbgHitCountTypeNone)
            return (condition, null, null, hits);
        return (condition, bp.HitCountTarget, HitModeValue(bp), hits);
    }

    private static string? ReadCondition(Breakpoint bp) =>
        string.IsNullOrWhiteSpace(bp.Condition) ? null : bp.Condition;

    private static string HitModeValue(Breakpoint bp) =>
        bp.HitCountType == dbgHitCountType.dbgHitCountTypeMultiple ? HitCountModes.Every : HitCountModes.Exact;

    /// <summary>Best-effort RCW release; failures are ignored by design.</summary>
    private static void Release(object obj)
    {
        try { Marshal.ReleaseComObject(obj); } catch { /* best effort */ }
    }
}
