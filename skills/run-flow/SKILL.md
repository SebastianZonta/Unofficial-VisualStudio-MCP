---
name: run-flow
description: "Run a flow plan against the VS debugger: a flow runner subagent builds, binds the targets, fires the trigger, checks every checkpoint, writes a run report, and returns the verdict. Use after plan-flow, when the developer names a flow to run."
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

Keep the subagent's ID. When the flow runner returns `Waiting: <instruction>` (a `human` trigger), put the instruction to the developer verbatim and ask them to say when it is done; the debugger waits meanwhile. Resume the same flow runner with `SendMessage` (to its ID): `done`, or `could not: <the developer's reason>`. Done when the flow runner returns its `Verdict:` block.

## 4. Report

Give the developer the verdict, the report path, and the FAIL / BLOCKED / OBS rows from the flow runner's `Verdict:` block, BLOCKED rows first with their fix leading. When it says `Stale: yes`, add that those checkpoints' anchors no longer match the code, and offer to send the planner back to re-anchor them; then stop and wait for the developer's answer. Never re-anchor on your own. Done when the developer has the verdict and the report path, and, for a stale plan, has answered the offer.

## 5. Re-anchor (stale plan, developer said yes)

Start one subagent with the `Agent` tool, `model: "sonnet"`, and this prompt (the reference path is `planner.md` in the `plan-flow` skill folder next to this skill's base directory, given as an absolute path):

```
You are the flow planner. Read <absolute path to planner.md> and follow it.
Request: re-anchor <plan path>, stale checkpoints <N, ...>
Flow-testing doc: <doc path>
Repo root: <absolute repo root>
```

Keep the subagent's ID. When it returns questions, put each to the developer with its proposed value and resume the same planner with `SendMessage`, one line per question: number, then the developer's answer verbatim, until it returns `Index: written`. Tell the developer what moved, anything `Still proposed:` (the plan cannot run until it is confirmed), and offer to rerun the flow (steps 2 to 4). Done when the developer has the re-anchored plan and the rerun offer.
