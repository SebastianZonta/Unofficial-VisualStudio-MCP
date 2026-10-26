# 01: Extract the session registry

**What to build:** Give the debug-session table a single owner behind the bridge seam, so attach converges parallel callers on one debug session per VS instance, dead bindings rebind or detach cleanly, and session-less inventory keeps working before any attach — all with behavior identical to today.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [x] Re-attaching the same VS instance returns its live debug session, never a duplicate binding
- [x] Parallel attaches against one VS instance converge on a single debug session
- [x] A dead binding rebinds or detaches without affecting sibling debug sessions
- [x] Detach releases exactly one debug session; siblings keep breakpoints and modes
- [x] Build plus existing unit suite stays green
