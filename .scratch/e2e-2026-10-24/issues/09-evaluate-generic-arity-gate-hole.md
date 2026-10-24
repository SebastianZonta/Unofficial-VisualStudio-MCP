# 09: Generic-arity lambda call sails through the read-only `evaluate` gate

**What broke (E2E 2026-10-24-1800-R2, I18 OBS):**
`evaluate("order.Items.ConvertAll<string>(x => x.Sku)")` evaluated fine
(`Count = 1500`, `List<string>`, expandable) while the gate refuses
`counter.Increment()`, `order.Items.Clear()`, `ComputeSubtotal()` and
lambdas-in-`Count` with `invalid-expression`/`invalid-argument`. A method call
with a lambda got through the read-only gate — possible gate hole for
generic-arity syntax. `counter.Value` side-effect checks still held elsewhere.

**Blocked by:** None.

**Status:** needs-triage

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R2.md` row I18. Per the case,
gate unit tests are authority on whether this input should refuse.
