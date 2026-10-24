# 09: Generic-arity lambda call sails through the read-only `evaluate` gate

**What broke (E2E 2026-10-24-1800-R2, I18 OBS):**
`evaluate("order.Items.ConvertAll<string>(x => x.Sku)")` evaluated fine
(`Count = 1500`, `List<string>`, expandable) while the gate refuses
`counter.Increment()`, `order.Items.Clear()`, `ComputeSubtotal()` and
lambdas-in-`Count` with `invalid-expression`/`invalid-argument`. A method call
with a lambda got through the read-only gate — possible gate hole for
generic-arity syntax. `counter.Value` side-effect checks still held elsewhere.

**Blocked by:** None.

**Update:** no gate hole — current code refuses it. `ContainsInvocation`
already counts `>` before `(` as a call when a `<` precedes it
(generic-arity), and UT `foo<T>(x)` + lambda-invocation rows pin it. R2 ran
I18 pre-fix (generic-arity branch landed in `32b8370`, after the R2 binary
was published). Added the exact R2 string
`order.Items.ConvertAll<string>(x => x.Sku)` as a UT row (41/41 green) and
re-ran I18 live on standalone-v8: `invalid-expression: expression must be
read-only (method calls are not supported)`. No new E2E case — I18 already
covers it. No VS running, `Sandbox/.vs` cleaned.

**Status:** resolved

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R2.md` row I18. Per the case,
gate unit tests are authority on whether this input should refuse.
