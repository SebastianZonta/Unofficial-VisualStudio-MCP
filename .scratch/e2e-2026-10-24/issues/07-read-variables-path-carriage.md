# 07: `read_variables` top-level expandable has no `path`; dotted element paths don't round-trip

**What broke (E2E 2026-10-24-1800-R2, I17):** top-level expandable `order`
entry keys are exactly `name,type,value,expandable` — no `path` field, so the
documented drill (`read_variables(f0, path: <that>)`) has nothing to quote.
Drill via bare name works (`path:"order"`, `path:"order.Items"`), and values
match `evaluate` (`"SKU-0001"`), but the returned element paths
(`order.Items.[0]`, dot form) do NOT round-trip (`path-not-found`); the
accepted form is `order.Items[0]` (no dot).

**Suspected:** two halves — (a) top-level entries never carry `path`; (b) the
path parser emits a dotted form for indexed elements that its own resolver
rejects. Either emit usable paths or document the bare-name + no-dot-index
form.

**Blocked by:** None.

**Status:** needs-triage

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R2.md` row I17.
