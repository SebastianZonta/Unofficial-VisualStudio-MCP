# Tools-layer dedup validation (report only, no edits)

Date: 2026-10-25. Scope: `src/VsDebugMcp/ToolPipeline.cs` + Session/Breakpoint/Execution/Inspection tools.

| Claim | Verified | Evidence |
|---|---|---|
| `ToolPipeline.cs` exists with session entry shape | YES | `src/VsDebugMcp/ToolPipeline.cs:6-38` — `RunSessionAsync` (2 overloads), `RunSessionTimeoutAsync`, `RunSessionFileLineAsync`, `RunSessionFrameAsync` |
| All 17 session-bound methods route through `ToolPipeline` | YES | SessionTools 3 (`SessionTools.cs:39,60,77`); BreakpointTools 4 (`BreakpointTools.cs:22,32,41,50`); ExecutionTools 6 (`ExecutionTools.cs:16,25,36,48,61,71`); InspectionTools 4 (`InspectionTools.cs:17,30,46,60`); 3+4+6+4=17. No direct `ValidateSessionId`/`MapAsync` left in session-bound paths (grep: only `ToolPipeline.cs:10,16` + 4 session-less `MapAsync` in SessionTools `:17,24,31,48`) |
| Validation order preserved (sessionId first, extra guards, then `MapAsync`) | YES | `ToolPipeline.cs:10-11,16-18` — `ValidateSessionId` runs before `validateRest`, `MapAsync` last. File/frame helpers validate file+line (`:24-30`) / frameId (`:32-37`) before `validateRest`. Call-site guard order matches pre-dedup diff (`git diff HEAD -- src/VsDebugMcp/*Tools.cs`): e.g. processId→timeoutMs, direction→timeoutMs, exceptionType→mode, path→maxVariables, expression→timeoutMs, `exceptionType.Trim()` preserved |
| Exception types/messages unchanged | YES | `ToolErrors.cs:9-22` unchanged (no diff vs HEAD). `ArgumentValidator.cs` refactor only extracts `RequireNonEmpty`/`RequireRange`/`IsAbsolutePath`; every message string identical in diff; `ValidateExecutablePath` (`:111-115`) semantically identical (`IsAbsolutePath` = non-empty + fully-qualified) |
| D6 TODOs removed, 1 remaining at `SessionTools.cs:11` | YES | grep `TODO\|D6\|dedup` in `src/`: sole hit `SessionTools.cs:11` (kept ILogger TODO). TODO comments removed from Breakpoint/Execution/Inspection tools (diff shows 3 deletions, 0 additions). No `D6` markers anywhere |
| `dotnet build VsDebugMcp.slnx` 0 src errors/warnings | YES | `src/VsDebugMcp` alone: `0 Warning(s) 0 Error(s)`; full `VsDebugMcp.slnx` after killing stale testhosts: `Build succeeded. 0 Warning(s) 0 Error(s)`. First attempt failed with MSB3027/3021 copy-lock on test dll (stale `testhost.exe` PIDs, infra only, resolved via `taskkill`) |
| `dotnet test VsDebugMcp.slnx --no-build` pass/fail + skips | YES | `VsDebugMcp.Tests.dll: Passed 330, Failed 0, Skipped 0`; `VsDebugMcp.E2E.dll: Passed 0, Failed 0, Skipped 9` (live-VS-gated `AuditE2ETests`) |

Notes: `AttachProcessAsync` diff also adds optional `expectedProcessName` passthrough (`SessionTools.cs:58,64`) — outside dedup scope, validation order unaffected. Validation-message equivalence checked by diff inspection, not by executing each throw path.
