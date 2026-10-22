Labels: wayfinder:map

## Destination

Agents can `attach` to several VS 2026 instances at once (nuget + 2 consumers) and drive each through its own isolated debug session, on branch `feature/multi-attach`, with tests and docs updated.

## Notes

- Domain: C# managed debugging in Visual Studio 2026 (attach, breakpoints, stepping, inspection).
- Skills every session should consult: `research` for facts outside this directory, `prototype` for throwaway stubs, `grilling` + `domain-modeling` for decisions and language.
- Standing preferences from charting: one debug session per VS instance (glossary holds), re-attach to the same instance is idempotent; inventory via `list_sessions` + `detach`, existing tool signatures unchanged; `stale-session` deleted, per-session `vs-not-running`; sessions fully isolated, any cross-project coordination lives at the agent level, never in the MCP; no session cap in v1; this map carries execution on `feature/multi-attach`.
- Tracker: local markdown (see `docs/agents/issue-tracker.md`, Wayfinding operations). Frontier = open, unblocked, unclaimed files in `.scratch/multi-attach/issues/`, first by number wins.

## Decisions so far

- [Session store and tool surface](issues/01-session-store-and-tool-surface.md): `Dictionary<sessionId, SessionBinding>` under a lock with idempotent re-attach by ROT display name; `list_sessions` (session-less inventory) + `detach` (releases one DTE); `stale-session` deleted, ADR-0003 superseded by ADR-0004.
- [Multi-DTE COM lifetime](issues/02-multi-dte-com-lifetime.md): per-session release safe inside dispatcher work items; ROT names stable, probe for liveness; single STA dispatcher kept for v1 (head-of-line blocking documented, not fixed); dead VS → per-session `vs-not-running` via new mapper cases; dead records kept until `detach`. Detail in `research/multi-dte-lifetime.md` on branch `research/multi-dte-lifetime`.
- [Bridge and fake build](issues/03-bridge-and-fake-build.md): dict store in both bridges with per-session isolation, `list_sessions`/`detach` wired, `stale-session` deleted, disconnect HResults mapped; `dotnet test` 108/108 with new `MultiSessionTests`.
- [Docs and branch hygiene](issues/04-docs-and-branch-hygiene.md): TOOLS/GLOSSARY/TROUBLESHOOTING/README updated, ADR-0008 supersedes ADR-0003; branch holds the effort set plus two pre-existing rename-only hunks from `main`.

<!-- the index: one line per closed ticket, enough to judge relevance, then zoom the link for the detail the ticket holds -->

## Not yet specified

- LiveCheck multi-instance harness against real VS 2026 instances.
- Session cap / eviction policy if session count ever grows unbounded.
- Agent-level coordinated-debug recipes (e.g. step-over here, step-into there).
- Attach by solution name as an alternative to ROT display-name override.

## Out of scope

- MCP-level coordinated or atomic cross-instance stepping.
- Proxy/gateway routing or codemode-style dispatch; same tools, just `sessionId`-scoped.
- Remote debugging, non-VS-2026 IDEs.
- Max-session enforcement or eviction in v1.
