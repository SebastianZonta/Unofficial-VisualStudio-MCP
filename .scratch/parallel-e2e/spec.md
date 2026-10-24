# Spec: parallel E2E (3 runners, sliced phases)

Status: ready-for-agent

## Problem Statement

The real-VS end-to-end pass takes almost 20 minutes under a single runner, and it keeps growing: the toolset went from 16 to 21 tools and the pass is now 123 cases across phases P0–P11 plus a human-assisted phase H. Every feature pays this tax, so the pass is becoming the bottleneck of every change. The pass is strictly sequential today — one runner, one VS instance, one debug session, one target process at a time — even though its phases contain independent chains that could run concurrently.

## Solution

Split the pass into three slices executed by three concurrent runners against the same revision. Each runner is a subagent with its own VS instances, its own debug session, its own target process and its own partial report; each runs its own P0 setup then its assigned slice. The maintainer's client still points at one freshly published binary, the Sandbox source stays single, per-run temp copies give each runner distinct file paths, the three name-global assertions become PID-scoped, and the main agent merges the three partial reports into one verdict file before running phase H with the user.

## User Stories

1. As the maintainer, I want the full P0–P11 pass wall-clock down from ~20 minutes to ~7 minutes, so that verifying a feature stops dominating the change cycle.
2. As the maintainer, I want the case count per slice roughly even (43/41/43), so that no single runner dominates the wall-clock.
3. As runner R1, I want my own P0 setup plus phases P1–P4 (discovery, session lifecycle, breakpoint identity, conditions) and P9 (attach_process against my own wait PID), so that I never wait on another runner's session or target.
4. As runner R2, I want my own P0 setup plus phases P5–P7 (inspection, frames, stepping), so that P5's single shared stopped target stays inside one runner where its write-last ordering holds.
5. As runner R3, I want my own P0 setup plus phases P8, P10–P11 (execution control and launch, two-VS, cleanup), so that P8's live-target chain and P10's self-contained two-VS flow stay inside one runner.
6. As a runner, I want my own VS instances that I start and no other runner touches, so that attach, breakpoints and exception settings never cross-talk (the existing never-touch-another-runner's-instances rule, extended to three runners).
7. As a runner, I want a distinct Sandbox file path (temp copy of the single Sandbox source, following the established SandboxB pattern), so that breakpoint file:line identity, lastHit assertions and the two-VS path-distinction check stay attributable and a leaked breakpoint in one runner cannot flake another.
8. As the maintainer, I want exactly one Sandbox source in the repo, so that scenario edits stay a one-file change plus the marker table and copies can never drift out of sync.
9. As the maintainer, I want the main agent to create the two temp copies at run start and runners to delete their copies in cleanup, so that no per-run machinery leaks onto disk.
10. As runner R1, I want case D3 scoped to my own recorded wait PID, so that the exactly-one-sandbox-process assertion holds while two other runners have live targets.
11. As runner R3, I want case E14 scoped to my own PIDs, so that the nothing-new-named-Sandbox assertion holds while other runners' targets are alive.
12. As runner R3, I want case Z3 scoped to my own PIDs, so that the empty-filter assertion verifies my cleanup without tripping on other runners' processes.
13. As a runner, I want to write my own partial report file with a run tag plus my R-suffix, so that two runners finishing in the same minute can never collide on one filename.
14. As the main agent, I want to merge the three partials into one verdict file with merged counts, so that there is still exactly one pass report per run.
15. As the maintainer, I want the pass verdict to be PASS only if every non-OBS case passes in all three slices, so that partitioning never weakens the bar (one FAIL or BLOCKED anywhere fails the pass).
16. As the user, I want phase H unchanged (main agent plus me, appended to the merged report), since human-assisted cases cannot be delegated to a subagent.
17. As a future agent, I want the canonical terms runner, slice and run tag defined in the domain glossary, so that the e2e doc, the tracker and the code all mean the same thing by them.
18. As the maintainer, I want the existing retry budget (retry once on vs-busy or timeout, never retry an assertion) and launch/resume timeout windows unchanged, so that parallelization changes procedure only, never strictness.
19. As the maintainer, I want the exception-settings restore chain to keep its must-run-cleanup semantics inside R3, so that a failing exception case can never leak a mutated CLR exception mode.
20. As the maintainer, I want each runner's cleanup to kill only the PIDs it started, so that one runner's teardown can never murder another runner's target or VS (the existing only-those-you-started rule, unchanged).
21. As the maintainer, I want DocsContractTests and the full unit suite green before and after, so that the procedure change provably touches no tool contract.
22. As the maintainer, I want the first parallel run treated as a pilot with its wall-clock measured against the ~20 minute baseline, so that the speedup claim is evidence, not arithmetic.

## Implementation Decisions

- Docs-only change: no production code, no protocol, no tool behavior, no case expectations change. The modules touched are the e2e procedure doc, the domain glossary (three new terms), and the agent entrypoint's E2E bullet (three-runner fan-out wording).
- Slice boundaries are fixed by the unbreakable chains: R1 = P0 + P1–P4 + P9, R2 = P0 + P5–P7, R3 = P0 + P8 + P10–P11. P5's shared stopped target, P8's live-target chain (E1 through E20 with the already-bound probe requiring the E1 target alive) and P8's exception M0 chain (E21–E25) never cross a runner boundary; P9's only coupling is the P0 wait PID, which every runner mints for itself, so it rides with R1.
- Per-runner P0: each runner builds, resolves marker lines dynamically (never hardcodes), starts its own VS, records its own wait PID. Staggering of the three P0 builds is not required (distinct temp-copy dirs, no shared bin lock); VS startup polling keeps its existing 10 s / 3 min budget.
- Temp copies extend the established second-copy pattern (used today for the two-VS phase): the main agent copies the Sandbox source to two temp dirs at run start; R2/R3 use them; each runner deletes its copy in cleanup. The repo keeps exactly one Sandbox source.
- PID scoping is surgical: D3, E14 and Z3 filter by the runner's own recorded PIDs instead of asserting on bare process names. Every other case, table and budget is byte-identical.
- Report mechanics: partials named with the shared run tag plus R-suffix; the main agent merges counts and the failures/blocked tables into one verdict file, then appends phase H. The minute-granularity filename collision is retired by construction.
- Runner prompts: one template, three instantiations differing only in slice (phase range) and run tag. Model per host: the opencode contributor model under opencode, sonnet under Claude Code (both references stay in the docs).
- No ADR: procedure-only, fully reversible by running the old single-runner flow, no surprising trade-off for a future reader beyond what the e2e doc itself records.
- Respects existing ADRs in the touched area: launch-without-consent semantics, read-only evaluate, exception-break detection and the write-only set_variable contract are all untouched; only assertion scope changes.

## Testing Decisions

- A good test here tests external behavior only: a green merged verdict plus a measured wall-clock, never the internal wording of the procedure doc.
- Highest existing seam, no new seams: the parallel E2E pass itself is the test (one seam, the same one the procedure already uses). DocsContractTests plus the full unit suite are the guardrails that must stay green to prove no contract moved.
- Prior art: the current single-runner pass (same tables, same bar) is the oracle for correctness; the pilot parallel run is the experiment for speed.
- Acceptance: one full three-runner pilot run, every non-OBS case PASS in all slices, merged verdict PASS, wall-clock recorded against the ~20 minute baseline, temp copies confirmed deleted, no stray Sandbox or VS processes left behind.
- First-run watch list (observations, not failures unless they flake cases): CPU contention from three concurrent VS startups, the P10 two-VS spike (four VS instances plus the maintainer's own), and any leaked-breakpoint or PID-reuse surprise the PID-scoped assertions were designed to contain.

## Out of Scope

- Automating phase H; it stays human-assisted by design.
- Any change to tool behavior, tool count, error codes, timeouts, retry budgets or the PASS bar.
- Permanent second/third Sandbox projects in the repo; temp copies only, created and destroyed per run.
- CI parallelization of the pass; this spec covers the maintainer-driven flow only.
- Staggering runners to preserve the name-global assertions; the agreed direction is PID scoping.
- New MCP tools, new harnesses, new seams.

## Further Notes

- The minute-granularity report collision and the leaked-breakpoint flake risk were found during design analysis; both are retired by the R-suffix reports and temp copies respectively, not by luck.
- If the pilot shows VS-startup contention dominating, the follow-up is staggering P0 starts, explicitly not re-merging slices.
