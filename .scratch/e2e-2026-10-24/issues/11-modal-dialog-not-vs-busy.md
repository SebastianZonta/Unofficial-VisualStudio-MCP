# 11: Modal dialog surfaces as `automation-failed`, not `vs-busy` (X1-manual)

**What broke (phase H 2026-10-24, X1-manual):** with a modal dialog open
(File → Open → Project/Solution picker) in VS on repo `Sandbox.slnx`,
`list_instances` → `{reachable:false, error:"automation-failed: solution
unreadable"}`. Expected `vs-busy` per X1 ("open modal, then any call →
vs-busy, never crash"). No crash (good); but the busy classification missed —
the OleMessageFilter / error mapper did not recognize the modal-blocked call
as call-rejected. `list_sessions` stayed normal throughout (server-side, no
COM call — X1's "any call" premise only holds for calls that reach VS).
After dismissal, `list_instances` recovered to `reachable:true` (resilience
half holds).

**Blocked by:** None.

**Status:** needs-triage

## Comments

Phase H evidence: `.scratch/e2e-runs/2026-10-24-1800.md` (Phase H section).
Decide the contract: modal-blocked → `vs-busy`, or document
`automation-failed` as the modal answer and relax X1.
