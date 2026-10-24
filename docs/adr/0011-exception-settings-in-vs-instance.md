# ADR-0011: Exception settings live in the VS instance; exception breaks carry a payload

Status: accepted

## Context

The exception-config spec asks for per-type exception-break settings
(first-chance vs unhandled-only) "scoped to my own debug session", that never
rewrite VS-wide defaults, plus an exception payload on breaks so the agent can
triage a throw without re-discovery.

EnvDTE exposes exception settings only as the VS instance's own table
(`Debugger3.ExceptionGroups["Common Language Runtime Exceptions"]`,
`SetBreakWhenThrown`). There is no per-debug-session layer, and VS persists
that table with the solution's user settings on its own schedule.

## Decision

- **Scope = the VS instance.** One debug session binds one VS instance
  (ADR-0008), so changing that instance's table never affects a sibling debug
  session on another VS instance. This is the isolation the spec asks for.
- **`default` = what this debug session found.** The bridge records each
  type's break-when-thrown value before the session's first change and
  restores it on `default`. It does not restore VS's factory defaults, which
  automation cannot read.
- **No revert on `detach`.** The bridge cannot know whether the user changed
  the same setting in the VS UI meanwhile; reverting could undo their change.
  Agents reset what they tuned with `default`.
- **Exact names only.** `exceptionType` must match a CLR group entry's name
  exactly (ordinal). Short or differently cased names are
  `exception-not-found`; nothing is created.
- **Mode gate.** Listing works in any mode; changes need break or design mode
  (`already-running` while running). There is no queued apply.
- **Exception payload.** When execution stops because of an exception
  (`LastBreakReason` thrown or not handled), the result's `exception` carries
  `type`, `path` (`$exception`) and `message`. `lastHit` is null (it is for
  breakpoint hits only, ADR-0006). The message read is bridge-initiated but
  still runs with `, nse` (ADR-0010). A refused or failed read leaves
  `message` null, with the note pointing at `$exception`, and never fails the
  break.

## Consequences

- Spec conflict, surfaced: the spec's "does not rewrite the VS-wide defaults
  file" holds only as far as VS's own persistence allows. A setting left at
  `first-chance` may survive the debug session and come back in the next VS
  session for that solution. Verify live; if it persists, agents must reset
  with `default` before detaching.
- Live-unverified: whether `ExceptionSettings.Item(name)` fails with a
  `COMException` for unknown names (the bridge maps that to
  `exception-not-found`), and how often `nse` refuses `Exception.Message`
  getters. Record findings in `docs/TROUBLESHOOTING.md`.
- The live `message` is the evaluator's display value (quoted, escaped),
  identical to what `evaluate("$exception.Message")` returns.
