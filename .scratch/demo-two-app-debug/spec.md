# demo-two-app-debug

## Problem Statement

Tomorrow's demo needs to show why this MCP server exists: an agent debugging a failure that cannot be solved by reading code alone. A single-app bug lets the audience say "I could have grepped that". We need two separately debuggable programs with a plausible reason to be separate, plus one deterministic runtime-only fault that forces the agent to bind two VS instances, stop two target processes, correlate one flow across both, and report a verdict with quoted tool evidence.

## Solution

Ship a Demo with two Solutions, each opened in its own VS instance and debugged through its own debug session: an Orders frontend API that depends over HTTP on a Pricing backend engine. The engine owns catalog prices, quantity tiers, coupon rules, tax, and an in-memory cache; the frontend owns order identity and forwards pricing calls. A cache-key fault in the engine (keyed by product only, ignoring quantity and coupon) poisons later orders: the code reads as a deliberate perf optimization, the HTTP payloads look plausible, and only live inspection in both target processes proves the stale hit. The demo runs a fixed slow MCP tour (discover, attach x2, breakpoint x2, repro, inspect, resume, detach) and ends in a verdict report carrying mode/lastHit evidence.

## User Stories

1. As a demo host, I want two Solutions each open in its own VS instance, so that the agent must use multi-attach rather than a single debugger.
2. As a demo host, I want the Orders Project to depend on the Pricing Project over HTTP, so that the split into two Solutions is credible and not contrived.
3. As a demo host, I want fixed localhost ports for both target processes, so that repro commands are deterministic on the demo machine.
4. As a demo host, I want a price contract covering product, quantity, and coupon in and unit price, discount, total, and cached flag out, so that the poisoned second order is visibly wrong yet plausible.
5. As a demo host, I want catalog plus quantity-tier plus coupon plus tax rules in the engine, so that the engine justifies existing as a separate service.
6. As a demo host, I want order identity minted in the frontend, so that the cross-process flow has a capture to correlate checkpoints.
7. As a demo viewer, I want a two-order repro where order one poisons order two, so that I see correct-then-wrong without any code change between calls.
8. As a demo viewer, I want the fault to read as intentional caching when browsed statically, so that I accept it cannot be closed by reading alone.
9. As a demo viewer, I want the slow MCP tour to show discovery before attach, so that I see VS instances enumerated before any debug session binds.
10. As a demo viewer, I want one debug session bound per VS instance, so that I see the session-to-instance ownership the glossary promises.
11. As a demo viewer, I want a breakpoint in each target process, so that I see both programs stop on the same flow.
12. As a demo viewer, I want stack plus variables plus evaluate on each stop, so that I see the full inspect vocabulary, not just break and continue.
13. As a demo viewer, I want the stale cache key evaluated live, so that the root cause lands as debugger output rather than presenter assertion.
14. As a demo viewer, I want resume plus detach to close the run cleanly, so that no target process is left stopped and no debug session leaks.
15. As a demo agent, I want a deterministic repro I can re-run with plain HTTP calls, so that I never depend on timing or UI clicks on stage.
16. As a demo agent, I want the expected wrong total documented for the host only, so that I can still be graded on finding it live without the audience being spoiled.
17. As a demo agent, I want a blank verdict template requiring quoted mode/lastHit evidence per checkpoint, so that my report proves I stopped where I claim.
18. As a repo maintainer, I want the Demo kept outside the main Solution like the existing Sandbox target, so that analyzers and contract tests never treat demo code as product code.

## Implementation Decisions

- Two Solutions, one Project each: an Orders frontend Solution and a Pricing engine Solution. Each Solution opens in its own VS instance; each VS instance binds exactly one debug session; each debug session owns at most one live target process. Glossary terms Solution, Project, VS instance, debug session, target process, VS PID versus target PID apply throughout.
- Split rationale: pricing rules (catalog lookup, quantity tiers, coupon acceptance, tax computation, in-memory caching) live in the engine; order identity and request fan-out live in the frontend, which calls the engine over HTTP. No database, no auth, no message bus — the smallest split that still justifies two deployables.
- API contracts: the engine exposes a price operation taking product, quantity, and coupon and returning unit price, discount, total, and a cached flag; the frontend exposes an order operation taking the same inputs and returning a fresh order identity plus the engine's pricing fields. Fixed localhost ports are part of the contract so repro and breakpoints are stable.
- The injected fault is a cache-key fault: the engine caches by product alone instead of by product plus quantity plus coupon, and serves stale totals on key collision. The surrounding code presents it as a perf optimization; no error is returned and no log confesses the collision, so static review stays ambiguous and only a stopped target process shows the wrong-key hit.
- Canonical flow for the demo: first use case places a small uncoploned order (cache miss, correct total, populates the poisoned entry); second use case places a large couponed order for the same product (cache hit, wrong total). The order identity from the frontend is the capture that lets the agent assert both stops belong to the same flow rather than background work.
- Slow MCP tour is fixed: session-less discovery, attach once per VS instance, one breakpoint per target process at the checkpoint locations, trigger the two use cases via HTTP, inspect via stack plus variables plus evaluate at each stop, continue to completion, detach both debug sessions. Logpoints, conditional breakpoints, hit counts, and exception breaks are explicitly not on the tour — the tour shows the floor every later feature stands on.
- Demo script and verdict template are part of the feature: the script lists triggers and checkpoints in order with wait budgets; the template requires a PASS/FAIL verdict backed by quoted mode/lastHit per checkpoint. The host guide holds expected totals; the audience-facing script does not spoil the root cause.
- No product-code seam changes: the debug bridge interface, the Fake bridge, and all MCP tool shapes are untouched. The Demo exercises existing seams only.

## Testing Decisions

- A good test for this feature asserts externally observable behavior only: HTTP status and totals for the two-order sequence, and that the documented MCP tour completes against real VS instances. No assertions on private cache fields, COM call order, or debugger internals.
- Tested surfaces: the price and order HTTP contracts via plain HTTP repro (miss then poisoned hit); the demo tour via a manual live pass binding two VS instances and walking the scripted checkpoints to verdict. The highest seam is the HTTP boundary plus the MCP tool boundary; no new seam is introduced and none is needed.
- Prior art: the live-verify harnesses for attach plus breakpoints (per-tool verify lines), and the Sandbox pattern of keeping debug targets outside the main Solution with their own build props and no analyzers. No Fake-bridge unit test fits — the fault is deliberately a live-state fault — so coverage is the manual two-VS live pass recorded in the verdict report.
- Regression guard is social, not automated: the host guide pins the two expected totals (correct first order, stale second order) so any accidental fix or port drift fails the repro before showtime.

## Out of Scope

- Any change to MCP tools, bridge seams, Fake bridge, contract tests, or troubleshooting docs.
- Fixing the cache fault in the shipped Demo (the fault is the feature; the fix belongs to the live agent on stage).
- Databases, auth, message buses, containers, or remote debugging.
- Additional flows, use cases, coupons, tiers, or tax locales beyond the two scripted orders.
- Persisted launch configs, E2E phase files, or ADR entries unless the Demo forces a product-code change (it must not).

## Further Notes

- Keep the Demo minimal enough to build and rehearse in one evening on .NET 10 / VS 2026 / Windows; every extra rule or endpoint is stage risk.
- Ports must be verified free on the demo machine at rehearsal; on collision the contract moves as a pair and the script is re-pinned, never patched live.
- The verdict report from the live pass is the demo's proof artifact; a PASS without quoted mode/lastHit per checkpoint does not count.
