# Host notes (DO NOT show the audience)

## Coupon catalog (`Pricing.Engine/Domain/PriceCalculator.cs`, case-insensitive)

| Coupon | Discount |
|--------|----------|
| SAVE20 | 20% |
| SAVE10 | 10% |
| WELCOME5 | 5% |
| (null/empty) | 0% — valid, no coupon |

## Expected totals

- Trigger 1 `widget x1, no coupon` → `total 108.00`, `cached false` (correct:
  100 x 1 x 1.08).
- Trigger 2 `widget x10 + SAVE20` → `total 108.00`, `cached true` (WRONG;
  correct would be `756.00` = 100 x 10 x 0.70 x 1.08). Distinct `orderId`
  per call.

## Root cause (the agent must find this live)

Singleton `Pricing.Engine/Infrastructure/PriceCache.cs` is keyed by product
alone (`TryGet(request.ProductId, ...)` / `Store(request.ProductId, ...)`),
ignoring quantity and coupon. It is injected into
`Pricing.Engine/Application/GetPriceHandler.cs`, whose
`// CHECKPOINT (pricing)` cache lookup (line 18; lookup at line 19) serves the
stale entry with `Cached = true`. The orders-side stop is the
`// CHECKPOINT (orders)` line (line 11) in
`Demo/Orders.Api/Application/CreateOrderHandler.cs`, just before the pricing
HTTP call (line 12). Trigger 1 warms the entry; trigger 2 serves it stale.
The surrounding comments present the cache as a perf optimization, so static
reading stays ambiguous — only a stopped target process showing the
wrong-key hit plus the `cached: true` payload in the frontend proves it.

## Fix (for after the demo, not during)

Key the cache by product + quantity + coupon (or drop the cache). Do not apply
before the show — the fault is the feature.

## Error cases (validation relay — stretch only, not the main tour)

- Unknown coupon → engine 400 `{"error":"unknown coupon '<v>'"}`, relayed
  verbatim by orders with the same 400 status (via `EngineErrorException`).
- `quantity < 1` → 400 `{"error":"quantity must be >= 1"}`. Orders validates
  locally (`CreateOrderHandler`, no engine call); the engine validates too.
- Unknown product → engine 404, relayed verbatim by orders.
- Engine 5xx / transport failure → orders 502
  `{"error":"pricing engine rejected the order"}`.

## Machine prep

- Free ports 5201/5202 before showtime; on collision move the pair and re-pin
  this sheet, never patch live.
- Two VS 2026 instances, one Solution each; `dotnet build` both `*.slnx` first.
