# 01: One-shot lifecycle in seam plus fake

**What to build:** a composed place-resume-remove run-to operation on the seam with fake-driven arrival, timeout cleanup, and concurrent-hit handling, so the lifecycle is testable with no VS.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Arrival reports the destination lastHit and the temporary breakpoint is gone afterwards.
- [x] Timeout returns still-running with cleanup done; unbindable file/line fails fast; design mode is rejected.
- [x] Pre-existing breakpoints survive; a concurrent earlier hit wins with cleanup still done.
