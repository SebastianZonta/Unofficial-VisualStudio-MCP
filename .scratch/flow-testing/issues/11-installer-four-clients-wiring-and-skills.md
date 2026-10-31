# 11: Installer: reliable MCP wiring and skill copies for opencode, Codex, Claude Code, Cursor

**What to build:** Both `install.ps1` and `install.sh` register the server in, and copy the flow-testing skills for, each of opencode, Codex, Claude Code and Cursor that the client probe detects. They also fix the wiring bugs the issue 09 pass found. See [spec](../spec.md).

**What happened (issue 09):** `install.ps1 -ConfigOnly` printed `Skipping C:\Users\<user>\.claude.json: not valid JSON (... contains the duplicated keys 'C:/Users/<user>/source/repos' and 'c:/Users/<user>/source/repos'.)`. Claude Code writes one `projects` key per cwd, as typed, so drive-letter case differences are common on Windows. `ConvertFrom-Json` (into a PSCustomObject, on 5.1 and 7) rejects keys that differ only by case, so Claude Code was never wired. The skills still installed.

**Blocked by:** none

**Status:** done

Spec change: this reverses the Out of Scope line "opencode or other clients' skill directories" and widens the Implementation Decisions installer line (skills went to the Claude skill directory only). Update both spec lines, with an ADR if the trade-off warrants one (definition-of-done step 4).

MCP wiring:
- [x] Parse JSON configs without the case-insensitive collision (e.g. `-AsHashtable` on PowerShell 7, or a targeted edit of the server section only), keeping the strict invalid-JSON skip. Covers `.claude.json`, `opencode.json` and Cursor `mcp.json`.
- [x] Codex: register the server in `~/.codex/config.toml` (`[mcp_servers.vs-debug]`, `command`, `args`), editing only that table and leaving the rest of the TOML untouched. Probe: the `codex` command or `~/.codex/`.
- [x] `install.sh` (python3 `json`, case-sensitive): confirm the case-duplicate keys round-trip.
- [x] `install.sh` on Windows with only the Microsoft Store `python3` stub: the stub passes `command -v`, then fails ("Python was not found"), and `set -euo pipefail` aborts before `install_skills`. Probe that `python3` really runs, and copy the skills either way.

Skills:
- [ ] (not done by decision: see the 2026-10-30 comment) Copy the three skill folders into each detected client's user skill directory, replacing only those three folders: Claude Code `~/.claude/skills/`, plus the paths for opencode, Codex and Cursor. Confirm each against the client's docs first: likely `~/.config/opencode/skills/`, `~/.codex/skills/` and `~/.cursor/skills/`, none verified.
- [x] (second option: installed for Claude Code only; Cursor and opencode also load `~/.claude/skills`, untested there, as `INSTALL.md` says) The skills name Claude Code tools (`Agent` with `model: "sonnet"`, `SendMessage` to resume a subagent). For each other client, either confirm an equivalent exists (subagent dispatch, resume with context) and make the skill wording client-neutral, or install the skills only where the orchestration works and say so in `INSTALL.md`.
- [x] `INSTALL.md` and the README "Flow testing" section name the clients that get the skills.

Tests:
- [x] (narrowed: skills checked in `~/.claude/skills` only; configs checked for the `vs-debug` entry and the surviving `other` server, not for "only") Seam 2 per client: both installers against a temp home (on Windows set `USERPROFILE` and `APPDATA` as well as `HOME`; see Comments). Each holds a fake install and, per client, an existing config plus an unrelated pre-existing skill. Check that each config gained only the `vs-debug` entry, the three skills landed, and the unrelated skill is untouched.
- [x] A `.claude.json` fixture with case-duplicate `projects` keys gets wired.

## Comments

Testing hazard (issue 09): on Windows, `install.sh`'s Python wiring resolves `~` through `USERPROFILE`, not `HOME`. A "temp HOME" run therefore wires the real client configs unless `USERPROFILE` is set too, while the bash skill copy follows `HOME`.

2026-10-30, implemented.
- Wiring: `install.ps1` parses with `ConvertFrom-Json -AsHashtable` on PowerShell 7 and `JavaScriptSerializer` on 5.1 (both keep `C:/x` and `c:/x`), into case-sensitive hashtables. The rewrite also found and fixed a data-loss bug: the old `To-Hashtable` collapsed other servers' arrays (`["x"]` became `"x"`, `[]` became `null` on 7 or `{}` on 5.1). Codex: both installers edit only the `[mcp_servers.vs-debug]` table of `$CODEX_HOME/config.toml` as text (rest of the file byte-for-byte, other keys in the table kept; output checked with `tomllib`). `install.sh` now runs `python3`, then `python`, before using one, so the Store stub falls through to a real `python` or to the manual-wiring message, and the skills copy either way.
- Skills, decision: installed for Claude Code only, the second option of the orchestration line. From the clients' docs and source: Codex reads `~/.agents/skills` (`~/.codex/skills` deprecated) and not `~/.claude/skills`; opencode reads `~/.config/opencode/skills`, `~/.claude/skills` and `~/.agents/skills`; Cursor reads `~/.agents/skills`, `~/.cursor/skills`, `~/.claude/skills` and `~/.codex/skills`. Subagents: Cursor has `Task` (model per subagent, resume by agent ID, inherits MCP); opencode has `task` (resume unverified); Codex has `spawn_agent`/`resume_agent` (MCP inside subagents unverified). None is tested with these skills, so copying per client would ship unproven orchestration, and opencode and Cursor would load the same skills from several directories. `INSTALL.md` says so. The spec's Out of Scope line still holds, so no spec change or ADR.
- Tests: `scripts/test-installers.ps1` (seam 2, now a CI step) runs `install.ps1` on PowerShell 7 and 5.1, `install.sh` with Git Bash, and `install.sh` with a failing `python3`/`python` stub. Each runs twice against a temp home (`HOME`, `USERPROFILE`, `APPDATA`) with existing Claude Code, opencode, Cursor and Codex configs and an unrelated `my-own` skill.
- Not done: a skill-wording change for other clients (would need the dogfood rerun of definition-of-done step 5); live check of the wired entries in Codex or Cursor (only the file contents are checked).
