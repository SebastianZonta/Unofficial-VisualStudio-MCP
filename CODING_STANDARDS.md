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
A verbatim copy in `DteDebugBridge` and `FakeDebugBridge` (the four mode
guards were the live case) means the shared home exists but a caller lags
it: move the logic, delete the copies, point both callers at the home.

## No COM train wrecks

Reach COM through one named helper per hop (`SessionDte` → `DebuggerOf` →
`ModeOf`/`LastHitOf`). A call site names the helper, never the chain.
Two-deep `dte.Solution?.FullName` / `dte.Solution?.Projects` is the current
ceiling: fold the next chain into a helper (e.g. `SolutionOf`) rather than
adding a third.

## Spec deviations

Behaviour that differs from the spec because the platform cannot do what it
asks gets an ADR in `docs/adr/` and a pointer from the spec line it contradicts.

## Formatting

Readability rules for review; `.editorconfig` at the root is the mechanical
half (CI's format gate runs `dotnet format whitespace` on changed `.cs`
files only — full `dotnet format` also reports pre-existing analyzer
suggestions, so the gate stays whitespace-scoped), this section is the
judgement half.

- Purpose-blocks breathe: one blank line before and after each `if`/`for`/`while`/`try`
separating it from surrounding statements; statements serving one goal stay
together with no blank lines inside.
- Signatures: 0–1 params stay on the method line; 2+ params go one per line,
indented, closing paren on its own line. New code follows it; existing long
signatures reflow only when their file is touched for another reason.
- Sugar: collapsed returns (`return a || b();`), target-typed `new`, and
collection expressions where the file already uses them. The `.editorconfig`
already carries them as warnings (they surface in the IDE, never break the
build since `EnforceCodeStyleInBuild` is unset); promote one to `error` only
with a cleanup commit that fixes every hit.
