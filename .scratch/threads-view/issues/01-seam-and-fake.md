# 01: Seam thread ops plus fake

**What to build:** thread list, freeze, and thaw plus an optional thread selector on stack/variable reads on the seam, emulated in the fake with seeded thread fixtures, so thread-aware debugging is testable with no VS.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Listing shows id, name, function, and frozen flag in deterministic order; freeze/thaw round-trips (thaw of unfrozen is a no-op).
- [ ] Reads default to the breakpoint-hit thread; unknown or foreign thread ids fail loudly; all ops require break mode.
- [ ] Thread ids never validate across debug sessions.
