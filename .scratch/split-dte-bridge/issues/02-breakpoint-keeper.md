# 02: Extract the breakpoint keeper

**What to build:** Keep all breakpoint ownership for a debug session in one module behind the bridge seam, so stable ids only go up, repeat sets stay idempotent, foreign breakpoints stay list-only, and claiming a foreign location stays refused — behavior identical to today.

**Blocked by:** 01 (needs the session entry the registry owns).

**Status:** ready-for-agent

- [ ] Setting a location the debug session already owns returns its existing id without changing options
- [ ] Setting a location holding a foreign breakpoint is refused with guidance, never claimed
- [ ] Listing shows foreign breakpoints as positional list-only entries that remove refuses
- [ ] Live breakpoint handles stay confined to the keeper and are touched on the dispatcher thread only
- [ ] Build plus existing unit suite stays green
