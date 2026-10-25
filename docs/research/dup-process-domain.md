# Duplication — process domain (ProcessDiscovery, ProcessLaunch, ProcessOwner, VsDiscovery, RunToCursor, VariableWrite, ArgumentValidator)

Scope: 7 files only, no edits. Line refs as read.

## 1. `extra ?? new Dictionary` x2 — ProcessLaunch.cs:73,87

```cs
// L73: foreach (var kv in extra ?? new Dictionary<string, string>())
// L87: foreach (var kv in extra ?? new Dictionary<string, string>())
```

Proposed: `var extras = extra ?? new Dictionary<string,string>();` once at top, loop both over `extras`.
Saves ~1 line + 1 allocation per call.

## 2. Executable-path absolute check — ProcessLaunch.cs:15 vs ArgumentValidator.cs:122

```cs
// ProcessLaunch.cs:15: if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath))
// ArgumentValidator.cs:122: same predicate, different throw type (DebugDomainException vs McpException)
```

Proposed: `static bool IsAbsolutePath(string? p) => !string.IsNullOrWhiteSpace(p) && Path.IsPathFullyQualified(p);` in one shared static (or ArgumentValidator), reused by both. Keeps throw sites distinct.
Saves ~1 line per site, removes drift risk (messages already drifted).

## 3. Exe-name normalize — ProcessLaunch.cs:19,60-61

```cs
// L19: Path.GetFileNameWithoutExtension(executablePath).Equals("devenv", ...)
// L60: var actual = Path.GetFileNameWithoutExtension(actualName.Trim());
// L61: var expected = Path.GetFileNameWithoutExtension(expectedName.Trim().Trim('"'));
```

Proposed: `private static string BaseName(string s) => Path.GetFileNameWithoutExtension(s.Trim().Trim('"'));` then `BaseName(x).Equals(...)`.
Saves ~2 lines, unifies quote-trim (L19 doesn't trim quotes).

## 4. Cache TTL guard x2 — ProcessOwner.cs:43-47,59-63

```cs
// SnapshotOwners L45: if (_cache is not null && DateTime.UtcNow - _cacheAt < CacheTtl)
// OwnerOf L61: same + _cache.TryGetValue(processId, out var cached)
```

Proposed: `private static bool TryGetFresh(out Dictionary<int,string?>? cache)` holding the lock + TTL check; both callers use it.
Saves ~4 lines.

## 5. WMI exception lists overlap — ProcessOwner.cs:19-21 vs 104-105

```cs
// L19: IsHostingFailure = ManagementException or UnauthorizedAccessException or COMException or InvalidOperationException or InvalidComObjectException or ...
// L104: catch (... same five + FormatException or InvalidCastException or OverflowException)
```

Proposed: `IsEntryFailure(ex) => IsHostingFailure(ex) || ex is FormatException or InvalidCastException or OverflowException`; inner catch uses it.
Saves ~2 lines, single list to maintain.

## 6. `using var searcher / using var results` + `using (mo)` — ProcessOwner.cs:66-74 vs 93-109

Two WMI enumeration blocks share shape; differ only in query string and per-row action. Proposed: `private static void ForEachProcess(Func<ManagementObject,bool> row)` or `IEnumerable` helper encapsulating searcher/results/dispose; `OwnerOf` and `QueryAllOwners` pass lambdas.
Saves ~8-10 lines; also isolates the SELECT-* Invariant (L91-92 comment) in one place.

## 7. Identical ordering chain — VsDiscovery.cs:19-20 vs 44-46

```cs
// L19: names.OrderByDescending(VersionOf).ThenByDescending(TryParsePid).ThenBy(n => n, ...)
// L44: identical chain on filtered entries
```

Proposed: `private static IOrderedEnumerable<string> NewestFirst(IEnumerable<string> names) => names.OrderByDescending(VersionOf)...`; `SelectCandidate` and `OrderForListing` call it (`FirstOrDefault` / `ToList` at site).
Saves ~4 lines.

## 8. Unbound-location throw x2 — RunToCursor.cs:32-35 vs 44-47

```cs
// reuse path: throw new DebugDomainException(ErrorCodes.UnboundLocation, $"{file}:{line} did not bind: {reuse.Warning}", ...)
// created path: same with set.Breakpoint.Warning
```

Proposed: `private static void ThrowUnbound(string file, int line, string? warning) => throw ...;`
Saves ~6 lines.

## 9. Path normalize x2 in one method — RunToCursor.cs:115-116

```cs
var listed = listedFile.Replace('/', '\\').TrimStart('\\');
var wanted = file.Replace('/', '\\').TrimStart('\\');
```

Proposed: `private static string Norm(string s) => s.Replace('/', '\\').TrimStart('\\');`
Saves ~1 line; ternary/if n/a.

## 10. `ReadOnly` overloads differ only by `inner` — VariableWrite.cs:22-28

```cs
public static DebugDomainException ReadOnly(... string why) => new(..., hint);
public static DebugDomainException ReadOnly(... string why, Exception? inner) => new(..., hint, inner);
```

Proposed: single `ReadOnly(frameId, path, why, Exception? inner = null)`.
Saves ~3 lines. Same applies to factory trio (`PathNotFound`/`AlreadyBound` in ProcessLaunch.cs:50) — consistent factory shape already, no change needed beyond noting.

## 11. `RequireNonEmpty` x7 — ArgumentValidator.cs:18-40,49-53,73-77,126-130

```cs
if (string.IsNullOrWhiteSpace(x)) throw Fail(...);
```

repeated for sessionId, file, breakpointId, frameId, write-path, write-value, exceptionType. Proposed: `private static void RequireNonEmpty(string? v, string msg)` or `ThrowIfNullOrWhiteSpace(v, name, remediation)`; each validator becomes 1 line.
Saves ~14 lines (3→1 each). Same pattern for range checks L96-112 (`ValidateTimeoutMs/MaxFrames/MaxVariables`) — optional `RequireRange(v, min, max, name)` saves ~6 more but adds abstraction; lowest-value of the set.

## 12. Coerce numeric arms x9 — VariableWrite.cs:58-68

Each arm: `T.TryParse(raw, style, inv, out var x) ? <literal> : throw Mismatch(...)`. Inherent to switch-expression coercion; only viable shrink is a generic `static string ParseOr<T>(...)` helper, but it obscures per-type suffix/cast formatting (`L`, `U`, `(short)`). Recommend: leave as-is (already the compact shape).

## 13. `CheckStart` guard wall — ProcessLaunch.cs:13-47 (35 lines, 6 throws)

Long method; split candidates: `CheckExecutableExists(path)` (L15-32: absolute/devenv/exists/runtimeconfig) + `ResolveWorkingDirectory(exe, wd)` (L33-46). No logic change.
Saves 0 lines, splits 35-line method into two ~15-line testable units.

## 14. `IsSameShownValue` numeric-vs-string branch — VariableWrite.cs:95-97

```cs
return decimal.TryParse(...) && decimal.TryParse(...) ? a == b : have == want || (...);
```

Nested ternary + `||` is the compact shape; an if/else would be longer. No change.

Total realistic saving if all applied: ~40-45 lines, mostly items 6, 8, 11.
