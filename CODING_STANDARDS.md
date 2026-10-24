# Coding standards

Judgement rules for review. Mechanical rules live in tests and the build
(`TreatWarningsAsErrors`, `DocsContractTests`), not here.

## Bridge parity

`FakeDebugBridge` is the contract the unit tests prove; `DteDebugBridge` must
match it. When a change adds a field to a shared result type (`ExecutionResult`,
`LaunchProcessResult`, …), check every path that produces that type, in both
bridges: each one sets the field, and a test drives each tool that returns it.
A field set on three of four paths is the typical miss (exception-config: the
fake's `launch_process` dropped `exception`).
Live is truth: when live COM disagrees with the fake (audit-oct24-high:
`Created` flag, honest `evaluate`, devenv guard), fix the fake and its tests
to match live, never the reverse.
Pre-audit gate: a bridge-behavior change must add or extend a case in
`tests/VsDebugMcp.E2E/AuditE2ETests.cs`, and the author runs it live
(`VSDEBUG_E2E=1`) before the manual 3-runner pass. Reject a bridge diff with
no E2E case and no live run. On any bug fix, state UT vs AuditE2E coverage
and add the missing case, or justify why neither fits.

## Shared rules, one home

Logic both bridges apply (validation, filtering, ordering, mode gates,
truncation) lives once in a shared static rules class (`ModuleListing`,
`ExceptionConfig`, `BreakpointOptions`), called from both bridges.

## Spec deviations

Behaviour that differs from the spec because the platform cannot do what it
asks gets an ADR in `docs/adr/` and a pointer from the spec line it contradicts.
