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

**Update (live on v7 pre-fix):** top-level `order` DOES carry
`"path":"order"` — half (a) does not reproduce; the R2 reading was wrong.
Real bug is emitter-only, half (b).

**Fix (done, unit-green, live-verified on v8):**
`DteDebugBridge.JoinMemberPath` (internal static): no dot when the child
starts with `[` — drill paths are fed back to the C# evaluator and must be
valid C#. `SnapshotMembers` uses it; the fake's canned paths already used the
no-dot form, which is why unit tests never caught it. New UT
`Drill_path_join_is_valid_csharp`; I17 live re-run: emitted
`order.Items[0]`, round-trips, Sku `"SKU-0001"`. 310/310 green. Published to
`standalone-v8/`, host repointed + restarted. Cleaned the orphaned bp the
server kill left behind (`Sandbox/.vs` deleted again, no VS running).

**Status:** resolved

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R2.md` row I17.
