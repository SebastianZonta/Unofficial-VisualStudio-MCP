# E2E: every tool against a real VS, on the Sandbox

The unit tests prove the tool contracts against `FakeDebugBridge`. This pass
proves them against real VS 2026: every one of the 21 tools, every reachable
error code, and the situations the fake can't produce (COM identity, real
frames, the evaluator, threads, exceptions, two VS instances). The target is
`Sandbox/`, a .NET console app built for this pass. The pass starts its own VS
instances and never touches any other VS instance.

The pass runs as three concurrent runners (R1, R2, R3) against the same
revision, each with its own P0 setup plus its assigned slice
(see [Parallel run](e2e-runner.md#parallel-run-3-runners-sliced-phases)): R1 = P0 + P1–P4 + P9,
R2 = P0 + P5–P7 + P10, R3 = P0 + P8 + P11. Wall-clock target is ~7 minutes against the
~20 minute single-runner baseline; the first parallel run is the pilot and its
wall-clock is recorded. Phase H stays human-assisted and runs after the merge.

Bar: every non-OBS case PASS. One FAIL or BLOCKED fails the pass.

Contents: [Automated suite](#automated-suite-audit-should-fix) | [Runner](e2e-runner.md) (roles, parallel run, rules, slice map) | [Setup phases](e2e-phases-setup.md) (Sandbox reference, P0-P4) | [Debug phases](e2e-phases-debug.md) (P5-P9) | [Closeout](e2e-phases-closeout.md) (P10-P11, H, audit medium) | [Report](#report)

## Automated suite (audit-should-fix)

`tests/VsDebugMcp.E2E` drives the same Sandbox target through the same tools over
MCP stdio against the real server binary in live mode: one case per audit
behaviour (stable ids, frame-true inspection, read-only gate incl.
no-side-effects, no ghost resume, parallel attach, evaluate budget, attach mode,
one-target rule). Opt-in and VS-free by default:

```powershell
$env:VSDEBUG_E2E=1
$env:VSDEBUG_E2E_VS_A="!VisualStudio.DTE.18.0:<pidA>"  # from list_instances
$env:VSDEBUG_E2E_VS_B="!VisualStudio.DTE.18.0:<pidB>"  # optional: enables the two-VS cases
dotnet test tests/VsDebugMcp.E2E/VsDebugMcp.E2E.csproj
```

It never runs in CI. Busy-VS behaviour (a modal dialog open in VS) stays a manual
step: it is the `BusyVs_StillSurfacesAfterRetryWindow` case, marked manual in the
suite. The manual pass below remains the authority for everything the suite skips.

## Where to read

- **Main agent (orchestrator):** this index + [e2e-runner.md](e2e-runner.md) (Roles prompt, slice map, runner rules). Fan out exactly three runners (R1/R2/R3), merge partials, then run H yourself.
- **R1:** this index + [e2e-runner.md](e2e-runner.md) + [e2e-phases-setup.md](e2e-phases-setup.md) (P0-P4) + [e2e-phases-debug.md](e2e-phases-debug.md) (P9 only).
- **R2:** this index + [e2e-runner.md](e2e-runner.md) + [e2e-phases-setup.md](e2e-phases-setup.md) (P0 only) + [e2e-phases-debug.md](e2e-phases-debug.md) (P5-P7) + [e2e-phases-closeout.md](e2e-phases-closeout.md) (P10 only).
- **R3:** this index + [e2e-runner.md](e2e-runner.md) + [e2e-phases-setup.md](e2e-phases-setup.md) (P0 only) + [e2e-phases-debug.md](e2e-phases-debug.md) (P8 only) + [e2e-phases-closeout.md](e2e-phases-closeout.md) (P11 only).
- **Phase H / audit:** [e2e-phases-closeout.md](e2e-phases-closeout.md).

Slices: R1 = P0 + P1-P4 + P9, R2 = P0 + P5-P7 + P10, R3 = P0 + P8 + P11. Wall-clock target ~7 minutes; bar: every non-OBS case PASS.

## Report

Each runner writes its partial `.scratch/e2e-runs/<tag>-R<N>.md`
(gitignored) in the format below, covering only its slice. The main agent
merges the three partials into `.scratch/e2e-runs/<tag>.md` (merged counts
plus the combined failures/blocked and observations tables), appends phase H
to it, and triages from it — still exactly one verdict file per run.
Single-runner filename `<yyyy-MM-dd-HHmm>.md` is the run tag without a suffix.

```markdown
# E2E run <timestamp>
Change under test: <line>  ·  MCP binary: <path + write time>  ·  VS: <version>
Verdict: PASS | FAIL   (PASS n · FAIL n · BLOCKED n · OBS n)

## Failures and blocked
| Id | Expected | Actual (evidence) |

## Observations
| Id | What happened |

## All cases
| Id | Status | Evidence |
```

Return the verdict, the counts, every FAIL/BLOCKED row and the path.
