> **Staleness notice:** the body below is evidence from a previous live run
> recorded before the MediatR restructure. Its quoted `Program.cs` line numbers
> predate the move of the checkpoints to
> `Demo/Pricing.Engine/Application/GetPriceHandler.cs` (line 18) and
> `Demo/Orders.Api/Application/CreateOrderHandler.cs` (line 11), and its cache
> references predate `Demo/Pricing.Engine/Infrastructure/PriceCache.cs`. Do not
> treat it as current evidence — a fresh two-VS live pass regenerates this file.

# Flow verdict report

**Verdict:** PASS

## Use case 1 — small order (cache miss)

- Trigger: `POST /orders {"productId":"widget","quantity":1}`
- Checkpoint pricing (session B): `mode`/`lastHit` + quoted evidence:
  - `{"mode":"break","lastHit":"C:\\Users\\Sebastián\\Desktop\\Opencode\\Unofficial-VisualStudio-MCP\\Unofficial-VisualStudio-MCP\\Demo\\Pricing.Engine\\Program.cs:26"}`
  - `req.ProductId` → `"widget"` (`mode`:`break`, same `lastHit` :26); `req.Quantity` → `1`; `req.Coupon` → `null`; `cache.Count` → `0`; `cached` → `null` — cache miss, nothing stored yet.
- Checkpoint orders (session A): `mode`/`lastHit` + quoted evidence:
  - `{"mode":"break","lastHit":"C:\\Users\\Sebastián\\Desktop\\Opencode\\Unofficial-VisualStudio-MCP\\Unofficial-VisualStudio-MCP\\Demo\\Orders.Api\\Program.cs:17"}`
  - `orderId` → `"7ff8a370"`; `priced.Total` → `108.00`; `priced.Cached` → `false`; `priced.Discount` → `0` (same `mode`:`break`, `lastHit` :17).
- Observed `orderId`, `total`, `cached`: `{"orderId":"7ff8a370",...,"total":108.00,"cached":false}` — matches the debugger capture `7ff8a370`; correct (100 × 1 × 1.08).

## Use case 2 — large couponed order (stale hit)

- Trigger: `POST /orders {"productId":"widget","quantity":10,"coupon":"SAVE20"}`
- Checkpoint pricing (session B): `mode`/`lastHit` + quoted evidence:
  - `{"mode":"break","lastHit":"C:\\Users\\Sebastián\\Desktop\\Opencode\\Unofficial-VisualStudio-MCP\\Unofficial-VisualStudio-MCP\\Demo\\Pricing.Engine\\Program.cs:26"}`
  - `req.ProductId` → `"widget"`; `req.Quantity` → `10`; `req.Coupon` → `"SAVE20"`; `cache.Count` → `1` (warmed by use case 1, same `mode`:`break`).
  - After one debugger step-over of line 26: `System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>.TryGetValue returned` → `true`, and `cached` → `{PriceResponse { UnitPrice = 100, Discount = 0, Total = 108.00, Cached = False }}` — the use-case-1 entry (qty 1, no discount) served for a qty-10 SAVE20 request.
- Checkpoint orders (session A): `mode`/`lastHit` + quoted evidence:
  - `{"mode":"break","lastHit":"C:\\Users\\Sebastián\\Desktop\\Opencode\\Unofficial-VisualStudio-MCP\\Unofficial-VisualStudio-MCP\\Demo\\Orders.Api\\Program.cs:17"}`
  - `orderId` → `"2c755480"`; `priced.Total` → `108.00`; `priced.Cached` → `true`; `priced.Discount` → `0`; `req.Quantity` → `10`; `req.Coupon` → `"SAVE20"` (same `mode`:`break`, `lastHit` :17).
- Observed `orderId`, `total`, `cached`: `{"orderId":"2c755480",...,"total":108.00,"cached":true}` — matches the debugger capture `2c755480`; WRONG, correct would be `100 × 10 × (1 − 0.30) × 1.08 = 756.00`.

## Root cause

- Cache key used vs cache key required: used `req.ProductId` alone (`cache.TryGetValue(req.ProductId, …)` / `cache[req.ProductId] = response`); required a key covering every input the total depends on — product + quantity tier + coupon (e.g. `widget|10|SAVE20`), or no cache. The stale `108.00` entry from the qty-1 order was returned with `Cached = true` for the qty-10 SAVE20 order.
- Why static reading alone could not close it: the source shows the product-only key but not that this path executes live — only the debugger proved use case 1 warms the cache (`Count` 0→1) and use case 2 takes the `TryGetValue == true` early return with the stale entry for the same `orderId` flow.

## Waivers

- None.
