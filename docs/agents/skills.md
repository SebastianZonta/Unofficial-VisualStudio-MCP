# Flow-testing skills (maintainer)

The skills under `skills/` ship with the server ([ADR-0014](../adr/0014-skills-ship-in-release.md))
and drive a developer's own app through the tools. Spec:
[flow-testing-skills](../../.scratch/flow-testing-skills/spec.md). Terms: GLOSSARY "Flow testing".

## Layout

One folder per skill, complete on its own (an installer or `npx skills` may install any
one):

- `skills/<name>/SKILL.md`: frontmatter `name`, `description`, and for user-only skills
  `disable-model-invocation: true` plus a description that starts "User-only".
- `skills/<name>/agents/openai.yaml`: Codex `interface` (display name, short description)
  and `policy.allow_implicit_invocation` (`false` for user-only skills).

| Skill | Invocation |
|---|---|
| `vs-debug-setup` | user-only |

## Changing a skill

1. Write per the `writing-for-agents` skill. A skill names tools and parameters but
   never restates TOOLS.md; it carries only the rules flow testing adds.
2. Name a tool, and its parameters, in a backticked call:
   `` `launch_process(sessionId, executablePath, timeoutMs: 10000)` ``. A `snake_case`
   tool may also stand bare (`` `list_instances` ``); a one-word tool (`attach`, `resume`,
   `detach`, …) is always a call, because the test can't tell a bare one from an ordinary
   word. Keep every other backticked word free of `snake_case` and of a lowercase word
   followed by `(`, and keep commas out of argument values.
   `DocsContractTests.Every_tool_and_parameter_a_skill_names_is_registered` fails on any
   name the server doesn't register, so a tool rename must update the skills too.
3. Run the [skills pass](#skills-pass) cases the change touches. Bar: every case PASS.

## Skills pass

Run whenever a skill changes, outside the E2E R1–R3 slices. It treats a temp copy of the
Sandbox as the developer's repo and drives the real VS through the skill as written.

Setup, once per pass:

1. Publish the server per [build.md](build.md) and register it in the agent client.
   The client must run at the same elevation as VS (TROUBLESHOOTING "Elevation mismatch").
2. Copy `Sandbox/` to a temp folder (without `bin/`, `obj/`, `.vs/`), `git init` it, and
   add an `AGENTS.md` holding one line. Open the copy's `Sandbox.slnx` in a VS 2026
   instance of its own and wait until it is idle.
3. Make the skill folder visible to the client (user-level skills folder or the copy's
   project skills folder), then work in the copy as the developer would.

Report to `.scratch/skills-runs/<yyyy-MM-dd-HHmm>.md` (gitignored) in the E2E report
format ([e2e.md](e2e.md#report)) with the skills under test in place of the change line.

| Id | Case | PASS when |
|---|---|---|
| setup | Type `/vs-debug-setup`. Answer: target `Sandbox`, default args `wait`, defaults elsewhere. Then type it again and change the default args to `wait 300`. | `docs/agents/vs-debug.md` records the `Sandbox.slnx` solution matched to the copy's VS instance, `bin/Debug/net10.0/Sandbox.exe` with its args, env and working dir, the build command, the elevation, and both locations. `AGENTS.md` holds the link line once, `.gitignore` the verdict folder once, after both runs. Every smoke item is green, with `launch_process` showing `mode: run`. Afterwards the Sandbox process is gone and `list_sessions` is empty. The second run left one setup doc holding `wait 300`. |
| setup-no-agent-file | Delete `AGENTS.md`, type `/vs-debug-setup`. | Setup asks whether to create `CLAUDE.md` or `AGENTS.md` before writing either, then writes the link line in the one chosen. |
