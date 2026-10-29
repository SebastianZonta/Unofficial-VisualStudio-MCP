# 02: Correct order-to-price flow, no fault

**What to build:** The credible split working end to end with correct totals: the engine owns catalog plus quantity-tier plus coupon plus tax computation behind its price operation, and the frontend mints a fresh order identity per call and forwards to the engine, returning the identity plus the engine pricing fields.

**Blocked by:** 01 (needs both Solutions serving on their fixed ports).

**Status:** resolved

- [x] Both scripted orders return correct totals through the frontend, each with a distinct order identity proving separate calls.
- [x] No pricing logic is duplicated in the frontend; the engine is the single owner of price math.
- [x] Plain HTTP repro of the two orders is deterministic with no timing or UI dependence.

## Comments

Deviation recorded: the standalone "both correct, no fault" milestone was never
run — 02 and 03 were implemented as one edit (cache present from the start).
Correct-math ownership is still evidenced: order 1 returned the exact
hand-computed total (100 x 1 x 1.08 = 108.00, `cached: false`), and the
engine's formula is the only price math in the system. The "both correct"
state exists only as the counterfactual pinned in `HOST_NOTES.md` (order 2
should be 756.00).
