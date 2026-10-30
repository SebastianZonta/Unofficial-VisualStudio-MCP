# 04: run-flow: one flow, one solution, launch

**What to build:** A developer names a flow; the main agent resolves it through the flow index (unknown name = error), refuses a plan with any *proposed* expectation, and dispatches one flow runner subagent (sonnet). The flow runner checks preconditions (VS instance with the solution open via `list_instances`; build command green), never starts VS, attaches a debug session, starts the target with `launch_process`, resolves each checkpoint's anchor to its current line, sets breakpoints, judges each hit by evaluating the plan's expressions (PASS/FAIL/BLOCKED/OBS with quoted evidence), writes the report (`<flow>-<run tag>`) to the report directory or a location named in the request, cleans up (own breakpoints removed, debug session detached) on every exit, and returns only verdict, report path, and non-PASS rows. Never `set_variable`. See [spec](../spec.md).

**Blocked by:** 03 (plan-flow: one flow with a launch trigger)

**Status:** ready-for-agent

- [ ] Flow runner instructions live as a reference file in the skill folder
- [ ] Unknown flow name → error, no guess; plan with a proposed expectation → refused before dispatch
- [ ] Missing VS / solution not open / build failure → BLOCKED with the fix, no breakpoints set
- [ ] On the Sandbox copy, the plan from 03 runs to a verdict with quoted evidence per checkpoint
- [ ] Deliberately wrong expectation → FAIL; unexpected extra fact → OBS; verdict PASS only with no FAIL/BLOCKED
- [ ] Report written to default directory; a request-named location overrides it for that run
- [ ] Main agent receives only verdict, report path, non-PASS rows
- [ ] After the run (success, failure, timeout), `list_breakpoints` in a fresh session shows none of the runner's breakpoints and no debug session is left open
