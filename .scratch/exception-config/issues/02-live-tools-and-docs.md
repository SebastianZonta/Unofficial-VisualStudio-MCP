# 02: Live exception settings plus tools and docs

**What to build:** the live bridge maps VS exception settings and list/configure tools expose them, verified manually by breaking first-chance on a test exception type.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** ready-for-agent

- [ ] First-chance break carries the object path; `$exception.Message`-style probes work immediately after the break.
- [ ] Existing `[Exception]` frame rendering and `read_stack` semantics unchanged.
- [ ] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the tools (no test counts in `README.md`).
