# 01: Consent-gated write in seam plus fake

**What to build:** a consent-gated single-variable write on the seam with fake frames to write against, so fault-injection logic is testable in CI with no VS.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Write with consent reflects on subsequent fake read; without consent fails closed with state unchanged.
- [x] Wrong mode, type mismatch, read-only target, and unknown path each fail with their structured error and no mutation.
- [x] Cross-session frame ids are rejected; `evaluate` stays read-only.
