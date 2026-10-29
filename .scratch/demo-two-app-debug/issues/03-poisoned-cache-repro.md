# 03: Poisoned cache plus deterministic two-order repro

**What to build:** The runtime-only fault plus its proof: the engine caches by product alone so the first order poisons the entry and the second order for the same product with different quantity and coupon gets a stale plausible-but-wrong total, reproducible with plain HTTP calls and unresolvable by static reading alone.

**Blocked by:** 02 (needs the correct flow working before the fault goes in).

**Status:** resolved

- [x] First order is a cache miss with the correct total; second order same product different quantity/coupon is a stale cache hit with the documented wrong total and no error returned.
- [x] Static code reads as intentional perf caching with nothing logging or confessing the key collision.
- [x] The order identity from the frontend correlates both stops to one flow rather than background work.

## Comments

Live repro this session (both services running):
order 1 `widget x1` → `orderId 666aa0d6, total 108.00, cached false` (correct);
order 2 `widget x10 + SAVE20` → `orderId fa9ab3d2, total 108.00, cached true`
(stale — correct would be 756.00). Distinct orderIds, no error on either call,
nothing in code or logs confesses the product-only key.
