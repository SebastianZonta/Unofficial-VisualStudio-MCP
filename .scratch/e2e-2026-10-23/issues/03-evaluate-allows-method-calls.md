# 03: evaluate executes method calls instead of rejecting with invalid-expression

**What broke (E2E 2026-10-23-2217):** I10 — `evaluate("counter.Increment()")` returned `{"value":"1","type":"int"}` instead of `invalid-expression` (side effect suppressed by `, nse`: `Value` stayed 0). I11 — `evaluate("order.Items.Clear()")` evaluated to void instead of `invalid-expression` (`Count` stayed 1500). Same root visible in OBS rows: I12 `counter.NextTicket` → `1`, I14 `order.ComputeSubtotal()` → `5625.00`, lambda `Count(...)` → `300` — all evaluated, writes neutered by NSE.

**Triage question:** the doc/TOOLS.md contract says invocations are `invalid-expression`, but the live bridge relies on `, nse` to neuter them. Decide one: tighten the shared read-only validator to reject invocations (keeping tool-boundary and shared checks in sync per the audit-should-fix dedupe), or downgrade the contract and the E2E expectation to OBS.

**Blocked by:** None (note audit-should-fix plans validator dedupe — keep in sync).

**Status:** resolved

- [ ] `counter.Increment()` and `order.Items.Clear()` → `invalid-expression`, state unchanged — or the contract is deliberately changed with TOOLS.md + e2e.md updated to match.
- [ ] C7/I15/I16 rejection behavior unchanged (no over-tightening: comparisons like `i == 3 || i == 4` must still evaluate).
- [ ] Seam-level tests pin the decided behavior.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-23-2217.md` rows I10, I11, I12, I14. Related spec: `.scratch/audit-should-fix/spec.md` (validator dedupe).
