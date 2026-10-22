Labels: wayfinder:research
Answers: .scratch/multi-attach/issues/02-multi-dte-com-lifetime.md

## Question

How does multi-DTE lifetime behave on the single `ComDispatcher.cs` STA thread:
per-session `ReleaseComObject` safety, ROT display-name stability with 3 VS instances open,
dispatcher queue behavior under concurrent calls to different sessions, and what a bound VS
exiting mid-session looks like from COM (for per-session `vs-not-running`)?

## Findings

### 1. Per-session `ReleaseComObject` is safe — one RCW per VS instance

- Each COM interface pointer entering the CLR gets a Runtime Callable Wrapper (RCW) with its
  own reference count; `ReleaseComObject` decrements only that RCW, and at zero the runtime
  releases its unmanaged references (subsequent use throws). Source: `Marshal.ReleaseComObject`
  reference remarks.
- Distinct VS instances are distinct COM identities, so `GetObject` on different ROT monikers
  yields distinct RCWs. Releasing session A's `DTE` RCW does not disturb session B's.
  The codebase already relies on this: `AttachAsync` releases the old `_dte` on re-bind
  (`src/VsDebugMcp/DteDebugBridge.cs:28-34`), `SnapshotInstance` releases its temp `dte`
  (`DteDebugBridge.cs:877`), `WithRot` releases rot/ctx/enumerator plus each moniker immediately
  (`DteDebugBridge.cs:769-776,799,824`).
- Design rule for multi-attach: `Dictionary<sessionId, (DTE dte, string displayName)>`; release
  exactly the departing session's RCW on detach / re-attach / dispose, then null/remove the slot.
  Never `FinalReleaseComObject`, never release the same RCW twice. Do NOT eagerly release child
  RCWs (`Debugger`, `Breakpoints`, `Expression`): the code correctly leaves them to GC, and the
  VS-team history ("ReleaseComObject Considered Dangerous") shows releasing a still-reachable
  shared RCW poisons later users (`InvalidComObjectException`) — the "silent assassin".
- Gap found: current releases happen off-dispatcher (`AttachAsync` line 31, `Dispose` line 945 run
  on the caller thread, not inside `_dispatcher.InvokeAsync`). The `ReleaseComObject` docs warn
  that releasing an RCW while a call on it is executing can AV. Multi-session should move the
  release *inside* a dispatcher work item for the dying session, serialized after its last call.

### 2. ROT display names are stable while instances stay open

- Format is `!VisualStudio.DTE.<version>:<pid>` — see `VsDiscovery.TryParsePid` /
  `VersionTextOf` (`src/VsDebugMcp/VsDiscovery.cs:24-37`) and Microsoft's launch-DTE sample,
  which polls for `$"VisualStudio.DTE.16.0:{vsProcess.Id}"` in the ROT.
- The ROT is an explicit Register/Revoke table: every `Register` must be matched by `Revoke`
  (`IRunningObjectTable::Register` / `::Revoke` docs). VS registers at startup and revokes on
  exit; opening/closing instance C only adds/removes C's entry — survivors' names (PID suffix
  included) do not change. `EnumRunning` is a point-in-time snapshot, and the code already
  re-enumerates per operation (`EnumRotNames` per bind/list) — keep that for liveness checks.
- Caveat: the name embeds the OS PID, so after exit + PID recycling a *new* VS could theoretically
  reoccupy an identical display string. Liveness must not rely on name presence alone — a bound
  session must also survive a probe COM call (see §4). Store `(displayName, pid)` at bind.

### 3. Dispatcher: concurrent sessions serialize FIFO; one wedged VS stalls all

- `ComDispatcher` (`src/VsDebugMcp/ComDispatcher.cs`) is one STA thread + `BlockingCollection`
  bounded to 100, FIFO. Concurrent agent calls to different sessions queue and execute strictly
  serially — STA-safe by construction, no per-session threads needed (EnvDTE objects have thread
  affinity; extra STA threads would break identity + ordering).
- Head-of-line blocking: a VS showing a modal dialog (→ `RPC_E_CALL_REJECTED 0x80010001`,
  mapped to `vs-busy` in `ComErrorMapper.cs`) or hanging hangs the single thread, so *all*
  sessions behind it wait. Timeouts (`10s` issue/mode, `30s` snapshot, `5s` poll) only cancel the
  *waiter* (`done.TrySetCanceled`; `ComDispatcher.cs:42,46-58`) — the work item still runs when
  dequeued. A burst of timed-out calls still drains serially.
- Queue full (100 pending) or disposed → `vs-busy` (`ComDispatcher.cs:35-41`). Keep the v1
  single-dispatcher design (map lists STA contention as not-yet-specified) and keep work items
  small: snapshot to plain DTOs inside one `InvokeAsync` and escape no live handles (already the
  `RunSnapshotAsync` pattern, `DteDebugBridge.cs:410-426`). No priority lanes in v1.

### 4. Bound VS exits mid-session: ROT revoke + disconnected RCW → per-session `vs-not-running`

- ROT side: the exiting VS revokes its entry, so re-enumeration drops the name and `GetRotObject`
  returns null — current `BindRunningInstance` already maps that to `vs-not-running`
  (`DteDebugBridge.cs:737-740`).
- Live-handle side: calls on the stale `DTE` RCW throw `COMException` with server-gone HResults,
  canonically `RPC_E_DISCONNECTED 0x80010108` ("object invoked has disconnected from its clients")
  and `RPC_S_SERVER_UNAVAILABLE 0x800706BA` (`COM error codes (STG, RPC)` reference).
- Gap: `ComErrorMapper` currently maps only `0x80010001 → vs-busy` and `0x80070005 →
  elevation-mismatch`; everything else becomes generic `automation-failed` (`ComErrorMapper.cs:11-22`).
  For per-session `vs-not-running`, add a call-site/disconnect check: on those HResults (plus ROT
  name gone), mark *that* session dead — release its RCW once on the dispatcher, null its `DTE`
  slot but keep the session record so later calls report `vs-not-running` (display name + "call
  attach again") instead of `no-session`/`stale-session`. The single-session analogue already
  exists (`RequireSessionAsync` null-`_dte` check, `DteDebugBridge.cs:55-57`). Sibling sessions are
  unaffected: separate RCWs, dispatcher keeps serving.

## Recommended session-record shape (for implementer)

`{ sessionId, displayName, pid, DTE? dte (null = dead), bool dead }` + on any `COMException` from
that session's dispatcher work: if HResult in `{0x80010108, 0x800706BA, 0x800706B5, 0x8001010A}`
or ROT re-enumeration lacks `displayName` → release-once on dispatcher, set `dte=null, dead=true`,
throw `vs-not-running`. Detach removes the record after its single release.

## Sources

- Code: `src/VsDebugMcp/DteDebugBridge.cs` (esp. 24-59, 728-828, 834-879, 939-949),
  `src/VsDebugMcp/ComDispatcher.cs`, `src/VsDebugMcp/VsDiscovery.cs`, `src/VsDebugMcp/ComErrorMapper.cs`.
- https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.releasecomobject
- https://devblogs.microsoft.com/visualstudio/marshal-releasecomobject-considered-dangerous/
- https://learn.microsoft.com/en-us/visualstudio/extensibility/launch-visual-studio-dte
- https://learn.microsoft.com/en-us/windows/win32/api/objidl/nf-objidl-irunningobjecttable-register
- https://learn.microsoft.com/en-us/windows/win32/api/objidl/nf-objidl-irunningobjecttable-revoke
- https://learn.microsoft.com/en-us/windows/win32/api/objidl/nf-objidl-irunningobjecttable-enumrunning
- https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-3 (RPC_E_DISCONNECTED 0x80010108)
