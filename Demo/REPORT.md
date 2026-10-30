# Flow verdict report

**Verdict:** PASS

Root cause proven through the debugger. Both checkpoints were quoted from live sessions; the `orderId` ties each pair of stops to one flow.

## Use case 1 — small order (cache miss)

- Trigger: `POST /orders {"productId":"widget","quantity":1}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Demo\Pricing.Engine\Application\GetPriceHandler.cs:19`. `request = GetPriceQuery { ProductId = widget, Quantity = 1, Coupon =  }`, `cached = null` (empty cache), `evaluate request.ProductId` -> `"widget"`.
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Demo\Orders.Api\Application\CreateOrderHandler.cs:12`. `cmd = CreateOrderCommand { ProductId = widget, Quantity = 1, Coupon =  }`. After stepping over the pricing await: `priced = PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }`. `evaluate orderId` -> `"3c162884"`.
- Observed `orderId`, `total`, `cached`: `3c162884`, `108.00`, `false` (HTTP body matched the debugger values). Correct.

## Use case 2 — large couponed order (stale hit)

- Trigger: `POST /orders {"productId":"widget","quantity":10,"coupon":"SAVE20"}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Demo\Pricing.Engine\Application\GetPriceHandler.cs:19`. `request = GetPriceQuery { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`, `couponDiscount = 0.20`, `cached = null` and `discount/total/result` still unset at the stop. `evaluate cache._entries["widget"]` -> `PriceResult { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }`, the entry stored by use case 1.
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Demo\Orders.Api\Application\CreateOrderHandler.cs:12`. `cmd = CreateOrderCommand { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`. After stepping over the pricing await: `priced = PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = True }`. `evaluate orderId` -> `"fda98c59"`.
- Observed `orderId`, `total`, `cached`: `fda98c59`, `108.00`, `true`. Wrong; correct is `756.00` (qty 10 x 100 minus SAVE20, plus whatever surcharge the calculator applies).

## Root cause

- Cache key used vs cache key required: used `request.ProductId` only (`"widget"`, the sole key in `cache._entries`). Required: a key that also covers `Quantity` and `Coupon`, since the price depends on both.
- Why static reading alone could not close it: the cache lookup reads correctly in isolation. Only the live stop showed the entry already stored under the bare `"widget"` key by an earlier request, and that the second request, with different inputs, hit it.

## Waivers

- `evaluate` rejects method calls, so the cache contents were read through `read_variables` on `cache._entries` and the indexer `cache._entries["widget"]` instead of `cache.TryGet(...)`.
- The first `step over` on each session timed out at the HTTP await (expected: the call blocks on the other session's breakpoint); the later `read_stack` showed `mode: break`.
- The 756.00 expected total comes from the Demo brief, not from running the calculator.
- A prior untracked `Demo/REPORT.md` was moved to `Demo/runs/2026-10-30-REPORT-prior-untracked-4.md` rather than overwritten.
