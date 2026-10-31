# 07: Area and "all" requests

**What to build:** A developer can plan an area ("the checkout flows") and get several flow plans under that area, each listed in the flow index; asking for a flow that already has a plan updates it instead of duplicating it. `run-flow` accepts a flow name, an area, or "all", resolved through the flow index, and runs the resulting flows one after another, returning one compact result per flow. See [spec](../spec.md).

**Blocked by:** 04 (run-flow: one flow, one solution, launch)

**Status:** done

- [x] Area request yields several plans under the area plus one index line each
- [x] Re-planning an existing flow updates its plan and index line; no duplicate file or line
- [x] `run-flow <area>` and `run-flow all` resolve through the index; an unknown area is an error
- [x] Flows sharing a solution run sequentially; main agent gets one compact result per flow

## Comments

Dogfood (2026-10-30, Sandbox copy): "plan the calc area: the step scenario and the stack scenario" → one planner wrote `calc/step.md` and `calc/stack.md`, 12 questions numbered across both, one `## calc` heading with two lines after the answers. "plan the step scenario again; inside Add it should also check sum" → same file updated in place (new checkpoint, earlier confirmations kept), still one `[step]` line. `run-flow billing` → not in the index (names and areas listed). `run-flow calc` → step then stack, each its own flow runner, both PASS with one `Verdict:` block each; no debug session left. `run-flow all` resolved to the five index lines in order; not executed (it would need a hand-started greet process and a hand-written name.txt; the sequential path is the one `calc` proved). Every multi-flow run is sequential for now, whatever its solutions; disjoint-solution parallelism is 08. Resolution precedence on a name clash: flow name, then area, then `all`. A proposed plan in a multi-flow run is skipped and reported; the rest run.
