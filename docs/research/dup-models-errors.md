# Dup: models + errors (fragment)

Scope: `BreakpointModels.cs`, `DebugModels.cs`, `ExceptionModels.cs`, `ExecutionModels.cs`,
`InspectionModels.cs`, `InstanceModels.cs`, `ModuleModels.cs`, `ProcessModels.cs`,
`BreakpointOptions.cs`, `ErrorCodes.cs`, `ToolErrors.cs`, `DebugDomainException.cs`,
`Program.cs`, `LiveCheck.cs`. No code edited.

## 1. `(Mode, LastHit)` envelope repeated in 12 records

| record | loc |
|---|---|
| `AttachResult(SessionId, Mode, LastHit)` | `DebugModels.cs:4` |
| `SetBreakpointResult(Breakpoint, Mode, LastHit, Created)` | `BreakpointModels.cs:17` |
| `ListBreakpointsResult(Breakpoints, Mode, LastHit, Log)` | `BreakpointModels.cs:19-21` |
| `RemoveBreakpointResult(RemovedId, Mode, LastHit)` | `BreakpointModels.cs:23` |
| `ListExceptionSettingsResult(Settings, Mode, LastHit)` | `ExceptionModels.cs:14` |
| `SetExceptionSettingResult(Setting, Mode, LastHit)` | `ExceptionModels.cs:16` |
| `ExecutionResult(Mode, LastHit, Status, Note, Exception)` | `ExecutionModels.cs:5` |
| `ReadStackResult(Frames, Mode, LastHit)` | `InspectionModels.cs:7` |
| `ReadVariablesResult(FrameId, Path, Variables, Mode, LastHit)` | `InspectionModels.cs:9` |
| `SetVariableResult(FrameId, Path, Value, Type, Mode, LastHit)` | `InspectionModels.cs:12` |
| `EvaluateResult(Expression, Value, Type, Expandable, Mode, LastHit)` | `InspectionModels.cs:14` |
| `ListModulesResult(Modules, Mode, LastHit)` | `ModuleModels.cs:15` |
| `LaunchProcessResult(ProcessId, Mode, LastHit, Status, Note, Exception)` | `ProcessModels.cs:17-18` |

Proposed: one base/interface, records compose it instead of re-declaring the pair:
```csharp
public abstract record DebuggerEnvelope(string Mode, string? LastHit);
public sealed record RemoveBreakpointResult(string RemovedId, string Mode, string? LastHit)
    : DebuggerEnvelope(Mode, LastHit); // or an IEnvelope interface if positional equality matters
```
Lines saved: ~12 (one param-list line per record collapses once call-sites use the base).

## 2. `LaunchProcessResult` duplicates `ExecutionResult` + `ProcessId`

- `ExecutionModels.cs:5`: `ExecutionResult(string Mode, string? LastHit, string Status, string? Note, ExceptionBreakInfo? Exception = null)`
- `ProcessModels.cs:17-18`: `LaunchProcessResult(int ProcessId, string Mode, string? LastHit, string Status, string? Note, ExceptionBreakInfo? Exception = null)`

Proposed: `sealed record LaunchProcessResult(int ProcessId, ExecutionResult Execution);` or flatten via base envelope (#1) + shared `ExecutionOutcome(Status, Note, Exception)` record. Lines saved: ~2-3 plus every future status/note change in one place.

## 3. `FilterAndOrder` duplicated (exceptions vs modules)

- `ExceptionModels.cs:49-56` (`FilterAndOrder`, contains-on-`Name`, `OrderBy Name Ordinal`)
- `ModuleModels.cs:31-43` (`FilterAndOrder`, contains-on-`Name`/`Path`, `OrderBy/ThenBy`)

Snippet (both shape): `var filter = x?.Trim(); return items.Where(...Contains(filter, OrdinalIgnoreCase)...).OrderBy(...).ToList();`

Proposed: one generic in shared helper:
```csharp
internal static IReadOnlyList<T> FilterAndOrder<T>(IEnumerable<T> items, string? f, Func<T,string> name, Func<T,string>? secondary = null, ...);
```
Lines saved: ~8-10 (one implementation, two one-line delegates at call sites).

## 4. Mode-guard duplication

- `ExceptionModels.cs:30-36` `RequireNotRunning`: `if (mode == "run") throw new DebugDomainException(...)`
- `ModuleModels.cs:21-27` `RequireDebugging`: `if (mode is not ("break" or "run")) throw ...`

Proposed: `DebuggerModes.Require(string mode, params string[] allowed)` (single throw site with code/detail/remediation args). Lines saved: ~6.

## 5. `DebugDomainException` factory repetition

- `ExceptionModels.cs:45-47` `NotFound(name) => new(...)`
- `BreakpointOptions.cs:182-185` `Exists(...) => new(...)`
- `BreakpointOptions.cs:210-216` `NotFound(breakpointId) => ternary ? new(...) : new(...)`

Same `new(ErrorCodes.X, $"{...}", "...")` shape ×4. Proposed: keep factories but route through one `Fail(code, detail, remediation)` helper, or one `NotFound(kind, id, remediation)` factory. Lines saved: ~3-4.

## 6. `catch (DebugDomainException) → DOMAIN ...; return 1` ×7 in `LiveCheck.cs`

Locs: `51-55`, `112-116`, `210-214`, `263-267`, `359-363`, `433-437`, `507-511`. Snippet:
```csharp
catch (DebugDomainException ex)
{
    Console.Error.WriteLine($"DOMAIN {ex.Code}: {ex.Message}");
    return 1;
}
```
Proposed: `private static int ReportDomain(DebugDomainException ex) { ...; return 1; }` → `catch (DebugDomainException ex) { return ReportDomain(ex); }`. Lines saved: ~24 (4 lines × 6 extra sites).

## 7. Identical local `Check(bool, string)` ×2

- `LiveCheck.cs:295-299` (in `RunAuditAsync`)
- `LiveCheck.cs:391-395` (in `RunExceptionAsync`)
```csharp
void Check(bool ok, string what)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {what}");
    if (!ok) failures++;
}
```
Proposed: `private static void Check(List-or-ref int failures, bool ok, string what)` or tiny `LiveAssert` class. Lines saved: ~5.

## 8. Arg-parsing repetition in `LiveCheck.cs`

- `9-10`, `62`, `130-131`, `228`, `281`, `484-485`: `args.SkipWhile(a => a != "<flag>").Skip(N)...` + `usage: ... <exe> <file> <line>` + `return 2`.

Proposed: `private static string[] Rest(string[] args, string flag)` + one `Usage(string)` helper. Lines saved: ~10-12.

## 9. `Program.cs:7-26` flag dispatch chain

Six identical `if (args.Contains("...", Ordinal)) return await LiveCheck.RunXAsync(args);`.
Proposed switch expression:
```csharp
return (args.Contains("--live-check-full", Ordinal), ...) switch { ... };
```
or `Dictionary<string, Func<string[], Task<int>>>`. Lines saved: ~6-8.

## 10. if/else → ternary/switch-expression candidates

- `BreakpointOptions.cs:59-60` (`Normalize` mode default): nested ternary already; collapsible to switch expression on `(hitCount, hitCountMode)` — saves ~1 line, readability only.
- `BreakpointOptions.cs:68-71` `CheckExpression`: `return CheckTextRules(...) ?? CheckInvocationRule(...);` already one line (no action).
- `BreakpointOptions.cs:247-268` `ContainsAssignment`: `for/if/continue/return true` → early-return chain is fine; only micro-save is `prev`/`next` locals → switch on char — not worth it (~0 saved).
- `LiveCheck.cs:186`, `207-208`, `261`: already ternaries (`? 0 : 1`, `?.EndsWith(...) == true`) — no action.
- `Program.cs:32-36` fake/live bridge select: `if/else AddSingleton` → `builder.Services.AddSingleton<IDebugBridge>(fake ? typeof(FakeDebugBridge) ...)` does not typecheck for generics; alternative `AddSingleton<IDebugBridge>(sp => ...)` — saves ~2 lines, marginal.

## 11. Const-string holder classes (same shape ×3)

- `BreakpointModels.cs:4-8` `HitCountModes`, `ExceptionModels.cs:6-10` `ExceptionBreakModes`, `ModuleModels.cs:4-9` `SymbolStates`. Each: `public static class X { public const string ... = "..."; }`.
Proposed: leave as-is (public wire contract; enum conversion churns serialization). If touched anyway, one source-generator or `static class Modes` nesting. Lines saved: 0 (deliberately not extracted).

## 12. `FormatLogMessage` vs `Holes` brace scan ×2

- `BreakpointOptions.cs:135-152` (`FormatLogMessage`: `for` + `IndexOf('}')` + `Substring`)
- `BreakpointOptions.cs:154-166` (`Holes`: same scan, `yield return Substring`)
- `BreakpointOptions.cs:227-244` (`BracesBalanced`: third scan with `IndexOfAny(['{','}'])`).

Proposed: single `foreach (var (start, close) in BraceSpans(template))` iterator; all three consume it. Lines saved: ~10-12.
