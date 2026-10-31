# 11: install.ps1 skips Claude Code wiring when ~/.claude.json has keys that differ only by case

**What happened:** In the issue 09 pass, `install.ps1 -ConfigOnly` printed `Skipping C:\Users\<user>\.claude.json: not valid JSON (... contains the duplicated keys 'C:/Users/<user>/source/repos' and 'c:/Users/<user>/source/repos'.)`. Claude Code writes one `projects` key per cwd as typed, so drive-letter case differences are common on Windows. `ConvertFrom-Json` (into a PSCustomObject, on 5.1 and 7) rejects keys that differ only by case, so Claude Code is never wired. The skills still installed. See [spec](../spec.md).

**Blocked by:** none

**Status:** needs-triage

- [ ] Parse `.claude.json` without the case-insensitive collision (e.g. `-AsHashtable` on PowerShell 7, or a targeted edit of `mcpServers` only), keeping the strict invalid-JSON skip
- [ ] Check `install.sh` (python3 `json`, case-sensitive) does not have the same problem
- [ ] `install.sh` on Windows with only the Microsoft Store `python3` stub: the stub passes `command -v`, then fails ("Python was not found"), and `set -euo pipefail` aborts before `install_skills`. Probe that `python3` really runs, and copy the skills either way.

## Comments

Testing hazard (issue 09): on Windows, `install.sh`'s Python wiring resolves `~` through `USERPROFILE`, not `HOME`. A "temp HOME" run therefore wires the real client configs unless `USERPROFILE` is set too, while the bash skill copy follows `HOME`.
