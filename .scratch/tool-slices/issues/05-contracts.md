# 05 — Slice G: contract gates and E2E mapping

Status: resolved

## Task

- Extend the docs-contract check from names and counts to params, defaults, `mode`/`lastHit`/`exception` envelope, and error-code assignment for every touched tool.
- Add the missing audit E2E cases that prove fake/live parity (envelope fields on all paths, read-only gate, devenv guard, symbol-state honesty) and map each tool slice to its runner slice and run tag.
- Enforce the pre-audit gate: bridge-behavior diff without a new audit case and a live run is rejected.

## Done when

- Contract test fails on param/default/envelope drift, not just renames.
- Every tool slice has a named live phase and a fake suite; skipped live cases are explicitly listed with reason.

## Notes

No tool behavior changes in this issue; gates only.

## Comments

- `DocsContractTests` now fails on param/default/envelope drift, not just
  renames: `Every_tool_parameter_is_documented_in_TOOLS` (each tool-call param
  appears in its TOOLS.md row), `Optional_tool_parameters_show_a_default_marker_in_TOOLS`
  (code defaults require a `` `name?` `` or `` `name` =`` marker; negative-checked
  by breaking one row and watching it fail), `Result_envelope_is_mode_lastHit_and_exception_only`
  (modeless set pinned to detach echo + three session-less lists; execution
  results carry `Exception`; envelope section names all three).
- Slice map (tool-slice → phases → runner → fake suite → live audit) added to
  `docs/agents/e2e.md` under Parallel run; skipped live cases listed with reason
  (`BusyVs` manual modal-dialog; devenv-guard/symbol-honesty pinned on the fake
  and covered by manual P9/P3 cases, no automated audit case).
- Pre-audit gate needed no change: already enforced at `CODING_STANDARDS.md:17-21`
  (bridge-behavior diff without a new audit case and a live run is rejected).
- Not pinned (shortcut): per-tool error-code assignment is dynamic (thrown at
  runtime), so no static test asserts which tool yields which code; the error-code
  catalog rows plus the fake exception-path tests are the gate. Exact default
  literals (e.g. `= 5000`) are not asserted, only the `?`/`=` marker.
- Build clean, 315 unit tests pass (312 + 3 new), E2E skipped (no live VS).
