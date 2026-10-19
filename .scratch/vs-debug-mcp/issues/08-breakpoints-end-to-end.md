# 08: Breakpoints end to end

**What to build:** the full breakpoint loop an agent can demo alone: set a file/line breakpoint, see it listed with bind state, remove it — all through the stdio boundary against the fake bridge.

**Blocked by:** 07 (Host skeleton plus attach round-trip).

Status: resolved

- [x] Set returns id plus bound flag immediately with a warning when symbols are not loaded (never blocks)
- [x] List reports bind states so the agent can poll to confirm arming before resuming
- [x] Remove by id; set/remove/list only — no toggles, conditions, or hit counts exposed
- [x] Scripted tool-call tests cover set/list/remove plus the unbound-warning path

## Comments

Implemented 2026-10-18. `BreakpointTools` (`set_breakpoint`/`list_breakpoints`/`remove_breakpoint`) + `BreakpointInfo` DTOs, all results carrying Mode/LastHit. Bridge seam extended (`Set/List/RemoveBreakpointAsync`); `FakeDebugBridge` models bind lifecycle via `SymbolsLoaded` (unbound + warning while false, poll-to-confirm on list once true). Named errors: new `DebugDomainException(Code, ...)` at the bridge seam (subclasses `InvalidOperationException`, so 07 intent preserved; 07's two assertions updated to the exact type since xUnit `ThrowsAsync` is exact-match), mapped to `McpException` at the tool boundary; explicit in-method arg validation (`invalid-argument`). Registration is chained `.WithTools<SessionTools>().WithTools<BreakpointTools>()` (single-type overload only). Tests: 9/9 pass (3 attach + 6 breakpoints). Server smoke-tested. Review: one wart — the try/catch→McpException mapping is triplicated across the three tools; extract a shared helper before 09/10 add more tools. No git repo, nothing to commit.
