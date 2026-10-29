# 04: Demo script plus verdict template plus live rehearsal

**What to build:** The runnable ten-minute show: an audience-facing script with two triggers and one checkpoint per target process in fixed MCP tour order, a host-only sheet with the expected correct and stale totals, a blank verdict template requiring quoted mode and lastHit per checkpoint, and one recorded live pass on two VS instances proving the tour completes.

**Blocked by:** 03 (needs the poisoned repro serving to rehearse against).

**Status:** ready-for-agent

- [x] Script runs discover before attach, one debug session per VS instance, one breakpoint per target process, inspect via stack plus variables plus evaluate at each stop, then continue and detach with no stopped target left behind.
- [x] Host sheet pins the correct first total and the stale second total; the audience script does not spoil the root cause.
- [ ] One live rehearsal verdict exists with PASS/FAIL backed by quoted mode/lastHit per checkpoint.

## Comments

Delivered this session: `DEMO_SCRIPT.md` (audience tour), `HOST_NOTES.md`
(expected totals + root cause + fix, host-only), `REPORT_TEMPLATE.md` (blank
verdict). HTTP-level repro verified (see 03). The two-VS live pass is
host-side: it needs VS 2026 + this MCP on the demo machine, which this
environment doesn't have. Rehearse it tonight per the script.

## Comments (restructure update)

- Coupon catalog + validation relay added: SAVE20/SAVE10/WELCOME5
  (case-insensitive, null/empty valid), unknown coupon → 400 relayed verbatim,
  quantity<1 → 400 (orders validates locally, engine too), unknown product →
  404 relayed verbatim, engine 5xx/transport → 502. Docs updated:
  `HOST_NOTES.md` (coupon table, error cases, new checkpoint locations),
  `DEMO_SCRIPT.md` (handler file refs, optional error-probe stretch).
- Checkpoint lines moved to the handler files:
  `Demo/Pricing.Engine/Application/GetPriceHandler.cs:18` (pricing) and
  `Demo/Orders.Api/Application/CreateOrderHandler.cs:11` (orders); cache is now
  singleton `Demo/Pricing.Engine/Infrastructure/PriceCache.cs` keyed by product
  only. `Demo/AGENTS.md` needed no change (no stale file refs; DLL paths and
  ports verified current).
- `Demo/REPORT.md` body is stale (pre-restructure `Program.cs` line numbers):
  staleness banner prepended, body untouched. A fresh two-VS live pass
  regenerates it.
