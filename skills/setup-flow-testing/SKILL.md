---
name: setup-flow-testing
description: "Set up this repo for flow testing under the VS debugger: record solutions, build command, and targets, create the flow index, and point the agent-instruction file at it. Run once before plan-flow or run-flow."
disable-model-invocation: true
---

# Setup flow testing

Record, once per repo, how a flow runner reaches the developer's app under the debugger, so `plan-flow` and `run-flow` start from a written record. Runs in the main conversation with the developer: explore, present a draft, confirm, then write.

## 1. Explore

Read the repo before asking anything. Done when every item below has an answer from the repo itself:

- Every `.sln` / `.slnx` (skip `bin/`, `obj/`, `node_modules/`) and its projects; which projects are executables (`OutputType` `Exe`/`WinExe`, or `Microsoft.NET.Sdk.Web`).
- For each executable project: its build output path for the default configuration, `launchSettings.json` profiles (arguments, working directory, URLs), and its assembly name (the process name an `attach` target is found by).
- The build command the repo already uses (scripts, CI workflow, `README`); otherwise `dotnet build <solution>`, one per solution that owns a target, joined by `&&`.
- `CLAUDE.md` and `AGENTS.md` at the repo root: which exist, and whether either already holds a `Flow testing:` pointer line.
- An existing agent-docs directory (`docs/agents/` or similar) and a prior `flow-testing.md` from an earlier run of this skill. On a rerun, the prior doc is the starting draft.
- `.gitignore` at the repo root, and whether it already ignores a flow report directory.

## 2. Draft and confirm

Present the draft of the flow-testing doc, built from [flow-testing.md](./flow-testing.md) and filled from what exploration found, plus the paths below. Lead each open choice with the recommended answer so the developer can accept it in a word:

- **Solutions**: one row per solution that owns a target, named as `list_instances` reports it (the solution file name without extension) with its repo-relative path.
- **Targets**: one per executable the developer wants under test, named by a short target name that flow plans refer to. Each is either `launch` (executable path, default arguments, working directory falling back to the project directory; for a process the flow starts) or `attach` (process name; for a process already running, e.g. one hosted by IIS or started by an orchestrator).
- **Doc path**: `<agent-docs dir>/flow-testing.md` (default `docs/agents/flow-testing.md`).
- **Flows directory**: default `docs/flows/`, holding the flow index `INDEX.md` and one subdirectory per area.
- **Report directory**: ask once; default `<flows dir>/runs/`.
- **Pointer file**: see step 3. When neither `CLAUDE.md` nor `AGENTS.md` exists, ask which one to create.

Let the developer edit the draft. Done when the developer approves it.

## 3. Write

Write exactly these, each updated in place on a rerun:

1. **Flow-testing doc** at the approved path, from the approved draft. On a rerun the draft started from the prior doc, so the developer's hand edits carry over; flow plans and the flow index are left untouched.
2. **Flow index** at `<flows dir>/INDEX.md`, only when it does not exist yet:

   ```markdown
   # Flow index

   One `## <area>` heading per area, one line per flow plan under it: `- [<flow>](<area>/<flow>.md): <gist>`. Links only; plan content lives in the plan.
   ```

3. **Gitignore entry**: the report directory as one line in the root `.gitignore` (create the file if absent). Skip when an equivalent line is already present (leading `/` and trailing `/` do not matter); on a rerun with a new report directory, replace the old entry rather than adding a second.
4. **Pointer line** in the pointer file:
   - `CLAUDE.md` exists → edit it. Else `AGENTS.md` exists → edit it. Neither → create the one the developer chose. Never create the other file.
   - The line, appended at the end after a blank line (or replacing the existing `Flow testing:` line on a rerun, so there is always exactly one):

     ```markdown
     - Flow testing: read [<doc path>](<doc path>) before plan-flow or run-flow (solutions, build, targets, flow index, report directory).
     ```

Done when the doc, the flow index, one gitignore entry, and one pointer line are on disk.

## 4. Report

Tell the developer what was written (paths), that the same-elevation rule in the doc applies to every run, and that `plan-flow` is next: name a flow in plain words and the planner writes its flow plan. They can edit the doc directly; rerun this skill to add solutions or targets. Done when the developer has the written paths and the next step.
