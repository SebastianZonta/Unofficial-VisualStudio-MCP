# 01: Setup skill writes and proves the setup doc

**What to build:** A developer runs `/vs-debug-setup` in their repo and gets a setup doc
that every later flow-testing session reads. It records:

- the solution and the open VS instance holding it;
- each launchable target process (exe, default args, env, working dir);
- the build command;
- whether VS runs elevated;
- the plan and verdict locations.

Setup also writes one link line in whichever of `CLAUDE.md` or `AGENTS.md` exists
(asking when neither does) and the gitignore line for verdicts. It then proves the
facts with its smoke check. The skill is user-invoked only, wherever the client supports
a flag for that. A new docs-contract case ties every tool and parameter a skill names to
the registered tool surface. The maintainer skills doc starts here, with the first
skills-pass case. Spec: [Setup](../spec.md#setup-vs-debug-setup),
[Packaging](../spec.md#packaging-and-distribution-adr-0014). Trigger defaults,
dependencies and the second VS instance arrive in later tickets.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Running setup on a temp copy of the Sandbox writes the setup doc with the five facts above, matched to the VS instance that holds the Sandbox solution.
- [ ] The link line is added to the existing `CLAUDE.md`/`AGENTS.md` (updated in place on a re-run, never duplicated); with neither present, setup asks which to create.
- [ ] The gitignore line for the verdict folder is added once.
- [ ] Smoke check: `list_instances` finds the solution, the build is green, the target launches and reaches run mode, and setup stops what it launched. An `elevation-mismatch` is explained with the TROUBLESHOOTING fix.
- [ ] Re-running setup updates the setup doc in place.
- [x] The skill folder is self-contained, written per `writing-for-agents`, user-only via `disable-model-invocation` and Codex `allow_implicit_invocation: false`, with a description that says it is user-only.
- [x] Docs contract suite: a skill naming a tool or parameter the server doesn't register fails the test (proven by a deliberate rename).
- [ ] The maintainer skills doc exists (skill layout, how to change a skill, skills-pass case "setup" PASS), with one line each in AGENTS.md and the docs index.

## Comments

2026-10-27: Skill (`skills/vs-debug-setup/`), contract case
(`DocsContractTests.Every_tool_and_parameter_a_skill_names_is_registered`, proven by
renaming `list_instances` and `timeoutMs` in the skill: both fail) and
`docs/agents/skills.md` landed. Not yet run: the skills pass cases `setup` and
`setup-no-agent-file`. No VS was open and the agent shell was not elevated, so the
live boxes above stay open until a session at VS's elevation runs them.
