# Flow verdict report

**Verdict:** PASS

Root cause proven with quoted `mode`/`lastHit` evidence at both checkpoints. Trigger 2 returned the stale cached total (`108.00`) instead of the correct `756.00`.

## Use case 1 — small order (cache miss)

- Trigger: `POST /orders {"productId":"widget","quantity":1}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Pricing.Engine\Application\GetPriceHandler.cs:19` (bp1 `hitCount: 1`). `request` = `GetPriceQuery { ProductId = widget, Quantity = 1, Coupon =  }`, `unitPrice` = `100`, `couponDiscount` = `0`. `cache._entries.Count` = `0` (miss).
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Orders.Api\Application\CreateOrderHandler.cs:12` (bp1 `hitCount: 1`). `cmd` = `CreateOrderCommand { ProductId = widget, Quantity = 1, Coupon =  }`, `priced` = `null`. After stepping over the pricing `await`: `priced` = `PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }`. After two more steps: `orderId` = `"17743380"`.
- Observed `orderId`, `total`, `cached`: `17743380`, `108.00`, `false` (HTTP 200). Matches expectation.

## Use case 2 — large couponed order (stale hit)

- Trigger: `POST /orders {"productId":"widget","quantity":10,"coupon":"SAVE20"}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Pricing.Engine\Application\GetPriceHandler.cs:19` (bp1 `hitCount: 2`). `request` = `GetPriceQuery { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`, `couponDiscount` = `0.20M`. `cache._entries.Count` = `1`; `cache._entries["widget"]` = `PriceResult { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }` (the qty-1 entry from use case 1).
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Orders.Api\Application\CreateOrderHandler.cs:12` (bp1 `hitCount: 2`). `cmd` = `CreateOrderCommand { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`. After stepping over the pricing `await`: `priced` = `PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = True }`. After two more steps: `orderId` = `"d63d8e50"`.
- Observed `orderId`, `total`, `cached`: `d63d8e50`, `108.00`, `true` (HTTP 200). Correct would be `756.00`.

## Root cause

- Cache key used vs cache key required: used `request.ProductId` alone (`"widget"`, the only key in `cache._entries`); required a key covering every input to the price — at least `ProductId` + `Quantity` + `Coupon`. The qty-1/no-coupon result was served to a qty-10/SAVE20 request.
- Why static reading alone could not close it: the lookup `cache.TryGet(request.ProductId, ...)` looks plausible in isolation; only the live stops show a populated cache keyed `"widget"` returning `Total = 108.00` / `Cached = True` for a request whose `Quantity = 10` and `couponDiscount = 0.20M`, across two services joined by the `orderId` flow.

## Waivers

- `launch_process` timed out on the first attempt in both sessions at `timeoutMs` 15000 (no process left behind, verified); both succeeded on retry with 60000. Not counted against the verdict.
- A premature `resume` on session A returned `already-running` (harmless, no state change).
- Prior untracked `Demo/REPORT.md` was archived to `Demo/runs/2026-10-30-REPORT-prior-untracked-2.md` before this report was written.
- Close-down: bp1 removed in both sessions, `list_breakpoints` empty for both, both sessions detached, targets 4460 (`Orders.Api`) and 15784 (`Pricing.Engine`) stopped, ports 5201/5202 free, both `devenv` instances (3108, 22096) left open.
