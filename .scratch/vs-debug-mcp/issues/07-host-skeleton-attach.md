# 07: Host skeleton plus attach round-trip

**What to build:** a running stdio MCP server an agent can attach to: the agent calls attach against a fake in-memory session and gets back a session id plus the standard result envelope, proving transport, registration, and session scoping end to end.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Server starts over stdio with explicit per-group tool registration and all logging to standard error
- [x] Attach returns an opaque session id; unknown session ids fail with a named no-session error
- [x] Every tool result carries debugger mode plus last-hit (envelope proven here, reused by all later tickets)
- [x] Scripted tool-call test drives attach through the stdio boundary against the fake bridge

## Comments

Implemented 2026-10-18. `src/VsDebugMcp` (net10.0, `ModelContextProtocol` v2.2.0): `Program.cs` (stdio transport, explicit `.WithTools<SessionTools>()`, all logs to stderr), `SessionTools` (`attach`), `IDebugBridge` (the one new seam), `FakeDebugBridge` (in-memory single-session; re-attach makes the old id stale), `AttachResult` (SessionId/Mode/LastHit envelope). Tests: 3/3 pass (`AttachTests`: envelope, no-session, stale-on-reattach). Server smoke-tested (starts, waits on stdio, clean shutdown). Follow-up for ticket 08: map bridge session errors to `McpException`/named tool-result errors at the tool boundary (currently `InvalidOperationException` at the bridge seam). No git repo in this folder, so nothing to commit.
