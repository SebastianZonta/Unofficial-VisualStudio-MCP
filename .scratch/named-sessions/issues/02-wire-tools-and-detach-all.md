# 02: Wire all tools plus detach_all and docs

**What to build:** every session-scoped tool accepts label-or-id through the shared resolution path plus a `detach_all` one-call teardown, so agents can work entirely by name.

**Blocked by:** 01 (needs the resolution contract to wire).

**Status:** ready-for-agent

- [ ] All 15+ session-scoped tools reachable by label; detach works by label; `detach_all` reports released sessions and handles the empty case.
- [ ] Freed labels are immediately reusable; dead sessions keep labels until detached (per-session `vs-not-running` unchanged).
- [ ] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document labels and `detach_all` (no test counts in `README.md`).
