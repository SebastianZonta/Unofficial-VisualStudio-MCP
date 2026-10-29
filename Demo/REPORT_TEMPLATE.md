# Flow verdict report

**Verdict:** PASS / FAIL (delete one)

## Use case 1 — small order (cache miss)

- Trigger: `POST /orders {"productId":"widget","quantity":1}`
- Checkpoint pricing (session B): `mode`/`lastHit` + quoted evidence:
- Checkpoint orders (session A): `mode`/`lastHit` + quoted evidence:
- Observed `orderId`, `total`, `cached`:

## Use case 2 — large couponed order (stale hit)

- Trigger: `POST /orders {"productId":"widget","quantity":10,"coupon":"SAVE20"}`
- Checkpoint pricing (session B): `mode`/`lastHit` + quoted evidence:
- Checkpoint orders (session A): `mode`/`lastHit` + quoted evidence:
- Observed `orderId`, `total`, `cached`:

## Root cause

- Cache key used vs cache key required:
- Why static reading alone could not close it:

## Waivers

- None (or list with one-line reason each).
