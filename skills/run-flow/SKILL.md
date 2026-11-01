---
name: run-flow
description: "Run flow plans against the VS debugger: a flow runner subagent per flow binds the targets, fires the trigger, checks every checkpoint, writes a run report, and returns the verdict. Use after plan-flow, when the developer names a flow, an area, or all to run."
---

# Run flow

Run the flow plans the developer names and hand back one verdict per flow. You are the orchestrator: a flow runner subagent per flow drives the debugger and writes the report; you resolve the request and relay the results. Debugger output stays inside the flow runners.

## 1. Resolve the flows

Find the `Flow testing:` pointer line in the repo-root `CLAUDE.md` or `AGENTS.md` and the flow-testing doc it links; when either is missing, tell the developer to run `setup-flow-testing` first and stop. Resolve the request in the flow index the doc names: a flow name matches the exact link text of a line (one flow); else an area matches the exact text of a `## <area>` heading (every flow under it, in index order); else `all` means every flow in the index, in index order. When nothing matches, tell the developer the name is not in the flow index, list the flow names and areas that are, and stop; the developer picks. Done when you have the plan paths.

## 2. Check the plans

Read each plan. A plan with any expectation (or the expected outcome) marked *proposed* is not run: list those expectations to the developer and tell them to confirm them through `plan-flow`. When no plan is left, stop. Done when every plan left to run has only *confirmed* expectations.

## 3. Build once, then dispatch the flow runners

Build once: run the doc's build command in the repo root. When it exits non-zero, stop and tell the developer `build fails: \`<first error line>\`` — no flow runner is started. Done when the build is green; every flow runner then assumes current exes and only checks exe existence (runner.md step 2).

A flow's solution set is the solutions, in the doc's Targets table, of every target its checkpoints name. Two flows may run at once only when their solution sets share no solution: one VS instance serves one flow runner at a time. Go through the flows in order: start each one whose solution set is disjoint from every running flow's (counting the ones just started), and queue the rest; start those of one pass in a single message, so they run at once. Each time a flow runner returns its `Verdict:` block, start the queued flows that are now disjoint, in order.

For each, pick the report path: `<report dir>/<flow>-<run tag>.md`, where the report dir is a location the developer named in this request, else the doc's run report directory, and the run tag is the local time as `YYYYMMDD-HHMMSS`. Start one subagent with this prompt (the reference path is `runner.md` in this skill's base directory, given as an absolute path):

```
You are the flow runner. Read <absolute path to runner.md> and follow it.
Flow plan: <plan path>
Flow-testing doc: <doc path>
Repo root: <absolute repo root>
Report: <report path>
Tactics: <absolute path to debugging-tactics.md in the flow-shared skill folder next to this skill's base directory>
```

Keep each subagent with its flow. When a flow runner returns `Waiting: <instruction>` (a `human` trigger), name its flow and put the instruction to the developer verbatim and ask them to say when it is done; the debugger waits meanwhile. Resume the same flow runner by continuing its session: `done`, or `could not: <the developer's reason>`. Done when every flow runner has returned its `Verdict:` block.

## 4. Report

For each flow, in the order the flow runners returned, give the developer its verdict, the report path, and the FAIL / BLOCKED / OBS rows from its `Verdict:` block, BLOCKED rows first with their fix leading; then name the flows not run (step 2). For each flow whose block says `Stale: yes`, add that those checkpoints' anchors no longer match the code, and offer to send the planner back to re-anchor them; then stop and wait for the developer's answer. Never re-anchor on your own. Done when the developer has each verdict and report path, and, for a stale plan, has answered the offer.

## 5. Re-anchor (stale plan, developer said yes)

For each stale flow the developer said yes to, one at a time, start one subagent with this prompt (the reference path is `planner.md` in the `plan-flow` skill folder next to this skill's base directory, given as an absolute path):

```
You are the flow planner. Read <absolute path to planner.md> and follow it.
Request: re-anchor <plan path>, stale checkpoints <N, ...>
Flow-testing doc: <doc path>
Repo root: <absolute repo root>
Tactics: <absolute path to debugging-tactics.md in the flow-shared skill folder next to this skill's base directory>
```

When it returns questions, put each to the developer with its proposed value and resume the same planner by continuing its session, one line per question: number, then the developer's answer verbatim, until it returns `Index: written`. Tell the developer what moved, anything `Still proposed:` (the plan cannot run until it is confirmed), and offer to rerun the re-anchored flows (steps 2 to 4). Done when the developer has the re-anchored plans and the rerun offer.
