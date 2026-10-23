# 02: Live EnvDTE modules plus tool and docs

**What to build:** the live bridge enumerates real target-process modules and a `list_modules` tool exposes them, so an agent can match an unbound breakpoint to its module's symbol state in one call.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** ready-for-agent

- [ ] Live attach, break, and list shows the expected symbol state for a module with an unbound breakpoint; resume and re-list works.
- [ ] Reporting is read-only: no symbol loads, no breakpoint changes, no mode change.
- [ ] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the tool (no test counts in `README.md`).
