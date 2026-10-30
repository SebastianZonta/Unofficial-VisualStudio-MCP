# 01: setup-flow-testing skill

**What to build:** The repo gains a top-level skills directory holding the `setup-flow-testing` skill. A developer who places it in their user skill directory and runs it in their own repo is walked through setup in the main conversation: the skill explores the repo first (solutions, projects, existing `CLAUDE.md`/`AGENTS.md`), proposes a draft, confirms it, then writes the flow-testing doc (solutions with VS instance override, build command, named targets with `launch` exe path/args/working dir or `attach` process name, flows directory, report directory, same-elevation rule), an empty flow index, a gitignore entry for the report directory, and one pointer line in whichever agent-instruction file exists. This repo's `AGENTS.md` gains one line pointing at the skills directory. See [spec](../spec.md).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Skills directory exists with one folder per skill; `setup-flow-testing` is a valid skill folder
- [ ] Setup explores before asking and shows a draft before writing anything
- [ ] Run on a temp copy of `Sandbox/`: flow-testing doc, empty flow index, gitignored report directory (default inside the flows directory, or the one the developer picks), one pointer line
- [ ] Pointer goes into `CLAUDE.md` if it exists, else `AGENTS.md`; neither present → asks; never creates the other
- [ ] Rerunning setup updates the doc and pointer in place (still exactly one pointer line, one gitignore entry)
- [ ] Skill text uses glossary terms (VS instance, debug session, target process, flow plan, flow index)
- [ ] This repo's `AGENTS.md` has one line pointing at the skills directory
