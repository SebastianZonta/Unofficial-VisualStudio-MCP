# Host notes (DO NOT show the audience)

## Expected totals

- Trigger 1 `widget x1, no coupon` → `total 108.00`, `cached false` (correct:
  100 x 1 x 1.08).
- Trigger 2 `widget x10 + SAVE20` → `total 108.00`, `cached true` (WRONG;
  correct would be `756.00` = 100 x 10 x 0.70 x 1.08). Distinct `orderId`
  per call.

## Root cause (the agent must find this live)

`Pricing.Engine/Program.cs` caches by product alone
(`cache.TryGetValue(req.ProductId, ...)`), ignoring quantity and coupon.
Trigger 1 warms the entry; trigger 2 serves it stale. The surrounding comments
present it as a perf optimization, so static reading stays ambiguous — only a
stopped target process showing the wrong-key hit plus the `cached: true`
payload in the frontend proves it.

## Fix (for after the demo, not during)

Key the cache by product + quantity + coupon (or drop the cache). Do not apply
before the show — the fault is the feature.

## Machine prep

- Free ports 5201/5202 before showtime; on collision move the pair and re-pin
  this sheet, never patch live.
- Two VS 2026 instances, one Solution each; `dotnet build` both `*.slnx` first.
