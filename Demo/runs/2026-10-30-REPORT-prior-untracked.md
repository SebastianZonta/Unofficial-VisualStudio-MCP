# Flow verdict report

**Verdict:** FAIL (procedural). Root cause is proven with debugger evidence, but the use case 1 orders checkpoint was not inspected (see Waivers).

## Use case 1 — small order (cache miss)

- Trigger: `POST /orders {"productId":"widget","quantity":1}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Pricing.Engine\Application\GetPriceHandler.cs:19`. `request` = `GetPriceQuery { ProductId = widget, Quantity = 1, Coupon =  }`; `cache._entries.Count` = `0` (miss).
- Checkpoint orders (session A): NOT INSPECTED. `list_breakpoints` showed `hitCount: 1`, `mode: run` after my premature `resume` (see Waivers).
- Observed `orderId`, `total`, `cached`: `78eccc6b`, `108.00`, `false` (HTTP response, as expected).

## Use case 2 — large couponed order (stale hit)

- Trigger: `POST /orders {"productId":"widget","quantity":10,"coupon":"SAVE20"}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...GetPriceHandler.cs:19`. `request` = `GetPriceQuery { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`; `cache._entries.Count` = `1`; `cache._entries["widget"]` = `PriceResult { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }` (the entry stored by use case 1).
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Orders.Api\Application\CreateOrderHandler.cs:12` (`hitCount: 2`). `cmd` = `CreateOrderCommand { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`. After stepping over the pricing `await`: `priced` = `PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = True }`; `orderId` = `"75634c4c"`.
- Observed `orderId`, `total`, `cached`: `75634c4c`, `108.00`, `true` (HTTP response). Correct would be `756.00`. The debugger `orderId` matches the HTTP `orderId`, so both stops belong to the same flow.

## Root cause

- Cache key used vs cache key required: used `ProductId` only (`"widget"`, case-insensitive dictionary in `PriceCache`). Required: every input that changes the price, i.e. `ProductId` + `Quantity` + `Coupon`.
- Why static reading alone could not close it: the code reads as a plausible per-product perf cache. Only the live stop showed that use case 2 never recomputed (`Cached = True`, `Discount = 0`) and that the stored entry at the lookup was the one use case 1 wrote, with different quantity and coupon.

## Waivers

- Use case 1, orders checkpoint (session A): I called `resume` on session A right after sending the request, before polling for the stop, so the breakpoint was hit (`hitCount: 1`) but passed without inspection. Use case 1's `orderId` is from the HTTP response only. Not re-run: a second run would hit the poisoned cache and destroy the evidence.
- `launch_process` results carried the new PID and `mode: run` but no `lastHit` (nothing had been hit yet). PIDs: engine 45192, frontend 32712.
- `evaluate` refuses method calls, so the cache was read through the private `_entries` field instead of `TryGet`.
- `list_processes` took more than 120s and was moved to a background task; it was optional and not needed.
- Closed down: both sessions detached, no breakpoints left in either, both targets stopped (ports 5201/5202 free), both VS instances still open (devenv 25964, 34676).
