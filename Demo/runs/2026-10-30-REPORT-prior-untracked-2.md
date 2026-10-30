# Flow verdict report

**Verdict:** PASS

Root cause proven with quoted `mode`/`lastHit` evidence at both checkpoints. Trigger 2 returned the stale cached total instead of the correct one.

## Use case 1 — small order (cache miss)

- Trigger: `POST /orders {"productId":"widget","quantity":1}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Pricing.Engine\Application\GetPriceHandler.cs:19`. `request` = `GetPriceQuery { ProductId = widget, Quantity = 1, Coupon =  }`, `unitPrice` = `100`. `cache._entries.Count` = `0` (miss).
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Orders.Api\Application\CreateOrderHandler.cs:12`. `cmd` = `CreateOrderCommand { ProductId = widget, Quantity = 1, Coupon =  }`. After stepping over the pricing `await`: `priced` = `PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }`. After two more steps: `orderId` = `"97e0e386"`.
- Observed `orderId`, `total`, `cached`: `97e0e386`, `108.00`, `false` (HTTP 200).

## Use case 2 — large couponed order (stale hit)

- Trigger: `POST /orders {"productId":"widget","quantity":10,"coupon":"SAVE20"}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Pricing.Engine\Application\GetPriceHandler.cs:19` (`hitCount: 2`). `request` = `GetPriceQuery { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`, `couponDiscount` = `0.20`. `cache._entries.Count` = `1`. `cache._entries["widget"].Total` = `108.00`, `cache._entries["widget"].Discount` = `0`.
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Orders.Api\Application\CreateOrderHandler.cs:12` (`hitCount: 3`). `cmd` = `CreateOrderCommand { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`. After stepping over the pricing `await`: `priced` = `PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = True }`. After two more steps: `orderId` = `"d8f3efa3"`.
- Observed `orderId`, `total`, `cached`: `d8f3efa3`, `108.00`, `true` (HTTP 200). Correct would be `756.00` (100 × 10 × (1 − 0.30) × 1.08).
- Same flow: session B's request and session A's `cmd` are identical (`widget`, qty 10, SAVE20), and the `orderId` in the HTTP response matches the one captured in session A.

## Root cause

- Cache key used vs cache key required: used `ProductId` only (`"widget"`, `PriceCache` dictionary, `OrdinalIgnoreCase`). Required: every input that changes the price, i.e. `ProductId` + `Quantity` + `Coupon`.
- Why static reading alone could not close it: the code reads as a plausible per-product perf cache, and the mismatch only shows at runtime. The live stops showed use case 2 taking the `Cached = True` branch with the quantity-1, no-coupon entry from use case 1 (`Total = 108.00`, `Discount = 0`) instead of recomputing.

## Waivers

- Aborted first trigger (procedural deviation from one repro sequence): I sent a background `curl` (30s timeout) and then a foreground retry (10s timeout) while the first request was paused at the Orders breakpoint. The 10s client timeout cancelled the request, and a step-over then ran with `ct.IsCancellationRequested = true` and left the handler frame. Pricing was never reached by that request (session B `hitCount` stayed `0`), so the cache was still empty. I did not restart either app; I let the aborted request finish and then ran trigger 1 once, with a 180s timeout. That aborted attempt is not part of the evidence above.
- Launch results carried the new PIDs and `mode: run` but no `lastHit` (nothing had been hit yet). PIDs: engine `2076`, frontend `27640`.
- `evaluate` refuses method calls, so the cache was read through the private `_entries` (`Count` and indexer) instead of `TryGet`.
- `orderId` is only assigned after the pricing call returns, so it is not visible at the line-12 stop; it was captured two steps later.
- Closed down: both debug sessions detached, no breakpoints left in either session (`list_breakpoints` empty), both target processes stopped (ports 5201/5202 free), both VS instances still open (devenv PIDs `23720` Pricing.Engine, `17812` Orders.Api). The `Demo/REPORT.md` from the earlier untracked run was moved to `Demo/runs/2026-10-30-REPORT-prior-untracked.md`, not deleted.
