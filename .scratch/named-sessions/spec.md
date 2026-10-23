# Named sessions
Status: ready-for-agent

## Problem Statement

`attach` returns opaque hex sessionIds. An agent juggling N debug sessions (one per VS instance) against N target processes cannot tell them apart without joining `list_sessions` against `list_instances` and keeping its own side-map. That side-map drifts: re-attach returns an existing debug session, `detach` frees it, and hex ids give no affordance for which is which. A wrong-id call fails closed with `no-session`, but the failure mode is confusion, retries, and tearing down the wrong sibling debug session.

## Solution

Give each debug session an optional human-meaningful label, managed MCP-side only (VS instances never see labels). The label can be set at `attach` time or via a `rename_session` tool, is shown in `list_sessions`, and is accepted wherever a sessionId is required: exact id, exact label, or unambiguous prefix match; an ambiguous prefix fails with an error listing the candidates. Add a `detach_all` tool for one-call teardown of every live debug session. Labels are unique across live debug sessions and are freed on `detach` (or `detach_all`), so a freed label is immediately reusable.

## User Stories

1. As an agent developer, I want to pass a label at attach time, so that each debug session is named from birth without a second call.
2. As an agent developer, I want to rename a live debug session, so that a hex handle from an earlier attach becomes meaningful later.
3. As an agent developer, I want `list_sessions` entries to include each debug session's label, so that I can pick the right session without joining other tools.
4. As an agent developer, I want to call any session-scoped tool with a label instead of a sessionId, so that stored scripts stay readable.
5. As an agent developer, I want unambiguous prefix match on label-or-id, so that short prefixes work without typing full hex.
6. As an agent developer, I want an ambiguous prefix to fail with an error listing the candidate debug sessions, so that I can disambiguate instead of hitting the wrong session.
7. As an agent developer, I want label uniqueness enforced across live debug sessions, so that a label always routes to exactly one session.
8. As an agent developer, I want a freed label to be reusable immediately after its debug session detaches, so that recycled workflows (attach → work → detach → attach) keep stable names.
9. As an agent developer, I want detach to keep working with either the sessionId or the label, so that teardown does not require id bookkeeping.
10. As an agent developer, I want a `detach_all` tool that releases every live debug session in one call, so that end-of-run cleanup cannot leak siblings.
11. As an agent developer, I want re-attaching the same VS instance to preserve (not duplicate or steal) its debug session's label, so that idempotent re-attach stays predictable.
12. As an agent developer, I want invalid labels (empty, whitespace-only, or colliding with an existing id/label) rejected with `invalid-argument`, so that naming rules are explicit and consistent with existing validation.
13. As an agent developer, I want unknown label-or-id values to keep failing with the existing `no-session` error, so that error handling does not fork into a new code path.
14. As an MCP client operator, I want labels to never reach VS/COM, so that naming cannot mutate debugger state or leak into the IDE.

## Implementation Decisions

- Everything crosses the `IDebugBridge` seam; tools stay thin and delegate label resolution to the bridge, exactly as session lookup works today.
- Resolution contract (bridge-owned, MCP-side only): accept exact sessionId, then exact label, then unambiguous prefix over the union of ids and labels; ambiguous prefix returns an error whose detail lists the matching candidates (sessionId + label pairs); no match returns existing `no-session`. All session-scoped tools (`set_breakpoint`, `list_breakpoints`, `remove_breakpoint`, `pause`, `resume`, `step`, `read_stack`, `read_variables`, `evaluate`, `attach_process`, `detach`, `RequireSessionAsync`) share this one resolution path so behavior cannot diverge per tool.
- Label lifecycle contract: optional `label` parameter on attach; new `rename_session` tool (label-or-id + new label); `list_sessions` entries gain a nullable label field; `detach_all` releases every live debug session and reports which sessions were released; labels unique per live debug session (case-sensitive match, consistent with existing id handling), freed on `detach`/`detach_all`; detached sessions never retain labels; per-session `vs-not-running` semantics unchanged (a dead debug session keeps its label until detached, siblings unaffected).
- Validation contract: labels constrained the same way as other free-text tool inputs — reject empty/whitespace-only with `invalid-argument`; reject duplicates with `invalid-argument` naming the holder; normalization limited to exact-match semantics (no case-folding, no trimming surprises — reject rather than silently rewrite).
- `FakeDebugBridge` is the behavioral reference: it gains the same label store, resolution order, ambiguity error shape, and lifecycle rules, so tests pin the contract without VS.
- Glossary discipline: spec and tools use `debug session`, `VS instance`, `target process`, `session-less` (`list_sessions` stays session-less), and `VS PID vs target PID` exactly; labels are properties of debug sessions, never of VS instances or target processes.

## Testing Decisions

- External behavior only: attach-with-label, rename, list-shows-labels, every session-scoped tool reachable by label, prefix-match accept, ambiguous-prefix error listing candidates, duplicate-label rejection, invalid-label rejection, unknown-value `no-session`, detach-by-label, `detach_all` empty/non-empty, label freed and reusable after detach, re-attach preserves label, labels invisible to VS (no debugger-state mutation observable through any tool).
- Prior art is `FakeDebugBridge` with `dotnet test VsDebugMcp.slnx` (no VS needed); extend the fake's session tests the same way multi-session behavior (one debug session per VS instance, idempotent re-attach, siblings unaffected by detach, per-session vs-not-running) was pinned there.
- No new harness against live VS beyond existing builder-only checks; no timing or COM-threading assertions.

## Out of Scope

- Persisting labels across server restarts; renaming VS instances, solutions, or target processes; non-unique or hierarchical labels; label-based filtering on `list_instances`/`list_processes`; changing sessionId format or generation; any VS/COM-visible naming; bulk rename.

## Further Notes

- Open question for the agent: whether `attach` with a colliding label should fail or steal-and-relabel — recommended default is fail with `invalid-argument` (predictable, matches uniqueness rule); record the choice in the new ADR if one is written.
- If a `rename_session` tool feels heavy, the minimal alternative is label-at-attach plus `detach_all` only — but the stories above assume rename ships; cutting it requires trimming stories 2 and 12 accordingly.
- Convention reminders: no test counts in README.md; decisions recorded as new files in docs/adr/ (never edit a released ADR).
