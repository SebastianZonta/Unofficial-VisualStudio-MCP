# 02: Ship skills in release and installer

**What to build:** A developer running the one-liner installer gets the flow-testing skills installed at user level alongside the MCP registration. The release zip carries the skills directory (so it rides the existing checksum); `install.ps1` and `install.sh`, when Claude Code is detected, copy the three skill folders into the user-level Claude skill directory, replacing only those folders. `-ConfigOnly` performs the copy from an existing install. `INSTALL.md` says so. See [spec](../spec.md).

**Blocked by:** 01 (setup-flow-testing skill)

**Status:** done

- [x] Release workflow includes the skills directory in the zip
- [x] `install.ps1` copies the skill folders when Claude Code is detected, including under `-ConfigOnly`
- [x] `install.sh` behaves the same
- [x] Run against a temp HOME with a fake install plus an unrelated pre-existing user skill: three skill folders land, the unrelated skill is untouched, a rerun replaces the three folders
- [x] Missing skills directory in the install (older bundle) is a clear message, not a crash
- [x] `INSTALL.md` notes the skills copy
