# 03: plan-flow: one flow with a launch trigger

**What to build:** A developer names one flow in plain words; the main agent dispatches a planner subagent (sonnet) that reads only the code (no MCP calls) plus the flow-testing doc and flow index, and writes one committed flow plan under its area: name, gist, `launch` trigger, ordered checkpoints (target name, file, method, verbatim anchor fragment, line hint, expressions, expectations), expected outcome. Every expectation read off the code is *proposed* and comes back as an open question; the main agent puts the questions to the developer and resumes the same planner via SendMessage, which marks them *confirmed* and adds the flow's one line to the flow index. Refuses to run before setup. See [spec](../spec.md).

**Blocked by:** 01 (setup-flow-testing skill)

**Status:** ready-for-agent

- [ ] Planner instructions live as a reference file in the skill folder, read by the subagent only
- [ ] No flow-testing doc → tells the developer to run setup, writes nothing
- [ ] On the Sandbox copy, a named flow yields one plan file with a `launch` trigger and anchored checkpoints
- [ ] Inferred expectations are marked *proposed* and returned as questions; the main agent never answers them itself
- [ ] Resumed planner marks answered expectations *confirmed* without re-exploring
- [ ] Flow index gains one line (name linking the plan, gist) under the area
- [ ] Planner never calls an MCP tool
