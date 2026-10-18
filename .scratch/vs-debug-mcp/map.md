## Destination

A buildable spec at `.scratch/vs-debug-mcp/spec.md` for a same-machine, C#-only, minimal-core debugger MCP for Visual Studio 2026 that any agent can use without reading VS docs (< ~10 plain-named tools, safe defaults).

## Notes

- Domain: C# managed debugging in Visual Studio 2026 (breakpoints, stepping, call stacks, scopes/variables, evaluate).
- Skills every session should consult: `research` for facts outside this directory, `prototype` for throwaway stubs to react to, `grilling` + `domain-modeling` for decisions and language.
- Standing preferences from charting: VS 2026 only (not VS Code); C# only for v1; same-machine local-only, no remote; plan-only map (decisions, not deliverables; prototypes are throwaway, never merged); simplicity bar — a generic agent debugs without VS expertise; no bias on extension-bridge vs COM/DTE automation — research both and pick simplest reliable path.
- Tracker: local markdown (see `docs/agents/issue-tracker.md`, Wayfinding operations). Frontier = open, unblocked, unclaimed files in `.scratch/vs-debug-mcp/issues/`, first by number wins.

## Decisions so far

- [MCP server shape](issues/03-mcp-server-shape.md): C# stdio via `ModelContextProtocol` v2.x, explicit tool registration, stderr-only logging; detail in `research/03-mcp-server-shape.md`.
- [VS integration paths](issues/01-vs-integration-paths.md): out-of-proc COM/DTE, no extension (VsBridge precedent); VSIX only as fallback; Concord rejected; detail in `research/01-vs-integration-paths.md`.
- [Managed debug APIs](issues/02-managed-debug-apis.md): EnvDTE `Debugger2/3/5` + `Breakpoint2`/`Expression`/`StackFrame` covers the core; wrap binds, waits, eval side effects, stale handles; detail in `research/02-managed-debug-apis.md`.
- [Minimal tool surface](issues/04-minimal-tool-surface.md): 10 tools (`attach`, `set/remove/list_breakpoints`, `pause`, `resume`, `step`, `read_stack`, `read_variables`, `evaluate` read-only); `timeoutMs 5000`, `maxFrames 20`, `maxVariables 50`; truncated values with drill-down.
- [Session and safety model](issues/05-session-safety-model.md): ROT-newest discovery with override; F5 + local attach-by-ID; single session; consent-gated PID attach; timeouts + one dispatcher thread; break-mode gating; named elevation/busy errors.
- [Tool shape stub](issues/06-tool-shape-stub.md): verdict pass, no changes; throwaway asset `stub-prototype.html` (never merged).

<!-- the index: one line per closed ticket, enough to judge relevance, then zoom the link for the detail the ticket holds -->

## Not yet specified

- Validation strategy for the spec (how a builder proves the core loop works against a sample C# app).

## Out of scope

- VS Code, Rider, or any non-VS-2026 IDE.
- Remote debugging across machines; languages beyond C# for v1.
- Beyond-minimal surface for v1: conditional breakpoints, watch windows, threads/tasks parallel stacks, Edit-and-Continue, profiler/intellitrace.
