---
name: plan-flow
description: "Write or update flow plans for one behaviour or an area of the developer's app: a planner subagent reads the code, every inferred expectation goes to the developer, then each plan is listed in the flow index. Use after setup-flow-testing, before run-flow."
disable-model-invocation: true
---

# Plan flow

Turn a flow (or an area of flows) the developer names in plain words into committed flow plans. You are the orchestrator: a planner subagent reads the code and writes the plan; you hold the conversation with the developer. Code reading stays inside the planner.

## 1. Check setup

Find the `Flow testing:` pointer line in the repo-root `CLAUDE.md` or `AGENTS.md`, the flow-testing doc it links, and the flow index that doc names. When any of the three is missing, tell the developer to run `setup-flow-testing` first and stop here, with the repo unchanged. Done when you have the doc path and the flow index exists.

## 2. Dispatch the planner

Start one subagent with the `Agent` tool, `model: "sonnet"`, and this prompt (fill the placeholders; the reference path is `planner.md` in this skill's base directory, given as an absolute path):

```
You are the flow planner. Read <absolute path to planner.md> and follow it.
Request: <the developer's words, verbatim>
Flow-testing doc: <doc path>
Repo root: <absolute repo root>
```

Keep the subagent's ID for step 4. Done when the planner returns a `Plan:` and `Index line` per plan, and `Questions:`.

## 3. Ask the developer

When the planner returned `Questions: none`, go to step 5. Otherwise put every question to the developer, each with the planner's proposed value so they can accept it in a word or give the intended one. The answers are the developer's: when they are unsure, the expectation stays proposed. Done when every question has the developer's answer.

## 4. Resume the planner

Send the answers to the same planner with `SendMessage` (to its ID), one line per question: question number, then the developer's answer verbatim. Done when the planner returns `Index: written`.

## 5. Report

Give the developer each plan path and index line, any expectation still proposed (that plan cannot run until it is confirmed), and the next step: commit the plans, then `run-flow <flow>` (or `run-flow <area>`). Done when the developer has the paths and the next step.
