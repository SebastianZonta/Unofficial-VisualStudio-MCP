# Flow-testing skills

Status: needs-info

Blocked on probes P1–P4 (see [Capabilities and probes](#capabilities-and-probes)).
Vocabulary: [GLOSSARY.md](../../GLOSSARY.md#flow-testing). Decisions:
[ADR-0013](../../docs/adr/0013-flow-runs-borrow-open-vs.md),
[ADR-0014](../../docs/adr/0014-skills-ship-in-release.md). Research:
[01-skill-distribution](research/01-skill-distribution.md).

## Problem Statement

A developer installs VsDebugMcp and gets 21 debugger tools, but turning "make sure
checkout still works" into a debugger session is entirely on them, every time. Which
VS instance holds the solution, which target process to launch and with what
arguments, how to make the flow happen (an HTTP call, a click), where to break, what
must hold there, and how to report it all get rediscovered by each new agent session.
Nothing is written down, so a check can't be repeated after the next change, and the
developer's own standards ("no unhandled exceptions, ever") live only in their head.
Flows that continue in background jobs, event handlers or a worker process are
harder still: the interesting code runs after the request has returned.

This repo already runs a plan → run → verdict loop (the E2E pass), but only for
maintainers checking the server against the Sandbox. A developer gets no equivalent
for their own app.

## Solution

Three skills ship with the server and are installed by the same one-line installer:

- **`vs-debug-setup`** (user-invoked only, once per repo) records the facts every flow
  run needs in a setup doc, interviews the developer for their **flow standards**,
  links the setup doc from the repo's `CLAUDE.md` or `AGENTS.md`, and proves the facts
  with one smoke check.
- **`vs-debug-plan-flow`** turns "test the checkout flow" into a **flow plan**. It reads
  the code to chart the flow, does a scouting run under the debugger, asks the
  developer only what the code can't tell (intent), and writes one committed file per
  flow: its use cases as ordered triggers and checkpoints.
- **`vs-debug-run-flow`** executes a flow plan against the developer's open VS
  instance(s), one use case at a time, and writes a **verdict** (PASS/FAIL with quoted
  tool evidence per checkpoint) that it never "fixes" by editing the plan.

Flow testing covers the developer's local .NET app, including in-process background
work and out-of-process workers (through a second open VS instance), and use cases
that need a person in the middle (an approval in an admin UI).

## User Stories

Install

1. As a developer who just installed VsDebugMcp, I want the one-line installer to also install the flow-testing skills into my agent, so that there is no second install step.
2. As a developer, I want the installed skills to match my installed server version, so that a skill never calls a tool or parameter my server lacks.
3. As a developer who prefers `npx skills`, I want to install the same skills from this repo with it, so that I can use my usual channel.
4. As a developer re-running the installer, I want it to replace only the three `vs-debug-*` skills, so that my other skills are untouched.

Setup

5. As a developer, I want to run setup once in my repo, so that every later agent session knows how to build, launch and reach my app without rediscovering it.
6. As a developer, I want setup to run only when I ask for it, so that no agent edits my `CLAUDE.md`, `AGENTS.md` or `.gitignore` on its own.
7. As a developer, I want setup to add one link line to whichever of `CLAUDE.md` or `AGENTS.md` already exists, so that any agent finds the setup doc without me pasting instructions.
8. As a developer whose repo has neither file, I want setup to ask me which one to create, so that it never picks for me.
9. As a developer, I want setup to find the open VS instance holding my solution, so that runs bind the right IDE.
10. As a developer with several runnable projects, I want setup to record each target process's executable, arguments, environment and working directory, or the process name to attach to, so that a run can start or find it.
11. As a developer, I want setup to record the build command, so that every run binds breakpoints to fresh symbols.
12. As a developer of a web API, I want setup to record the base URL and a readiness check, so that command triggers wait until my app listens.
13. As a developer whose API needs a token, I want setup to record only the name of the env var holding it, so that no secret lands in a committed file.
14. As a developer whose VS runs elevated, I want setup to detect an elevation mismatch and tell me the fix, so that I don't chase an empty instance list.
15. As a developer whose flows need a database or a message broker, I want setup to record each dependency with a readiness check, so that a run blocks with a named cause instead of failing confusingly.
16. As a developer whose flows reach a worker process, I want setup to record which VS instance holds which process, so that a run can bind both.
17. As a developer, I want setup to prove the recorded facts with one smoke check, so that a broken setup shows up now rather than mid-run.
18. As a developer, I want to state the criteria that hold for every flow once, as flow standards, so that I don't repeat them in every plan.
19. As a developer, I want setup to rewrite vague criteria into checkable ones with me, or leave them out, so that every standard can actually pass or fail.
20. As a developer, I want the plan and verdict locations recorded in the setup doc with sensible defaults, so that I can move them to fit my repo.
21. As a developer, I want to re-run setup when my solution, ports or processes change, so that the setup doc stays true.

Plan

22. As a developer, I want to say "test the checkout flow" in plain words and have the planner start, so that I needn't remember a command.
23. As a developer without a setup doc, I want the planner to stop and tell me to run setup, so that it never guesses repo facts.
24. As a developer, I want the planner to read the code first and collect the flow's references (entry point, members it passes, branches, throw sites), so that it asks me only what the code can't tell.
25. As a developer, I want to be asked only about intent (the right outcome, the triggering input, which use cases matter), so that planning stays short.
26. As a developer, I want the planner to do a scouting run before asking for expectations, so that I see the route really exists and its breakpoints bind.
27. As a developer, I want values seen while scouting labelled observed and never written as expectations unless I confirm them, so that today's bugs don't become expected behaviour.
28. As a developer, I want one flow plan per flow with one section per use case, so that the happy path and its error paths live together.
29. As a developer, I want checkpoints anchored by file, enclosing member and a snippet rather than line numbers, so that ordinary edits don't break the plan.
30. As a developer, I want flow testing to need no marker comments in my code unless I ask for them, so that it doesn't touch my sources.
31. As a developer, I want reached, not-reached, order, values, hit-count, exception and outcome checks, so that I can say precisely what "works" means.
32. As a developer of an API with background jobs (a hosted Quartz job, an `IHostedService`, an in-process domain-event handler), I want deferred checkpoints with a wait budget, so that decoupled work is part of the flow.
33. As a developer, I want a checkpoint to capture a value (an order id) and later checkpoints to count only hits carrying it, so that an unrelated job run can't make a use case pass.
34. As a developer with a scheduled job, I want a kick in the plan, so that a run doesn't wait for the schedule.
35. As a developer with a separate worker process, I want checkpoints in that process too, so that the whole flow is checked.
36. As a developer, I want a human trigger in the middle of a use case (approve the order in the admin UI), so that approval flows are testable.
37. As a developer, I want a use case to waive a named flow standard with a reason, so that an expected exception doesn't fail a legitimate use case.
38. As a developer, I want the planner to offer moving a rule that applies to every flow into flow standards, so that it isn't buried in one plan.
39. As a developer, I want the planner to update an existing flow plan in place (refresh anchors, keep ids, ask only about changed behaviour), so that plans stay current cheaply.
40. As a developer, I want the planner to offer running the plan when it finishes, so that I see a verdict right away.
41. As a developer, I want flow plans committed, so that teammates and later sessions reuse them.

Run

42. As a developer, I want a flow run to use my already-open VS, so that it starts in seconds.
43. As a developer, I want a flow run to leave my VS as it found it (its own breakpoints removed, exception settings it changed reset, my breakpoints untouched), so that flow testing never disturbs my IDE.
44. As a developer debugging something myself, I want a flow run to stop BLOCKED rather than take over my VS, so that my session is safe.
45. As a developer, I want each flow run to build first, so that checkpoints bind to the current code.
46. As a developer, I want a flow run to check dependency readiness first and BLOCK naming the service that's down, so that I know what to start.
47. As a developer, I want use cases run one at a time in plan order, or only the ones I name, so that results are attributable.
48. As a developer, I want command triggers run in the background with their output collected afterwards, so that a request paused at a checkpoint never reports a spurious timeout.
49. As a developer, I want to be asked once per flow run for attach consent, naming the process and target PID, so that attaching stays a conscious act.
50. As a developer at a human trigger, I want my targets running, the next checkpoints armed and the instruction filled with captured values ("approve order 1042"), so that my action is observed.
51. As a developer, I want "done" at a human trigger to be the next armed checkpoint hitting or me saying done, whichever comes first, so that I don't have to report the obvious.
52. As an agent that just changed code, I want to run a flow plan on my own, doing every use case that needs no human first and asking before each human-gated one, so that I can verify without blocking on the developer early.
53. As a developer, I want a check that fails to be recorded as FAIL while the use case continues, so that later checkpoints still give evidence.
54. As a developer, I want a stop on my own breakpoint during a run recorded as an observation and resumed, so that my breakpoints never fail a run.
55. As a developer, I want an unexpected exception reported as FAIL against the standard it breaks, so that the verdict says which rule was broken.
56. As a developer, I want a stale anchor (snippet gone or ambiguous) to BLOCK its checkpoint with "re-plan", never a guessed line, so that a verdict never rests on the wrong code.
57. As a developer, I want a missing second VS to BLOCK the out-of-process checkpoints with the instruction to open it, so that the rest of the flow still runs.
58. As a developer, I want a verdict file listing every row by id, the standards checked, and every waiver, so that an old verdict still makes sense after the standards change.
59. As a developer, I want redacted checkpoints to show only match or mismatch in the verdict, so that sensitive values stay out of it.
60. As a developer, I want verdicts gitignored, so that run output never lands in a commit.
61. As a developer, I want the run never to edit the plan or my code and to offer next steps (debug the FAIL, re-plan stale anchors), so that I stay in control of fixes.

Maintain

62. As a maintainer, I want a contract test that fails when a skill names a tool or parameter the server doesn't register, so that a rename can't silently break developers' runs.
63. As a maintainer, I want the CI install guard to fail when the release zip or an installer drops a skill, so that every release carries all three.
64. As a maintainer, I want a skills pass that treats the Sandbox as the developer's repo, so that I can verify the skills end to end after changing one.
65. As a maintainer, I want the Sandbox to gain an HTTP scenario with an in-process background loop and a second consumer process, so that command triggers, deferred checkpoints, captures and out-of-process legs are exercised.
66. As a maintainer, I want human triggers verified in phase H, so that the human handover is checked with a real person.
67. As a maintainer, I want a maintainer doc for the skill layout, the skills pass and how to change a skill, so that skill changes follow a known path.
68. As a developer reading the README, I want a Skills section, so that I learn the skills exist and how to start.

## Implementation Decisions

### Packaging and distribution ([ADR-0014](../../docs/adr/0014-skills-ship-in-release.md))

- Three self-contained skill folders live in this repo and ship inside the release zip.
  Each holds its `SKILL.md`, written per the `writing-for-agents` skill, plus a Codex
  `agents/openai.yaml` (display name, short description, implicit-invocation policy).
  Each folder is complete on its own: an installer or `npx skills` may install any one.
- The installer gains a skills step: every run that wires a client also installs the
  three skills into that client's user-level skill folder, replacing only the
  `vs-debug-*` folders it owns. Which folders get written, and how the installer avoids
  a client that reads both shared folders listing a skill twice, is decided by probe P1.
  Claude Desktop (upload only) gets no skills; Windsurf waits on P1.
- `npx skills add` against this repo's skills folder is documented as an alternative.
  The Claude Code plugin marketplace is deferred.
- **Invocation.** `vs-debug-setup` is user-invoked only (`disable-model-invocation:
  true`; Codex `allow_implicit_invocation: false`). Elsewhere its description says it
  is user-only, pending P2. `vs-debug-plan-flow` and `vs-debug-run-flow` are
  model-invocable: their descriptions name the trigger phrases ("test the X flow",
  "run the X flow plan").
- Skills name tools and parameters but never restate TOOLS.md. They tell the agent to
  load the vs-debug toolset, and they carry only the rules flow testing adds.

### Setup (`vs-debug-setup`)

- Writes three things in the developer's repo:
  - the **setup doc** (`docs/agents/vs-debug.md`);
  - **flow standards** (`docs/agents/flow-standards.md`, linked from the setup doc);
  - a gitignore line for the verdict folder.
- Adds one link line to `CLAUDE.md` if it exists, else `AGENTS.md`, else asks which
  to create. An existing link is updated in place, never duplicated.
- The setup doc records:
  1. the solution(s) and the open VS instance holding each, matched by solution name
     (two instances holding the same solution: ask);
  2. per target process, either launch facts (built exe, default args, env, working
     dir) or the process name to attach to; for flows spanning processes, which VS
     instance holds which process;
  3. the build command run before every flow run;
  4. trigger defaults: base URL, readiness check (port or health URL), names of env
     vars holding tokens, never their values;
  5. whether VS runs elevated (an `elevation-mismatch` from discovery is explained
     with the TROUBLESHOOTING fix);
  6. dependencies (database, broker, cache), each with a readiness check;
  7. locations: plans in `docs/agents/flows/<flow>.md` (committed), verdicts in
     `docs/agents/flows/runs/<flow>-<yyyy-MM-dd-HHmm>.md` (gitignored), and the
     default deferred wait budget.
- **Smoke check.** `list_instances` finds the solution, the build is green, each
  launchable target launches and reaches run mode (hosts also pass their readiness
  check), and setup then stops what it launched.
- **Flow standards** are interviewed and kept only when they map to a checkpoint check
  or an outcome check. Examples: no unhandled exception, no first-chance `SqlException`,
  every HTTP trigger returns non-5xx, never reaches `Debug.Fail`. Vague criteria are
  rewritten with the developer or left out. No timing standards, because breakpoints
  distort elapsed time.
- There is no fourth skill. Standards are edited by re-running setup or by hand.

### Flow plan format

- One file per flow, starting with:
  - a `Format: 1` line;
  - the flow name and its target process(es);
  - a pointer to flow standards (referenced, never copied);
  - a references block (entry point, members, branches, throw sites).
- Each use case is one section holding an ordered table of trigger rows and checkpoint
  rows, plus its expected outcome and waivers. Trigger ids are `UC<n>-T<k>`, checkpoint
  ids `UC<n>-<m>`. Ids are stable: re-planning keeps them, and new use cases are
  appended. This shape comes from the grilling session and is trimmed to its decisions:

  ```markdown
  ## UC2 Order needs approval
  | Id | Step | Checks |
  | UC2-T1 | command `POST {baseUrl}/orders` (amount over limit) | outcome 202 |
  | UC2-1 | OrderService.cs · Submit · `if (total > _limit)` | reached; capture `order.Id` as `orderId` |
  | UC2-T2 | human: approve order {orderId} in the admin UI | — |
  | UC2-2 | ApprovalHandler.cs · Handle · `await _bus.Publish(` | deferred 120 s; `cmd.OrderId == {orderId}` |
  Waivers: none
  ```

- **Triggers:** `launch` (the debugger starts the target with args/env/cwd), `command`
  (a shell command against the running target, e.g. HTTP, `dotnet test --filter`, a
  queue message), or `human`. A **kick** is a command trigger for scheduled work.
- **Anchors** are file · enclosing member · a short unique snippet, resolved to a line
  at every flow run. A missing or ambiguous anchor blocks that checkpoint. Marker
  comments are offered only on request.
- **Checks:**
  - reached (`lastHit` equals the resolved anchor);
  - not reached (no hit before the use case ends);
  - order (within one target process only);
  - values (read-only `read_variables`/`evaluate`, comparing value not decoration);
  - hit count;
  - exception (an exception break of a named type);
  - outcome (status code, exit code or stdout of a trigger).
- **Per-checkpoint attributes:**
  - target process (defaults to the use case's main one);
  - deferred (hits after the trigger's outcome, within a wait budget);
  - captures (a named value read at this checkpoint);
  - redacted (the verdict shows only match or mismatch).
- A deferred checkpoint without a correlating capture states why (e.g. "singleton
  job").
- A use case is **human-gated** when any trigger is human or it needs attach consent.

### Plan (`vs-debug-plan-flow`)

1. No setup doc: stop with one line telling the developer to run `vs-debug-setup`.
2. Read the code to chart the flow and collect its references and candidate anchors.
3. **Scouting run** (setup green, trigger not human): put logpoints on candidate
   anchors, trigger once, and show which hit, in what order and with what values.
   Values are labelled observed and become expectations only when the developer
   confirms them.
4. One short round of questions on intent only: the right outcome per use case, the
   triggering input, which use cases matter, waivers.
5. Write the plan. When an existing plan exists, update it in place (refresh anchors
   and references, keep ids, ask only about changed behaviour). Offer to move any rule
   stated for every flow into flow standards.
6. Offer to run the plan. The run skill needs only the setup doc, flow standards and
   the plan, so the same or a fresh session works.

### Run (`vs-debug-run-flow`) ([ADR-0013](../../docs/adr/0013-flow-runs-borrow-open-vs.md))

- **Preflight:**
  1. Setup doc present, else point to setup.
  2. Build.
  3. Dependency readiness: a service that's down blocks the use cases needing it.
  4. Resolve every anchor.
  5. Find each needed VS instance by solution and attach with `instanceOverride`.
  6. An instance already debugging (not `design`) is BLOCKED; a missing second instance
     blocks its checkpoints with the instruction to open it.
  7. Attach consent, if any use case needs it, is asked once, naming the process and
     target PID.
- **VS state.** Never start or close VS. On every exit path, detach (which deletes the
  session's own breakpoints) and reset each exception setting the run changed (ADR-0011:
  detach does not). Foreign breakpoints are never touched.
- **Order.** Use cases run one at a time in plan order, or only the named ones. When an
  agent started the run on its own, ungated use cases go first, then the run asks
  before each human-gated one ("ready for UC2? midway it needs you to approve an
  order").
- **Targets.**
  - A target needed by command or human triggers is launched (or attached) once per
    flow run and reused across use cases.
  - A `launch` trigger always starts a fresh target, so a reused one is stopped first
    (one target process per debug session).
  - Only the current use case's checkpoints are armed.
  - The run stops the processes it launched at the end, never ones it attached to.
- **Command triggers** start in the background with output redirected to a file. The
  run drives the debugger through the use case, then reads the output for the outcome
  check. The client timeout is set high enough that a pause never shows as a failed
  request (P3).
- **Human triggers:**
  1. Ensure no target process is stopped.
  2. Arm the following checkpoints.
  3. Print the instruction with captures filled in.
  4. Wait in `resume` calls. "Done" is the next armed checkpoint hitting or the human
     saying done, whichever comes first (P4).
  5. The next checkpoint's deferred budget starts when the human trigger is done.
- **Waiting.** `resume` accepts 1–60 s per call and already waits for the next stop
  while the target runs. Budgets beyond 60 s (deferred checkpoints, human triggers)
  are consecutive `resume` calls. Launch and resume use 30 s unless the plan says
  otherwise, as in the E2E runner rules.
- **How checks are carried out:**
  - Captures are read at their checkpoint, then substituted as literals into later
    conditions (native, read-only).
  - Hit counts use a logpoint when uncorrelated. A correlated hit count uses a
    conditional breakpoint that the run counts and resumes, because logpoints take no
    condition.
  - Exception checks and standards set first-chance breaking before launch and reset
    afterwards.
  - Not-reached checkpoints are breakpoints whose hit is a FAIL.
- **Statuses:**
  - A failed check is FAIL and the use case continues to its end.
  - A reached checkpoint never hit before the use case ends is FAIL.
  - A stop on a foreign breakpoint is OBS and the run resumes.
  - An exception nobody expected is FAIL against the standard it breaks, unless
    waived.
  - Retry once on `vs-busy` or a timeout; never retry a check.
  - A target that never starts blocks every checkpoint of that use case.
  - A stale anchor blocks its checkpoint with "re-plan".
- **Flow standards** are checked in every use case except where waived. The verdict
  quotes the standards it checked and lists every waiver.
- **After the verdict:** report the verdict, counts, every FAIL and BLOCKED with
  evidence, and the path. Offer next steps. Never edit the plan to match what was
  observed, and never touch the developer's code.
- **Verdict format** (adapted from the E2E report):

  ```markdown
  # Flow run <flow> <yyyy-MM-dd-HHmm>
  Flow plan: <path> · Use cases: all | <ids> · MCP binary: <path + write time> · VS: <version>
  Verdict: PASS | FAIL   (PASS n · FAIL n · BLOCKED n · OBS n)
  Standards checked: <quoted> · Waivers: <id: standard (reason)>
  ## Failures and blocked
  | Id | Expected | Actual (evidence) |
  ## Observations
  | Id | What happened |
  ## All rows
  | Id | Status | Evidence |
  ```

### Safety

- Launch is preferred over attach. Attach consent is asked per flow run and never
  stored in a plan or the setup doc (ADR-0004).
- No `set_variable` in v1. Use cases must be reachable by real triggers, and
  per-call consent (ADR-0012) can't be given unattended.
- Plans and the setup doc hold env var names, never secret values. Verdicts quote what
  the debugger showed, since they are local and gitignored; a redacted checkpoint
  shows only match or mismatch.

### Docs in this repo

- README gains a Skills section (what the three do, installed by the one-liner).
- INSTALL gains a mention of the skills and the folders the installer writes.
- A new maintainer doc in the agent docs covers the skill layout, the skills pass and
  how to change a skill.
- AGENTS.md and the docs index each gain one line.
- The E2E marker table is updated for the new Sandbox scenarios.

### Capabilities and probes

Proven:

- Conditions, hit counts and logpoints: conditional-breakpoints spec, E2E P4.
- Two VS instances each holding a target process in break mode at once: E2E P10 M4.
- `resume` while running waits for the next stop, and the per-call budget is 1–60 s:
  live bridge and argument validation.
- Exception breaks carry the type: ADR-0011, E2E P8.
- `launch_process` with args, env and cwd, no consent: ADR-0009, ECHO.
- `elevation-mismatch` surfaced by discovery: TROUBLESHOOTING.
- Skill folders per client, `npx skills` subfolder installs, and MCP prompts being
  user-only: [research 01](research/01-skill-distribution.md).
- The user-only flag in Claude Code and Codex: research 01, prior-art skills.

Unproven (each needs a probe before the work that depends on it):

| Probe | Question | How | Decides |
|---|---|---|---|
| P1 | Does a same-name skill in both `~/.claude/skills` and `~/.agents/skills` list twice in opencode, Cursor and Copilot? Does Windsurf load skills, and from where? | Spike: a dummy skill in both folders, listed in each client | Installer target folders |
| P2 | Do opencode, Cursor, Copilot and Gemini CLI honour any user-only flag? | Spike: a dummy skill with each candidate flag, then try model invocation | The setup skill's invocation promise per client (until proven: description says user-only) |
| P3 | Does a command trigger started in the background stay pending while the target sits at a checkpoint, then complete after resume, with output collected from a file, from a non-elevated agent shell against a localhost listener that needs no URL reservation? | Spike on the new Sandbox `serve` scenario | The command-trigger mechanics and the Sandbox listener choice |
| P4 | Does text the agent prints before a blocking `resume` reach the human while the call is pending? | Phase H human trigger, once per client | Human handover: if not shown mid-turn, the run ends its turn after printing and waits for "done" before resuming |

## Testing Decisions

- **What makes a good test:** it drives the skills the way a developer would and
  asserts only on what a developer sees: the files setup writes, the flow plan, the
  verdict, and the state of VS afterwards. It never asserts on skill wording.
- **Seam 1 (primary): the skills pass.** A separate pass outside the R1–R3 slices,
  run whenever a skill changes. It treats a temp copy of the Sandbox as the
  developer's repo and drives the real VS through the real installed skills. Cases:
  1. setup writes the setup doc, flow standards, link line and gitignore line, and its
     smoke check is green;
  2. plan the `inspect` flow (scouting run included), then run it: PASS;
  3. an edited snippet makes a stale anchor: BLOCKED "re-plan";
  4. a waived standard appears in the verdict;
  5. the `serve` scenario's HTTP command trigger, in-process background loop (deferred
     checkpoint with a capture) and second consumer process (out-of-process leg across
     two VS instances): PASS;
  6. a foreign breakpoint along the route: OBS;
  7. VS is left as found (no session breakpoints, exception settings reset).
  Human triggers are verified in phase H. Prior art: the E2E pass (`e2e.md`,
  `e2e-runner.md` rules, report format) and P10's two-VS setup.
- **Seam 2: the docs contract suite.** A new case checks that every tool name and
  parameter a skill mentions is registered on the server. Prior art: the existing
  tool-row and parameter cases.
- **Seam 3: the CI install guard.** Its drift check extends to: the release zip
  carries the skills folder, and both installers install every skill folder. Prior
  art: the existing installer/doc drift step.
- **Sandbox additions:** a `serve` scenario (localhost HTTP endpoint, listener chosen
  by P3) that queues work for an in-process background loop, and a second Sandbox
  process that consumes it. Each gets `// E2E:<NAME>` markers, added to the marker
  table. Existing scenarios and markers are unchanged.
- No new `FakeDebugBridge` or `AuditE2ETests` cases. The feature adds no server
  behaviour, and the coverage statement in the definition of done says so.

## Out of Scope

- Docker, remote and cloud target processes; non-.NET targets.
- Writing target state (`set_variable`) to force a branch.
- Timing or performance standards.
- Ordering checks across processes (correlation through captures replaces them).
- Starting or closing VS instances, or dependencies (database, broker), from a flow
  run.
- A Claude Code plugin marketplace; Claude Desktop skills (upload only).
- A fourth skill for editing flow standards.
- Running flow runs in CI.
- Any change to the server's tool surface.

## Further Notes

- Inspired by Matt Pocock's `setup-matt-pocock-skills` (one-time repo setup linked
  from `CLAUDE.md`/`AGENTS.md`), `to-spec` and `wayfinder`. The planner borrows
  wayfinder's charting of routes and references, but not its tickets.
- **Known risk:** [e2e-2026-10-24 issue 10](../e2e-2026-10-24/issues/10-logpoint-hit-surfaces-as-stop.md)
  (needs-info). On multi-threaded code a logpoint occasionally stops (1 in 3 runs) or
  logs `(unavailable)` (1 in 60 hits). Scouting runs and logpoint hit counts treat a
  logpoint stop as OBS and resume, and count an `(unavailable)` entry as a hit with no
  value.
- After a flow run ends, VS may briefly show a phantom `other<N>` twin of a removed
  breakpoint (same issue); it clears with the owned removal and is not a foreign stop.
- "Runner", "spec" and "E2E" keep their existing meanings. Flow testing has its own
  terms, kept in GLOSSARY.
