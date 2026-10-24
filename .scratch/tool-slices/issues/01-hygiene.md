# 01 — Hygiene: index matches disk, exception-config triage, slice wording

Status: resolved

## Task

- Fix `.scratch/README.md`: mark `set-variable` done (disk already done), add missing rows for `audit-oct24-high`, `audit-oct24-medium`, `parallel-e2e`, `audit-should-fix`, expand `e2e-2026-10-24` to 11 issues with current states.
- Close or re-scope `exception-config/02-live-tools-and-docs.md` (`needs-info`): E2E exception cases plus ADR-0011 already cover list/set/first-chance/unhandled-only; leave only the delta or close.
- Reconcile run-tag slice table (`parallel-e2e` spec, `docs/agents/e2e.md`, `GLOSSARY.md` Slice entry) on P9/P10 placement and runner PID scoping.

## Done when

- `README.md` row count equals disk directories; no untracked spec dir.
- `exception-config-02` is `resolved` or rewritten to delta-only with `Status:` updated.
- One slice table agreed across the three docs.

## Notes

No bridge, fake, or tool code in this issue.

## Comments

- Index: `set-variable` moved to Done (spec `done`, both issues resolved); added rows for `audit-oct24-high` (done), `audit-oct24-medium`, `parallel-e2e`, `e2e-2026-10-24` (11 issues), and `tool-slices` itself. `audit-should-fix` was already listed, verified correct. 22 feature rows = 23 disk dirs minus gitignored `e2e-runs`.
- `exception-config-02` closed as resolved: the two live checks are covered by E2E 2026-10-24-1800-R3 (E13, E21–E25 PASS) plus ADR-0011, per the issue's own close condition. No delta remained.
- Slice table: `e2e.md` (rebalance `851a8d4`, exercised by the 2026-10-24-1800 pilot: R2 ran the M-cases) is the truth; `GLOSSARY.md` Slice entry and `parallel-e2e` spec stories 4–5 + slice-boundary bullet updated to R1 = P0+P1–P4+P9, R2 = P0+P5–P7+P10, R3 = P0+P8+P11. P9 was already R1 in all three docs; PID scoping (D3/E14/Z3 runner-scoped) already agreed.
