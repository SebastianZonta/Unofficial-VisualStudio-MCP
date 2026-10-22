# ADR-0008: Multi-session with per-instance bindings

Status: accepted (supersedes ADR-0003)

## Context

Agents work on several solutions at once (a nuget plus its consumers), each in
its own VS instance. ADR-0003 allowed one active session: a new `attach`
replaced the old binding and the old id failed with `stale-session`, forcing
list → attach → work → re-attach loops that cannot hold two debuggers open.

## Decision

One debug session per VS instance, many alive at once. `attach` keeps a
`Dictionary<sessionId, binding>` keyed by ROT display name; re-attaching the
same instance returns its live session (probed for liveness, rebound when
dead). Two session tools complete the surface: `list_sessions` (session-less
inventory) and `detach` (releases one binding, siblings unaffected). The error
catalog loses `stale-session`: unknown ids are `no-session`, a dead binding is
per-session `vs-not-running`. Cross-session coordination stays at the agent
level — the MCP never steps or resumes two instances as one. The single STA
dispatcher is kept: one wedged VS stalls all sessions (documented, not fixed).

## Consequences

Agents hold several debuggers open with independent breakpoints, modes, and
`lastHit` per session. A VS exiting mid-session kills only its own record
(`detach` or re-attach recovers it). Single-session clients keep working
unchanged, minus `stale-session`.
