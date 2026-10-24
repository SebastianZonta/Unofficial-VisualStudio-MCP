# 02: Live exception settings plus tools and docs

**What to build:** the live bridge maps VS exception settings and list/configure tools expose them, verified manually by breaking first-chance on a test exception type.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** needs-info

- [ ] First-chance break carries the object path; `$exception.Message`-style probes work immediately after the break.
- [ ] Live break result carries the message read under `nse`, matching the `$exception.Message` probe; a refused read yields an absent message plus note, never a failed break.
- [x] Existing `[Exception]` frame rendering and `read_stack` semantics unchanged.
- [x] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the tools (no test counts in `README.md`).

Live code and docs are in; the two live checks above are unverified (E2E pass skipped by request, no exception-config cases in docs/agents/e2e.md yet).
