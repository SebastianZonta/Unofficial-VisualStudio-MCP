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

## Shared rules, one home

Logic both bridges apply (validation, filtering, ordering, mode gates,
truncation) lives once in a shared static rules class (`ModuleListing`,
`ExceptionConfig`, `BreakpointOptions`), called from both bridges.

## Spec deviations

Behaviour that differs from the spec because the platform cannot do what it
asks gets an ADR in `docs/adr/` and a pointer from the spec line it contradicts.
