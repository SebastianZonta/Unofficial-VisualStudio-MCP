## Problem Statement

Adding features keeps re-breaking the pass for the same reasons: the MCP binary under test is stale (publish-to-dirty-dir, exe locked while hosts run), the fake unit tests stay green while live VS disagrees, and the "after a feature" obligations (ADR, TROUBLESHOOTING entry, e2e case, contract test, coverage statement) live as one overloaded bullet so agents satisfy 2 of 5 and drop the rest. E2E orchestration is stated 3 times, so runners skip slices or touch another runner's VS instances.

## Solution

One small hardening slice that makes the correct path the easy path: a single publish script both humans and E2E preflight use, a single Definition of Done checklist `AGENTS.md` points at, a single status enum in the issue tracker, and a CI gate that rejects a bridge diff with no `AuditE2ETests` case. No new tools, no architecture change.

## User Stories

1. As a feature agent, I want one publish command that always produces a fresh binary, so that I stop triaging stale-binary false positives.
2. As a feature agent, I want a 5-line Definition of Done checklist, so that I don't drop the ADR / TROUBLESHOOTING / e2e / contract-test step.
3. As an E2E orchestrator, I want the slice map and prompt template in one runbook file, so that R1/R2/R3 fanout stops drifting across three copies.
4. As a reviewer, I want CI to fail when `DteDebugBridge.cs` / `FakeDebugBridge.cs` / gate files change without an `AuditE2ETests.cs` change, so that fake/live parity stops relying on prose.
5. As a triager, I want one `Status:` vocabulary in one file, so that I stop guessing between `claimed`/`resolved` and `needs-triage`/`ready-for-agent`.
6. As a maintainer, I want marker-table and docs-index drift caught by a test, so that a scenario rename fails fast instead of BLOCKING an E2E run hours later.

## Implementation Decisions

- Seams (highest first, existing preferred): CI workflow file (presence gate, no VS needed); `DocsContractTests` suite (wire-format + throw-site coverage, already runs in CI); `IDebugBridge` result-type reflection for the parity snapshot (unit-only, no VS); `scripts/publish-fresh.ps1` as the single publish path used by local E2E preflight (CI keeps its own inline hygiene step that inspired it); `docs/agents/definition-of-done.md` as the single checklist `AGENTS.md` points at (progressive disclosure, no restatement).
- GLOSSARY vocabulary used throughout: VS instance, debug session, target process, mode/lastHit, runner, slice, run tag, VS PID vs target PID, mine/consent.
- Respects ADRs: ADR-0010 (`, nse` stays backstop, never the primary gate); ADR-0011 (exception breaks via `LastBreakReason` + `$exception`); ADR-0012 (strict literal coercion + re-read in `VariableWrite`).
- `publish-fresh.ps1`: takes `-OutDir`, removes it, publishes Release win-x64 self-contained, asserts `System.Management.dll` > 100KB, prints path + write time for the run-report `MCP binary:` field, refuses to write into a directory whose exe is locked by a running host.
- Definition of Done replaces `AGENTS.md` publish bullet and after-feature bullet with one pointer line each; canonical coverage rule stays in `CODING_STANDARDS.md` only.
- Tracker: fold the five `Status:` values into `docs/agents/issue-tracker.md`; delete `docs/agents/triage-labels.md`; update the three pointers (`AGENTS.md`, `.scratch/README.md`, `docs/README.md`) to the single file.
- E2E runbook split is deferred to a follow-up issue (keeps this slice docs-small and reviewable); this spec only adds the P0 hygiene lines (`Unblock-File`, delete `Sandbox/.vs`, Z3 empty-filter assert) inline.
- No new MCP tools, no bridge behavior change, no `Sandbox/` scenario change in this slice.

## Testing Decisions

- Good tests prove external behavior at the highest seam: CI green/red, script exit code + report line, contract-test failure on drift — never implementation prose.
- Modules tested: CI gate (static: bridge file list vs `AuditE2ETests.cs` diff); `DocsContractTests` extensions (error-code throw-site presence, envelope shape per path); `publish-fresh.ps1` (placeholder-size assert, locked-exe refusal) verified by dry run, not by launching VS.
- Prior art: existing `DocsContractTests` (tool/error-code rows, tool counts), CI publish-hygiene step (size check + `--live-check` exit-2 probe), `E2EFixture` skip-without-`VSDEBUG_E2E=1` convention.
- Live VS pass is out of scope for this slice; author states UT vs `AuditE2ETests` vs manual coverage per `CODING_STANDARDS.md` pre-audit gate.

## Out of Scope

- Single shared expression-gate theory table for evaluate/condition/logpoint/drill-path (own spec).
- `vs-busy` ownership/idempotency ADR and per-session dispatcher lanes (own spec).
- E2E runbook file split and prompt-template generator script (follow-up issue in this spec).
- Marker-table codegen and full docs-index contract test (follow-up; this slice only pins the CI presence gate).
- Any bridge, tool-surface, `Sandbox/`, or ADL behavior change.

## Further Notes

- Slice map unchanged: R1 = P0 + P1–P4 + P9, R2 = P0 + P5–P7 + P10, R3 = P0 + P8 + P11.
- If CI lacks `origin/main` depth, use merge-base or `fetch-depth: 0` for the diff gate.
- `standalone*/` dirs stay gitignored publish generations; script defaults to a fresh empty dir outside the repo or a new `standalone-v<N>` the caller names.
