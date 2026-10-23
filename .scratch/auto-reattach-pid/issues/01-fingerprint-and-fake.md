# 01: Fingerprint plus follow state in the fake

**What to build:** per-session follow mode (default off) with exe-path-plus-args fingerprint matching, emulated in the fake (exit plus same-binary reappearance), so auto-reattach logic is testable with no process churn.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Follow is off by default; opt-in/out round-trips; `detach` clears follow state.
- [ ] Exit then same-binary reappearance re-attaches with the new target PID; different binary never matches; live binding never double-binds.
- [ ] Breakpoints survive re-attach; gates (same-user, consent posture) apply per re-attach.
