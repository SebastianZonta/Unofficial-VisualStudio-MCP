# 09: Execution control end to end

**What to build:** the run/step loop an agent can demo alone: pause, resume, and one direction-parameterized step tool, with waits that return data instead of hanging and guidance instead of raw failures.

**Blocked by:** 07 (Host skeleton plus attach round-trip).

Status: resolved

- [x] Single step tool with over/into/out direction; pause and resume as separate tools
- [x] Every result echoes debugger mode plus last-hit; waits honor the timeout parameter (default 5000 ms) and cancellation
- [x] Over-budget waits return a first-class timeout status, never a hung call
- [x] Inspect-style calls outside break mode are unreachable here, but execution errors stay named (bad session, timeout)
- [x] Scripted tool-call tests cover pause/resume/step plus the timeout path

## Comments

Implemented 2026-10-18. `ExecutionTools` (`pause`/`resume`/`step`, `timeoutMs = 5000` defaults, cancellation plumbed, direction validated in-method) + `ExecutionResult(Mode, LastHit, Status, Note)` where Status is `ok`/`timeout`. Bridge seam extended; fake models run/break/design transitions (resume with armed bound bp lands break-with-lastHit, without stays run; pause requires run; step requires break), `ForceTimeout` models over-budget waits without changing mode. New `ToolErrors.MapAsync` helper clears 08's triplicated catch wart; `BreakpointTools` refactored onto it (08 tests still green). All breakpoint/set/list/remove results now echo live mode/lastHit instead of hardcoded design. Tests: 17/17 pass (3 attach + 6 breakpoints + 8 execution). Server smoke-tested. No git repo, nothing to commit.
