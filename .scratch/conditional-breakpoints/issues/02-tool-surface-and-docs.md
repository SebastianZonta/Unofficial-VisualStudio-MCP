# 02: Tool surface and docs for conditional breakpoints

**What to build:** `set_breakpoint` accepts condition, hit-count, and logpoint inputs and `list_breakpoints` displays them, so an agent can set and audit conditional breakpoints and logpoints end to end through the tools.

**Blocked by:** 01 (needs the seam contract to wire).

**Status:** ready-for-human

- [x] `set_breakpoint` options round-trip through the tool boundary; invalid inputs fail naming the offending input.
- [x] `list_breakpoints` entries show condition, hit-count setting, current count, and bound state deterministically.
- [x] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the new options (no test counts in `README.md`).

## Comments

Implemented: new `set_breakpoint` params (`condition`, `hitCount`, `hitCountMode`, `logMessage`) validated at the tool boundary (`invalid-argument` naming the input) and at the seam; docs updated in all three files. Covered by `ConditionalBreakpointTests.cs`.
