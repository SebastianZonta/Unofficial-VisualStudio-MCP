# 01: Seam contract plus fake for launch

**What to build:** a session-scoped launch operation on the seam (exe path, args, working directory, env) with the fake returning a phantom target PID and attached-from-birth binding, so launch flows are testable with no OS process.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Missing executable and already-bound session fail with structured errors and no partial binding.
- [x] Launching a `devenv` path is refused (never attach to a VS PID).
- [x] Result shape matches `attach_process` (target PID plus mode/lastHit); `attach_process` semantics unchanged.
