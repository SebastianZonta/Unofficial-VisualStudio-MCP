# 07: Area and "all" requests

**What to build:** A developer can plan an area ("the checkout flows") and get several flow plans under that area, each listed in the flow index; asking for a flow that already has a plan updates it instead of duplicating it. `run-flow` accepts a flow name, an area, or "all", resolved through the flow index, and runs the resulting flows one after another, returning one compact result per flow. See [spec](../spec.md).

**Blocked by:** 04 (run-flow: one flow, one solution, launch)

**Status:** ready-for-agent

- [ ] Area request yields several plans under the area plus one index line each
- [ ] Re-planning an existing flow updates its plan and index line; no duplicate file or line
- [ ] `run-flow <area>` and `run-flow all` resolve through the index; an unknown area is an error
- [ ] Flows sharing a solution run sequentially; main agent gets one compact result per flow
