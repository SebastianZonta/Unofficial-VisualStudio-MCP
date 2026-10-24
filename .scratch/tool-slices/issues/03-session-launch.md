# 03 — Slice C: session/process/launch end to end

Status: resolved

## Task

- Own `attach`, `detach` (deletes own breakpoints, siblings unaffected), `list_sessions`, `list_instances`, `list_processes` (session-less), `attach_process` (consent plus same-user, consent checked before bind, VS PID never an attach target, prompt re-list on PID recycle), `launch_process` (absolute path, no shell expansion, attached from birth, one target process per debug session).
- Land in order: per-session dispatcher lanes ADR first, then session labels, then follow-mode fingerprint behind the bridge seam.
- Add one fake unit case plus one live audit case proving the same assertion on both bridges for every touched path.

## Done when

- Session isolation, consent ordering, and one-target rule proven on fake and live (`VSDEBUG_E2E=1`) for the slice's E2E phases.
- No edits outside Slice C paths.

## Notes

Respects ADR-0004, ADR-0007, ADR-0008, ADR-0009. Vocabulary: VS instance vs debug session vs target process, mine vs consent, VS PID vs target PID.

## Comments

- No code changes: all three Done-when gates were already proven on both
  bridges at slice triage (verified 2026-10-24: 42/42 slice-C fake tests green).
- Session isolation — fake: `MultiSessionTests` (7: distinct sessions, breakpoint
  + mode isolation, detach forgets one/keeps siblings, per-session death,
  reattach). Live: `ParallelAttach_SameInstance_SameSession`,
  `AttachMode_ReportsKnownMode`.
- Consent ordering — fake: `Consent_gate_runs_before_the_one_target_check`,
  `Same_user_gate_runs_before_the_one_target_check`. Live:
  `OneTargetRule_BothDirections` (consent-required precedes already-bound).
- One-target rule — fake: `Second_attach_process_while_bound_fails_named`,
  `Launch_while_a_target_process_is_bound_fails_named`,
  `Launch_after_attach_process_fails_named`,
  `Attach_process_after_launch_fails_named`. Live: `OneTargetRule_BothDirections`
  (both directions plus the rule no longer stranding the session).
- Live cases above last exercised in the 2026-10-24-1800 pilot (R1/R2/R3 PASS);
  not re-run headless here. No edits outside Slice C paths (none at all).
- Deferred by maintainer choice: per-session dispatcher lanes ADR (would reverse
  ADR-0008's accepted "single STA dispatcher is kept"), session labels, and
  follow-mode fingerprint are undefined new-feature work — re-triage as separate
  issues with definitions before any implementation.
