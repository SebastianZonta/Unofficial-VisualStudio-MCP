# 02: Live exception settings plus tools and docs

**What to build:** the live bridge maps VS exception settings and list/configure tools expose them, verified manually by breaking first-chance on a test exception type.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** resolved

- [x] First-chance break carries the object path; `$exception.Message`-style probes work immediately after the break.
- [x] Live break result carries the message read under `nse`, matching the `$exception.Message` probe; a refused read yields an absent message plus note, never a failed break.
- [x] Existing `[Exception]` frame rendering and `read_stack` semantics unchanged.
- [x] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the tools (no test counts in `README.md`).

Live code and docs are in; verified live by E2E 2026-10-24-1800-R3: E13 (unhandled break carries `$exception` path, message equals `$exception.Message` probe, no `lastHit`) and E21–E25 (list/set first-chance/unhandled-only round-trip with restore) all PASS.
