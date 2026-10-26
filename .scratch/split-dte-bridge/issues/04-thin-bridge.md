# 04: Thin the bridge, verify green

**What to build:** Leave the live bridge as 22 delegating operations with no remaining lifecycle, ownership, or stepping logic of its own, so the full tool surface behaves exactly as before and the split is proven by a green build and suite.

**Blocked by:** 02, 03 (needs keeper and driver in place).

**Status:** ready-for-agent

- [ ] The live bridge holds no session-table, breakpoint-identity, or stepping logic of its own
- [ ] The external bridge seam is unchanged: same operations, same DTOs, same error codes
- [ ] The in-memory fake bridge is untouched
- [ ] Build plus existing unit suite stays green with no live-VS gate
