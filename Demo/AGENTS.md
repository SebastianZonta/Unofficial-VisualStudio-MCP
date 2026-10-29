# Demo: poisoned-cache two-app debug (trigger word: `start`)

You are the demo agent. When the host says `start`, run this whole tour yourself
and end with a verdict report. Do not ask for confirmation mid-tour; narrate
what you do as you do it.

## Mission

Two ASP.NET Core apps run as separate targets: `Orders.Api` (`:5201`, frontend)
depends over HTTP on `Pricing.Engine` (`:5202`, pricing rules + cache). Order 2
returns a plausible-but-wrong total. Find the root cause **only through the
debugger** (reading code is allowed to navigate, never to conclude) and prove
it with quoted tool evidence.

## Rules

- Do NOT edit, rebuild, or restart either app mid-tour. The fault is the
  feature; the fix belongs to after the show.
- Do NOT clear the cache by restarting to "check something" — a restart
  destroys the evidence. One repro sequence, top to bottom.
- Every claim in your verdict must carry quoted `mode`/`lastHit` evidence.

## Tour

1. **Build** (if not already built):
   `dotnet build Demo/Pricing.Engine/Pricing.Engine.slnx --nologo -v q` and
   `dotnet build Demo/Orders.Api/Orders.Api.slnx --nologo -v q`.
2. **Discover**: `list_instances` — expect two VS instances, one per Solution.
   Optionally `list_processes` to show the machine state before anything runs.
3. **Bind**: `attach` to the Pricing VS instance (debug session B), `attach`
   to the Orders VS instance (debug session A). One session per instance.
4. **Breakpoints before launch** (this is the point — set them first):
   - Session B: breakpoint at the `// CHECKPOINT (pricing)` cache lookup.
   - Session A: breakpoint at the `// CHECKPOINT (orders)` line after the
     pricing HTTP call.
5. **Launch** (one live target per debug session):
   - Session B: `launch_process` the engine (`dotnet` exe, args =
     `Demo/Pricing.Engine/bin/Debug/net10.0/Pricing.Engine.dll`, working
     directory = `Demo/Pricing.Engine`).
   - Session A: `launch_process` the frontend (`dotnet` exe, args =
     `Demo/Orders.Api/bin/Debug/net10.0/Orders.Api.dll`, working directory
     = `Demo/Orders.Api`).
   - Confirm each launch result carries the new target PID plus mode/lastHit.
6. **Repro** (in this order, plain HTTP — narrate expected vs actual):
   - Trigger 1: `POST http://localhost:5201/orders`
     `{"productId":"widget","quantity":1}` → expect `total 108.00`,
     `cached false`.
   - Trigger 2: `POST http://localhost:5201/orders`
     `{"productId":"widget","quantity":10,"coupon":"SAVE20"}` → observe what
     comes back (correct would be `756.00`).
7. **Inspect at each stop**: `read_stack`, `read_variables`, `evaluate` the
   cache key + stored entry (session B) and the pricing payload + `orderId`
   (session A). The `orderId` is your capture — quote it to prove both stops
   belong to the same flow. `continue` after each stop.
8. **Close**: `continue` to completion, `detach` both sessions. No target left
   stopped, no session left bound.

## Verdict

Write `Demo/REPORT.md` following `Demo/REPORT_TEMPLATE.md`: PASS/FAIL with
quoted `mode`/`lastHit` per checkpoint, the cache key used vs the cache key
required, and one line on why static reading alone could not close it. A PASS
without quoted evidence does not count. End your turn with the verdict word.
