# 02: Live follow watch plus docs

**What to build:** the live bridge watches via `list_processes` polling after a bound target process exits and re-attaches on fingerprint match, reporting run-ended then re-attached, verified manually.

**Blocked by:** 01 (needs the fingerprint contract to implement).

**Status:** ready-for-agent

- [ ] Kill-and-restart with identical args re-attaches with the new target PID; a colliding-name different binary is ignored.
- [ ] Poll-then-bind races resolve to watch-continues, never a wrong binding.
- [ ] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document follow mode (no test counts in `README.md`).
