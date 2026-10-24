# 01: Busy VS orphans breakpoints (run_to_cursor leaks its temporary breakpoint)

**What broke (E2E 2026-10-24-0210, scoped run_to_cursor pass):** E16 — `run_to_cursor` at HEARTBEAT right after `launch_process(wait 600)` returned `vs-busy`, yet the target stopped at line 134 and the temporary breakpoint stayed behind as `other1` (no longer owned, so not removable through the tools). The same shape appeared earlier at line 13 during `--live-check-run-to`.

**Root cause (code read, not isolated live):**
- `remove_breakpoint` treated every COM failure from `Delete()` as "already deleted by hand", so a busy rejection (`RPC_E_CALL_REJECTED`) dropped the id while the breakpoint stayed in VS.
- `set_breakpoint` could fail after `Add` succeeded (busy on a later read in the same call), so no id ever owned the new breakpoint.

**Fix:** busy rejection now surfaces as `vs-busy` and keeps the id owned; a failure after `Add` deletes the just-created breakpoint; `run_to_cursor` retries removal on `vs-busy` before giving up (the id stays owned if VS stays busy).

**Blocked by:** None.

**Status:** resolved

- [x] Fake/unit coverage: cleanup retried on vs-busy; persistently busy cleanup leaves an owned, removable breakpoint; only call-rejected counts as busy.
- [x] Scoped E2E E16–E20 green on a fresh publish (needs the user to stop the MCP host, republish, and close leftover VS A / Sandbox processes).

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-0210.md` row E16.

Scoped rerun on the 02:18 binary (`.scratch/e2e-runs/2026-10-24-0224.md`): E16, E17, E18, E19, E20 PASS after clearing the breakpoint the pre-fix run had persisted in `Sandbox/.vs`.
