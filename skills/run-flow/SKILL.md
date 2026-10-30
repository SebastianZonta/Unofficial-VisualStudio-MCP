---
name: run-flow
description: "Run a flow plan against the VS debugger: a flow runner subagent builds, launches the target, checks every checkpoint, writes a run report, and returns the verdict. Use after plan-flow, when the developer names a flow to run."
disable-model-invocation: true
---

# Run flow

Run one flow plan the developer names and hand back its verdict. You are the orchestrator: a flow runner subagent drives the debugger and writes the report; you resolve the request and relay the result. Debugger output stays inside the flow runner.

## 1. Resolve the flow

Find the `Flow testing:` pointer line in the repo-root `CLAUDE.md` or `AGENTS.md` and the flow-testing doc it links; when either is missing, tell the developer to run `setup-flow-testing` first and stop. Look the flow name up in the flow index the doc names, by the exact link text of its line. When no line matches, tell the developer the name is not in the flow index, list the names that are, and stop; the developer picks. Done when you have the plan path.

## 2. Check the plan

Read the plan. When any expectation (or the expected outcome) is marked *proposed*, list those to the developer, tell them to confirm them through `plan-flow`, and stop. Done when every expectation in the plan is *confirmed*.

## 3. Dispatch the flow runner

Pick the report path: `<report dir>/<flow>-<run tag>.md`, where the report dir is a location the developer named in this request, else the doc's run report directory, and the run tag is the local time as `YYYYMMDD-HHMMSS`. Start one subagent with the `Agent` tool, `model: "sonnet"`, and this prompt (the reference path is `runner.md` in this skill's base directory, given as an absolute path):

```
You are the flow runner. Read <absolute path to runner.md> and follow it.
Flow plan: <plan path>
Flow-testing doc: <doc path>
Repo root: <absolute repo root>
Report: <report path>
```

Done when the flow runner returns its `Verdict:` block.

## 4. Report

Give the developer the verdict, the report path, and the FAIL / BLOCKED / OBS rows from the flow runner's `Verdict:` block, BLOCKED rows first with their fix leading. When it says `Stale: yes`, add that the plan's anchors no longer match the code and `plan-flow` can re-anchor it. Done when the developer has the verdict and the report path.
