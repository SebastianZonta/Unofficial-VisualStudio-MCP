# Demo script (audience-facing, ~10 min)

Two Solutions, one VS instance each: `Orders.Api` (http://localhost:5201) and
`Pricing.Engine` (http://localhost:5202). No code is changed during the demo.

## 1. Discover (session-less)

- `list_instances` — show both VS instances.
- `list_processes` (optional) — show the two target processes.

## 2. Bind (one debug session per VS instance)

- `attach` to the `Orders.Api` VS instance → debug session A.
- `attach` to the `Pricing.Engine` VS instance → debug session B.

## 3. Checkpoints (one breakpoint per target process)

- Session B: breakpoint at the `// CHECKPOINT (pricing)` cache lookup in `Program.cs`.
- Session A: breakpoint at the `// CHECKPOINT (orders)` line after the pricing call in `Program.cs`.

## 4. Repro (triggers, plain HTTP — run in this order)

Trigger 1 (cache miss, correct):

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5201/orders `
  -ContentType 'application/json' -Body '{"productId":"widget","quantity":1}'
```

Trigger 2 (stale hit, wrong):

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5201/orders `
  -ContentType 'application/json' -Body '{"productId":"widget","quantity":10,"coupon":"SAVE20"}'
```

## 5. Inspect (at each stop)

- `read_stack`, `read_variables` — show where you stopped.
- `evaluate` the cache key and the stored entry (session B), the pricing
  payload and the `orderId` (session A). The `orderId` is the capture proving
  both stops belong to the same flow.
- `continue` after each stop.

## 6. Close

- `continue` to completion, then `detach` both debug sessions.
- No target process left stopped, no debug session left bound.

## 7. Verdict

Fill `REPORT.md` from `REPORT_TEMPLATE.md`: PASS/FAIL backed by quoted
`mode`/`lastHit` per checkpoint. A PASS without quoted evidence does not count.
