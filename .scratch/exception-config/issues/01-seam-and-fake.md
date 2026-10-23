# 01: Settings seam plus fake

**What to build:** per-type exception-break settings (first-chance, unhandled-only, default) on the seam with fake-seeded settings and breaks, so configuration logic is testable with no VS.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] List works in any mode; enable, restrict, and reset round-trip through list.
- [ ] Change-while-running is rejected naming the mode; unknown type names are rejected.
- [ ] Break payloads carry a usable exception object path; settings never leak across sessions.
