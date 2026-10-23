# 02: Live run-to-cursor plus tool and docs

**What to build:** the live bridge implements place-resume-delete against EnvDTE and a `run_to_cursor` tool exposes it, verified manually with one call arriving at the destination.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** ready-for-agent

- [ ] Break in a target, run to a later line in one call: arrival shows destination lastHit and the breakpoint list is unchanged.
- [ ] Break-or-run accepted, design rejected, timeout mirrors `resume` semantics.
- [ ] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the tool (no test counts in `README.md`).
