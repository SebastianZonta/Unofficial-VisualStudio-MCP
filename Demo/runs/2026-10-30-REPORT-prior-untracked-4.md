# Flow verdict report

**Verdict:** PASS

Root cause proven through the debugger, with quoted `mode`/`lastHit` evidence at both checkpoints. Trigger 2 returned the stale cached total (`108.00`) instead of the correct `756.00`.

## Use case 1 — small order (cache miss)

- Trigger: `POST /orders {"productId":"widget","quantity":1}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Pricing.Engine\Application\GetPriceHandler.cs:19`. `request` = `GetPriceQuery { ProductId = widget, Quantity = 1, Coupon =  }`, `unitPrice` = `100`, `couponDiscount` = `0`, `cached` = `null`. `cache._entries` = `Count = 0` (miss).
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Orders.Api\Application\CreateOrderHandler.cs:12`. `cmd` = `CreateOrderCommand { ProductId = widget, Quantity = 1, Coupon =  }`, `priced` = `null`. After stepping over the pricing `await`: `priced` = `PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }`. After two more steps: `orderId` = `"b3e17def"`.
- Observed `orderId`, `total`, `cached`: `b3e17def`, `108.00`, `false` (HTTP 200). Matches expectation.

## Use case 2 — large couponed order (stale hit)

- Trigger: `POST /orders {"productId":"widget","quantity":10,"coupon":"SAVE20"}`
- Checkpoint pricing (session B): `mode: break`, `lastHit: ...Pricing.Engine\Application\GetPriceHandler.cs:19`. `request` = `GetPriceQuery { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`. `cache._entries.Count` = `1`; the debugger lists the single key `["widget"]`, and `cache._entries["widget"]` = `PriceResult { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }` (the qty-1 entry from use case 1).
- Checkpoint orders (session A): `mode: break`, `lastHit: ...Orders.Api\Application\CreateOrderHandler.cs:12`. `cmd` = `CreateOrderCommand { ProductId = widget, Quantity = 10, Coupon = SAVE20 }`. After stepping over the pricing `await`: `priced` = `PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = True }`. After two more steps: `orderId` = `"9e57d7ab"`.
- Observed `orderId`, `total`, `cached`: `9e57d7ab`, `108.00`, `true` (HTTP 200). Correct would be `756.00`.

## Root cause

- Cache key used vs cache key required: used the product id alone (`"widget"`, the only key in `cache._entries` at the stop). Required: a key covering every input to the price, at least product + quantity + coupon. A qty-1/no-coupon result was served to a qty-10/SAVE20 request.
- Why static reading alone could not close it: the lookup looks plausible in isolation. Only the live stops show a populated cache holding one `"widget"` entry returning `Total = 108.00` / `Cached = True` for a request with `Quantity = 10, Coupon = SAVE20`, and the shared `orderId` joins the two services' stops into one flow.

## Waivers

- `launch_process` results carried `mode: run` and the new PID (B: 31372, A: 36340) but no `lastHit` field; none exists before a first stop. Not counted against the verdict.
- `resume` on session A right after trigger 1 returned `already-running` (target was already running; harmless, no state change).
- I did not evaluate `couponDiscount` at the trigger-2 pricing stop, so the report makes no claim about it.
- The prior untracked `Demo/REPORT.md` was archived to `Demo/runs/2026-10-30-REPORT-prior-untracked-3.md` before this report was written.
- Close-down: bp1 removed in both sessions, `list_breakpoints` empty for both (`"breakpoints":[]`), both sessions detached (`list_sessions` empty), targets 31372 (`Pricing.Engine`) and 36340 (`Orders.Api`) stopped (verified as the port owners first), ports 5201/5202 free, both `devenv` instances (3108, 22096) left open.
