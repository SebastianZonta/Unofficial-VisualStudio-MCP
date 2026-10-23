# Module / Symbol Status Reporting

Status: ready-for-agent

## Problem Statement

Agents currently cannot tell why a breakpoint stays unbound: `list_breakpoints` shows bound/unbound but never says whether the module is loaded or symbols failed. We once burned a whole demo on unloaded symbols with no tool to see it. The agent guesses — re-setting breakpoints, resuming blindly — because the debugger's module/symbol state is invisible through the MCP.

## Solution

Add a session-scoped, read-only `list_modules` tool: per debug session, report each module in the target process with module name/path, loaded or not, and per-module symbol load state. Read-only and side-effect free; requires break or run mode (not design); each debug session sees only its own target process's modules.

## User Stories

1. As an agent, I want to list modules in the target process for my debug session, so that I can see what is actually loaded.
2. As an agent, I want each entry to carry module name and module path, so that I can match a breakpoint's file to the module that must contain it.
3. As an agent, I want a loaded flag per module, so that I can tell unloaded code apart from loaded code.
4. As an agent, I want per-module symbol load state (loaded, missing, failed), so that I stop guessing why a breakpoint stays unbound.
5. As an agent, I want a symbol detail (path or failure reason) where available, so that missing-PDB versus mismatched-PDB is distinguishable.
6. As an agent, I want to correlate an unbound breakpoint with its module's symbol state, so that "symbols not loaded" becomes a one-step diagnosis.
7. As an agent, I want the listing to be read-only with no debugger side effects, so that checking status never disturbs the target process.
8. As an agent, I want the tool to require break or run mode and fail clearly in design mode, so that I get "not debugging" instead of an empty misleading list.
9. As an agent, I want module data scoped to my debug session, so that sibling debug sessions on other VS instances never cross-contaminate.
10. As an agent, I want entries ordered deterministically (by module name), so that repeated polls diff cleanly.
11. As an agent, I want an optional name substring filter, so that I can jump straight to the module I care about without flooding context.
12. As an agent, I want one unreadable module entry to be skipped or flagged without failing the whole list, so that a single locked module never blinds me.
13. As an agent, I want the `mode` echo on the result, so that I know whether the snapshot was taken in break or run mode.
14. As an agent, I want existing breakpoint, execution, and inspection tools unchanged, so that status reporting adds diagnosis without changing debugging behavior.
15. As a builder, I want module enumeration behind the single bridge seam, so that tools stay thin and EnvDTE specifics stay in one place.

## Implementation Decisions

- Add a session-scoped, read-only list operation on the IDebugBridge seam plus a matching MCP tool beside the breakpoint tools; no behavior change to any existing tool.
- Entry contract per module: module name, module full path, loaded flag, symbol state enum (loaded / missing / failed / unknown), plus a detail string (symbol path when loaded, failure reason when failed, empty when unknowable).
- Mode gate: the operation requires the debug session's debugger to be in break or run mode and fails with a not-debugging error in design mode; in run mode the result is a best-effort snapshot that may race module loads.
- Session scoping: enumeration runs against the VS instance bound to the calling debug session and reports only that session's target process modules; sibling debug sessions are fully independent.
- Snapshot semantics: live debugger handles are snapshotted to plain DTOs and never escape, mirroring existing inspection tools; per-module failures are flagged with detail text while the rest succeed.
- Optional name substring filter matches case-insensitively against module name and path; empty means no filtering. Ordering is by module name then path, both ascending and deterministic.
- Glossary vocabulary governs naming: debug session versus VS instance versus target process, mode, session-less is explicitly not this tool (a session id is required), VS PID versus target PID stays distinct.
- No symbol loading or module loading is triggered: this tool reports state only and never attempts to load symbols, load modules, or change breakpoints.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge interface results, and filter/ordering behavior. No assertions on EnvDTE internals, COM call order, or private state.
- Tested surface: listing against the FakeDebugBridge with mixed symbol states; name filtering; deterministic ordering; per-debug-session independence (modules under session A never appear under B); design-mode rejection; single bad entry flagged without failing the list; read-only check (listing twice changes nothing observable).
- Prior art: tool-boundary tests against the FakeDebugBridge in the style of the existing breakpoint and inspection tests; fake-seeded module tables covering loaded/missing/failed symbol states without touching a real VS instance.
- Live coverage stays manual: attach to a real target process, break, list modules, confirm an unbound breakpoint's module shows the expected symbol state, resume and re-list.

## Out of Scope

- Loading symbols, reloading modules, or changing symbol paths (report-only; remediation is a follow-up).
- Breakpoint-to-module auto-correlation inside the server (the agent correlates using name/path; no new join field beyond what both tools already return).
- Per-function or per-source-file symbol granularity; module level only.
- Symbol server configuration, PDB search paths, or checksum validation.
- Persisting module snapshots across detaches or MCP restarts.
- Session-less operation; a debug session in break/run mode is always required.

## Further Notes

- This is the diagnostic companion to conditional breakpoints: when a conditional breakpoint never fires, `list_modules` is the first check (unloaded module or missing symbols) before suspecting the condition expression.
- Symbol states are debugger-reported and best-effort: `unknown` is a valid state when the debugger exposes nothing, and agents should treat it as "no information" rather than "symbols fine".
