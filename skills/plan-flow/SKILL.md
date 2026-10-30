---
name: plan-flow
description: "Write or update a flow plan for one behaviour of the developer's app: a planner subagent reads the code, every inferred expectation goes to the developer, then the plan is listed in the flow index. Use after setup-flow-testing, before run-flow."
disable-model-invocation: true
---

# Plan flow

Turn a flow the developer names in plain words into a committed flow plan. You are the orchestrator: a planner subagent reads the code and writes the plan; you hold the conversation with the developer. Code reading stays inside the planner.

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

Keep the subagent's ID for step 4. Done when the planner returns `Plan:`, `Index line` (or `Index: written`), and `Questions:`.

## 3. Ask the developer

When the planner returned `Questions: none`, go to step 5. Otherwise put every question to the developer, each with the planner's proposed value so they can accept it in a word or give the intended one. The answers are the developer's: when they are unsure, the expectation stays proposed. Done when every question has the developer's answer.

## 4. Resume the planner

Send the answers to the same planner with `SendMessage` (to its ID), one line per question: question number, then the developer's answer verbatim. When it returns new questions, go back to step 3. Done when the planner returns `Index: written`.

## 5. Report

Give the developer the plan path, the index line, any expectation still proposed (the plan cannot run until it is confirmed), and the next step: commit the plan, then `run-flow <flow>`. Done when the developer has the path and the next step.
