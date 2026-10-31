---
name: debug-with-vs
description: "Ad-hoc debugging under the VS debugger: a debugger subagent binds one target, stops at one hypothesis at a time, and returns root cause with quoted evidence. Use on any 'why does X fail / debug this' request; run-flow stays for confirmed flow plans."
---

# Debug with VS

Find why the developer's app misbehaves, under the debugger. You are the orchestrator: a debugger subagent drives the MCP tools and the code; you hold the conversation. Tool output stays inside the subagent.

## 1. Gate the entry

You need symptom + repro trigger (`launch` args / `command` / `human` step) + target area (`solution`/`project`/file region). Ask for only what is missing and stop until answered. Done when you have all three.

## 2. Dispatch the debugger

Start one subagent with this prompt (reference paths are in this skill's base directory and the `flow-shared` skill folder next to it, each given as an absolute path):

```
You are the debugger. Read <absolute path to debugger.md> and follow it.
Symptom: <developer's symptom, verbatim>
Trigger: <launch args / command / human step>
Target area: <solution/project/file region>
Repo root: <absolute repo root>
Flow-testing doc: <doc path or "none">
Tactics: <absolute path to debugging-tactics.md in the flow-shared skill folder next to this skill's base directory>
```

Single turn; step 3 only relays its block, no resume. Done when the debugger has returned its block.

## 3. Report

Give the developer the `Diagnosis:`, `Where:`, `Evidence:`, and `Fix:`/`Need:` lines verbatim, plus the `Cleanup:` line. When it says BLOCKED, lead with the fix. When it names a code change, offer to apply it; do not apply unasked. Done when the developer has the diagnosis and the next step.
