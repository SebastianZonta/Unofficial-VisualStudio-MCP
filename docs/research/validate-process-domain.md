# Validate process-domain dedup fixes

Read-only validation; no source edits. Paths: `src/VsDebugMcp/*.cs`.

| claim | YES/NO | evidence |
|---|---|---|
| ArgumentValidator IsAbsolutePath one-liner | YES | `ArgumentValidator.cs:19-20` expression-bodied `!IsNullOrWhiteSpace && IsPathFullyQualified` |
| ArgumentValidator RequireNonEmpty/RequireRange one-liners | PARTIAL | Helpers themselves are 4-5-line bodies (`ArgumentValidator.cs:22-32`); all call sites are `=>` one-liners (e.g. `:34-38`, `:96-100`) funneling through `Fail` |
| ArgumentValidator verbatim messages | YES | e.g. `sessionId must not be empty; remediation: call attach first` (`:35`); `executablePath must be an absolute path; remediation: ...` (`:114`); single `Fail` home adds `ErrorCodes.DataKey` (`:11-16`) |
| ProcessLaunch CheckExecutableExists hoist | YES | `ProcessLaunch.cs:19-39`: single private `CheckExecutableExists`, called from `CheckStart` (`:15`) |
| ProcessLaunch ResolveWorkingDirectory hoist | YES | `ProcessLaunch.cs:43-59`, called from `CheckStart` (`:16`) |
| ProcessLaunch BaseName hoist | YES | `ProcessLaunch.cs:41` single `BaseName` (Trim+Trim('"')+GetFileNameWithoutExtension); used by devenv guard (`:25`) and `CheckExpectedName` (`:72-73`) |
| ProcessLaunch extras hoist | YES | `ProcessLaunch.cs:85` `var extras = extra ?? new Dictionary...` before validation loop (`:86-96`); merged case-insensitively (`:97-104`) |
| ProcessOwner TryGetFresh | YES | `ProcessOwner.cs:26-33` lock+TTL check; used by `SnapshotOwners` (`:73`) and `OwnerOf` (`:86`) |
| ProcessOwner IsEntryFailure | YES | `ProcessOwner.cs:23-24` = `IsHostingFailure + Format/InvalidCast/Overflow`; per-entry catch in `QueryAllOwners` (`:118`) so one bad row never poisons snapshot |
| ProcessOwner ForEachProcess | YES | `ProcessOwner.cs:40-51` single WMI enumeration with deterministic disposal; `SELECT *` NB comment (GetOwner needs full object); used by `OwnerOf` (`:91`) and `QueryAllOwners` (`:111`) |
| VsDiscovery NewestFirst | YES | `VsDiscovery.cs:45-48` `OrderByDescending(VersionOf).ThenByDescending(TryParsePid).ThenBy(ordinal)`; shared by `SelectCandidate` (`:19`) and `OrderForListing` (`:43`) |
| RunToCursor ThrowUnbound | YES | `RunToCursor.cs:104-106` expression-bodied `throw new DebugDomainException(UnboundLocation, $"{file}:{line} did not bind: {warning}", ...)`; called on reuse (`:33`) and create (`:43`) paths |
| RunToCursor Norm | YES | `RunToCursor.cs:119` `Replace('/','\\').TrimStart('\\')`; `ExactLocation` (`:111-116`) OrdinalIgnoreCase compare |
| VariableWrite ReadOnly(inner=null) | YES | `VariableWrite.cs:22-24` factory with `Exception? inner = null` default, passes `inner` to `DebugDomainException` |
| throw types/messages unchanged | YES | Boundary `ArgumentValidator` throws `McpException` via `Fail` only; domain helpers (`ProcessLaunch`, `ProcessOwner`, `RunToCursor`, `VariableWrite`) throw `DebugDomainException` only (plus `Win32Exception` read for message in `StartSuspended`, `COMException` inspected in `IsLostGoRace`). All messages keep `remediation` second arg; no bare host errors |
| build `dotnet build VsDebugMcp.slnx` | YES | `Build succeeded. 0 Warning(s) 0 Error(s)` (retry after stale `testhost` lock MSB3027 cleared; lock was a parallel agent's test run, not source) |
| test `dotnet test VsDebugMcp.slnx --no-build` | PARTIAL | Unit: `Passed 328/328` in slnx run; `VsDebugMcp.E2E` 9 skipped (needs live VS) then `Test host process crashed` -> exit code 1. Standalone rerun `tests/VsDebugMcp.Tests --no-build`: `Passed 330/330`. E2E crash is infra (no live VS in this env), not a dedup regression |

Notes: first `dotnet build` failed only on file lock (`testhost (27260)` from a concurrent session); PID was already gone on retry and rebuild was clean. Slnx unit count 328 vs standalone 330 is parallel-run interference, not code variance.
