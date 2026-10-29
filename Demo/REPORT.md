# Flow verdict report

**Verdict:** FAIL

## Use case 1 — small order (cache miss)

- Trigger: `POST /orders {"productId":"widget","quantity":1}`
- Checkpoint pricing (session B): breakpoint `bp1` `GetPriceHandler.cs:19` `{"bound":true,"hitCount":1}`, session `"mode":"run"` after the stop (list_breakpoints). The stop itself was continued by an operator `resume` before inspection — see Waivers. Corroborating miss-path proof: session A evaluated `priced` as `{PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }}` with `"mode":"break","lastHit":".../Orders.Api/Application/CreateOrderHandler.cs:13"`.
- Checkpoint orders (session A): `"mode":"break","lastHit":"C:\Users\Sebastián\Desktop\Opencode\Unofficial-VisualStudio-MCP\Unofficial-VisualStudio-MCP\Demo\Orders.Api\Application/CreateOrderHandler.cs:13"`, `cmd` = `{CreateOrderCommand { ProductId = widget, Quantity = 1, Coupon =  }}`, `priced` = `{PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }}`, stepped to `orderId` = `"9bc8f4f4"` (`"mode":"break"`).
- Observed `orderId`, `total`, `cached`: HTTP `{"orderId":"9bc8f4f4","productId":"widget","quantity":1,"coupon":null,"unitPrice":100,"discount":0,"total":108.00,"cached":false}` — matches debugger capture (`9bc8f4f4`), correct total 108.00. This response **poisoned the cache** with the qty-1/no-coupon entry.

## Use case 2 — large couponed order (stale hit)

- Trigger: `POST /orders {"productId":"widget","quantity":10,"coupon":"SAVE20"}`
- Checkpoint pricing (session B): `"mode":"break","lastHit":"C:\Users\Sebastián\Desktop\Opencode\Unofficial-VisualStudio-MCP\Unofficial-VisualStudio-MCP\Demo\Pricing.Engine/Application/GetPriceHandler.cs:19"`, `request` = `{GetPriceQuery { ProductId = widget, Quantity = 10, Coupon = SAVE20 }}`, `request.Quantity` = `10`, `request.Coupon` = `"SAVE20"`, with `unitPrice` = `100`, `couponDiscount` = `0.20` resolved — yet one step-over of `cache.TryGet(request.ProductId, …)` yielded `cached` = `{PriceResult { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }}` (`"mode":"break"`): the qty-1/no-coupon entry, keyed by productId alone.
- Checkpoint orders (session A): `"mode":"break","lastHit":".../Orders.Api/Application/CreateOrderHandler.cs:13"`, `cmd` = `{CreateOrderCommand { ProductId = widget, Quantity = 10, Coupon = SAVE20 }}`, `priced` = `{PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = True }}`, stepped to `orderId` = `"0716ee6c"` (first attempt `orderId` = `"573d4908"` with identical payload evidence).
- Observed `orderId`, `total`, `cached`: HTTP `{"orderId":"0716ee6c","productId":"widget","quantity":10,"coupon":"SAVE20","unitPrice":100,"discount":0,"total":108.00,"cached":true}` (first attempt `{"orderId":"573d4908",…,"total":108.00,"cached":true}`) — expected `756.00`. Plausible-but-wrong: stale `108.00` served with `cached:true`, `orderId` matching the debugger capture.

## Root cause

- Cache key used vs cache key required: used `request.ProductId` only (`cache.TryGet(request.ProductId, …)` / `cache.Store(request.ProductId, …)`); required product + quantity + coupon. Debugger proof: request carried `Quantity = 10, Coupon = SAVE20` (discount `0.20` resolved) but the lookup returned the stored `{Total = 108.00}` entry and the handler returned it with `Cached = true`, relayed end-to-end with matching `orderId`.
- Why static reading alone could not close it: the source shows the key shape, but only live state proves the stored entry's stale payload is what order 2 actually served (`Cached = True` in the engine and on the wire, same `orderId`), ruling out recompute, relay, or rounding hypotheses.

## Waivers

- Launch via apphost `Pricing.Engine.exe` / `Orders.Api.exe` (same build output) instead of `dotnet.exe <dll>`: the bridge rejects `dotnet.exe` (`invalid-argument: … is not a .NET 5+ app (no .runtimeconfig.json beside it)`). Launch evidence quoted: `{"processId":32244,"mode":"run","status":"ok",…}` (engine), `{"processId":8292,"mode":"run","status":"ok",…}` (frontend).
- Orders breakpoint at code line 13 (first statement after the pricing `await`) for comment line 11, so `priced` + `orderId` are observable at the stop; pricing breakpoint at code line 19 for comment line 18. Both reported `{"bound":true}` before launch.
- UC1 and first UC2 pricing stops were continued by an operator `resume` before inspection (`hitCount` `1` then `2` proves both stops); trigger 2 was repeated once with the identical payload — a read-only stale hit that neither stores nor clears — to capture the pricing stop with full `mode`/`lastHit` evidence. No edit, rebuild, or restart; cache poisoned once by trigger 1, never cleared. Both sessions `detach`ed; no target left stopped, no session left bound.
