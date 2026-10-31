# 10: Cross-solution run: one target's checkpoint not reached, once

**What happened:** In the issue 09 dogfood pass, the first `greet-pair` run (two debug sessions, `command` trigger) ended FAIL: sandbox2's checkpoint passed, but sandbox reached `mode: design` with its breakpoint at `Scenarios.cs:153` showing `hitCount: 0`. The run also showed an `other1` breakpoint at the same line in the Sandbox session, which the run did not create; `list_breakpoints` at cleanup showed none. A clean rerun of the same plan passed. Report: `docs/flows/runs/greet-pair-20261030-215101.md` in the dogfood copy. See [spec](../spec.md).

**Blocked by:** none

**Status:** needs-triage

- [ ] Reproduce, or rule out: is it the flow runner's session switching (runner.md step 5, several debug sessions), the bridge, or the target exiting early?
- [ ] Where did `other1` come from, and why was it gone by cleanup?
- [ ] Fix, or document the limit in runner.md

## Comments

Context: greet ran just before on the same Sandbox VS instance and left `name.txt` (Ada); it was deleted before greet-pair started, so both targets should have waited for the command. In the rerun, the flow runner appended diagnostics to the plan's command (`; echo exit=$?; cat name.txt`), which breaks runner.md step 5 ("character for character"). It reported that as an OBS. Watch for this. A third run (`greet-pair-20261030-221355.md`) PASSed with the command run as written. In that run the first `run_to_cursor` in the Sandbox debug session returned `vs-busy` (HRESULT 0x80010001) while that session was already in `mode: break` at the checkpoint. A busy VS around the session switch is a lead for the first run's miss.
