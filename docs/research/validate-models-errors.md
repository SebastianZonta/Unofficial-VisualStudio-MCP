# Models/Errors Dedup Validation (no edits)

| Claim | YES/NO | Evidence |
|---|---|---|
| `DebuggerModes` centralizes mode gates in `DebugModels.cs` | YES | `src/VsDebugMcp/DebugModels.cs:7-20` — `Require`/`RequireNot` with single `Fail` throw site each |
| `ListFiltering` centralizes trim/substring/order in `DebugModels.cs` | YES | `DebugModels.cs:22-30` — `FilterAndOrder` trims, case-insensitive `Contains`, caller-supplied haystacks + order |
| `DebugDomainException.Fail` is the single factory | YES | `src/VsDebugMcp/DebugDomainException.cs:11` — `Fail(code, detail, remediation) => new(...)`; `ComErrorMapper`/`ComDispatcher`/bridges still use `new` directly for non-domain (COM/dispatcher/infra) failures, which is out of scope |
| `ExceptionModels.cs` delegates to shared helpers, same ordering | YES | `src/VsDebugMcp/ExceptionModels.cs:30-47` — `RequireNotRunning`→`DebuggerModes.RequireNot`, new `ValidateMode`, `NotFound`→`Fail`, `FilterAndOrder`→`ListFiltering` with `OrderBy Ordinal` (unchanged) |
| `ModuleModels.cs` delegates to shared helpers, same ordering | YES | `src/VsDebugMcp/ModuleModels.cs:21-32` — `RequireDebugging`→`DebuggerModes.Require` (break/run), `FilterAndOrder`→`ListFiltering` with identical 4-key ordering |
| `BreakpointOptions` factories use `Fail` | YES | `src/VsDebugMcp/BreakpointOptions.cs:12,190,201,218,222` — `ValidateLocation`, `Exists`, `RequireSameOptions`, `NotFound` all via `Fail` |
| `BreakpointOptions.Normalize` is a switch | YES | `BreakpointOptions.cs:59-64` — `(null,_)=>null`, named-mode passthrough, default `Exact` |
| `BraceSpans` is the single brace scan | YES | `BreakpointOptions.cs:161,153,155,236` — `FormatLogMessage`, `Holes`, `BracesBalanced` all consume `BraceSpans`; only `IndexOf('}')` left is inside `BraceSpans` itself |
| `Program.cs` uses a dispatch table | YES | `src/VsDebugMcp/Program.cs:7-19` — `(Flag, Run)[]` tuple array + loop, 7 entries |
| `LiveCheck.cs` has shared `Rest`/`Usage`/`ReportDomain`/`Check` helpers | YES | `src/VsDebugMcp/LiveCheck.cs:436-469` — `Rest`, `Usage` (exit 2), `ReportDomain` (`DOMAIN {code}`), `Check(ref failures,...)`, plus `FailsWith` used by audit/exception checks |
| Public wire contracts / JSON shapes unchanged | **NO — 2 exceptions** | `SymbolStates.Failed` const removed (`ModuleModels.cs` diff: `-public const string Failed`); `IDebugBridge.AttachProcessAsync` gained optional `expectedProcessName = null`. Records (`AttachResult`, `ModuleInfo`, `ExceptionSettingInfo`, result envelopes with `Mode`/`LastHit`) and `ErrorCodes` (still `const string`, not enum) unchanged |
| No envelope reshape, no const→enum | PARTIAL | No record-shape/envelope changes found in diff; `ErrorCodes.cs:5-33` still `static class` + `const string`. But see `SymbolStates.Failed` removal above (a wire value narrowing, not a reshape) |
| `dotnet build VsDebugMcp.slnx` | YES | Build succeeded, 0 errors (1 pre-existing MSB3026 retry warning: test dll locked by `testhost`) |
| `dotnet test VsDebugMcp.slnx --no-build` | **NO (partial)** | `VsDebugMcp.Tests`: **330/330 passed**. `VsDebugMcp.E2E`: 9 skipped + **test-host process crashed / run aborted** (E2E needs live VS; same abort on full-sln run) |
