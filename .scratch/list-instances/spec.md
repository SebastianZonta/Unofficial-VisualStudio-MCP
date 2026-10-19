Status: ready-for-agent

## Problem Statement

When several VS instances are open on the same machine, the agent cannot answer "place a breakpoint in this X project" deterministically. Attach binds newest-wins by default, and there is no way to see which Solution and Projects each VS instance has open, so project-routed debugging is guesswork.

## Solution

A read-only instance-listing capability plus the existing explicit-override attach. The agent lists all running VS instances with their Solution and Project names, picks the VS instance whose Projects contain X, then attaches with that instance's display name. Switching VS instances stays explicit re-attach under single-session semantics.

## User Stories

1. As an agent, I want to list all running VS instances, so that I can see what is available before binding.
2. As an agent, I want each listed VS instance to carry its ROT display name, so that I have a deterministic attach key.
3. As an agent, I want each listed VS instance to carry its process id, so that I can correlate with task managers and logs.
4. As an agent, I want each listed VS instance to carry its version, so that newest-first ordering is explainable.
5. As an agent, I want each listed VS instance to carry its Solution name and path, so that I can match by Solution.
6. As an agent, I want each listed VS instance to carry its Project names, so that "breakpoint in X project" resolves to one VS instance.
7. As an agent, I want an empty Solution reported as empty data rather than an error, so that fresh VS instances are still listable.
8. As an agent, I want unreachable or busy VS instances skipped and flagged rather than failing the whole list, so that one modal dialog never blinds me.
9. As an agent, I want the list sorted newest-version-first, so that default choice matches attach behavior.
10. As an agent, I want Project names capped and alphabetically sorted, so that large solutions never flood my context.
11. As an agent, I want listing to require no session id, so that discovery works before any attach.
12. As an agent, I want listing to never mutate debugger state, so that discovery is safe to call freely.
13. As an agent, I want switching VS instances to mean re-attach with staleness on the old session, so that single-session semantics stay unambiguous.
14. As a builder, I want instance enumeration behind the single bridge seam, so that tools stay thin and COM stays in one place.
15. As a builder, I want ROT name parsing and ordering as a pure function, so that selection logic is testable without COM.

## Implementation Decisions

- Add a session-less, read-only list operation on the bridge seam plus a matching MCP tool beside attach. No behavior change to attach.
- Agent-side matching is the routing rule: the agent lists, finds the VS instance whose Project list contains X, then attaches with that entry's display name. No server-side project-hint parameter on attach.
- Keep single active session: a new attach replaces the old binding and the old session id goes stale. Multi-instance work equals list, attach, work, re-attach.
- Entry contract per VS instance: display name usable directly as instance override, numeric process id, version string, Solution name, Solution path, Project name list, unreachable flag, optional error text.
- Process id is parsed from the ROT display-name pid suffix; version from the version segment of the same name.
- List ordering mirrors attach default: highest version first, then display-name order.
- Empty or unsaved Solution is data: empty Solution path plus empty Project list, reachable, no error.
- Project list is alphabetically sorted and capped at 50 entries.
- Enumeration walks the ROT and snapshots each VS instance's Solution and Projects to plain DTOs inside the single STA dispatcher call; live automation handles never escape.
- Failure isolation per VS instance with a small per-instance budget inside an overall timeout: unreadable, busy, or dead instances return reachable-false entries with error text while the rest succeed.
- Non-VS ROT entries are ignored, as with attach selection.
- Glossary vocabulary governs naming: VS instance versus debug session versus Solution versus Project versus instance override.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge interface results, and pure ROT-name parsing. No assertions on COM internals, call order, or private state.
- Tested modules: the new list tool at the tool-call boundary against the in-memory fake bridge; the bridge interface contract for session-less listing and empty-solution shape; ROT display-name parsing, version ordering, and override prefix matching as pure unit tests.
- Prior art: tool-boundary tests against the fake bridge in the style of the existing attach tests; pure ROT selection tests in the style of the existing discovery tests; fake-driven unreachable-flag coverage without requiring a running VS.
- Live COM coverage stays manual: open two VS instances with distinct solutions and verify list output, attach-by-display-name, and skip-and-flag under one busy instance.

## Out of Scope

- A session-per-VS-instance map or parallel multi-session support.
- Server-side project-name routing or fuzzy solution-title search inside attach.
- Launching VS, opening solutions, loading projects, or starting debugging.
- Remote machines, SSH VS instances, or elevated-cross-user attach flows beyond existing same-user rules.
- Write operations on Solutions or Projects.
- Persisting instance history across MCP restarts.

## Further Notes

- The ROT display-name shape `!VisualStudio.DTE.<version>:<pid>` is the only stable cross-instance key available without new VS APIs; solution titles are human-friendly but not unique, hence the agent matches on Project names and attaches by display name.
- If Project-name collisions across VS instances become common, the natural follow-up is a tie-break rule (Solution path match first, then most-recently-used) rather than enlarging this spec.
