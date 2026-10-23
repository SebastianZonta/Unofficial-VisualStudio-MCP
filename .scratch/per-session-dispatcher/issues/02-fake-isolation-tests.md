# 02: Fake isolation tests for the scheduling contract

**What to build:** fake-driven tests pinning the observable isolation contract (wedged sibling completes, per-session busy/timeout shapes, detach/attach progress during blockage, fallback parity), so scheduling regressions fail in CI with no VS.

**Blocked by:** 01 (needs the ADR's contract to pin).

**Status:** ready-for-agent

- [ ] Sibling completes at normal latency while one session is wedged; session-less tools stay responsive.
- [ ] Detach and attach make progress during blockage; fallback mode shows the same observable contract.
- [ ] No timing-sensitive assertions beyond the existing timeout-result contract.
