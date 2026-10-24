# 01: list_processes and attach_process success paths throw bare host errors

**What broke (E2E 2026-10-23-2217, VS 18.11.12306.390, standalone 21:35:07):** every `list_processes` call (`{}`, `ownedOnly` true/false, `nameFilter: "sandbox"`) and every `attach_process(waitPid, consent: true)` success-path call throws a host-level `Error: An error occurred invoking '<tool>'.` with no error code. Retried, 3 attempts for A3.

**Still fine:** A1/A2 validation paths (`consent-required`, `process-not-found`, `invalid-argument`) all return proper codes, so only the success paths are broken. Both tools touch OS process enumeration/attachment, so the likely root is one shared unmapped exception (e.g. process enumeration or owner lookup throwing).

**Blocked by:** None.

**Status:** resolved

- [ ] `list_processes` returns entries (D3/D4 green) instead of a bare host error.
- [ ] `attach_process` success path returns `status: ok, mode: run` (A3 green), unblocking A4–A7.
- [ ] Failures in these paths carry mapped error codes, never bare host errors.
- [ ] Unit coverage over the seam for the previously-throwing path (fake or live-check).

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-23-2217.md` rows D3, D4, A3. Cross-checked "no new process" assertions via shell `Get-Process` in E14/Z3.
