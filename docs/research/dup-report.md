# Duplication / simplification report — merged

Fragments: `dup-tools-layer.md`, `dup-bridge-layer.md`, `dup-process-domain.md`, `dup-models-errors.md`. No code edited.

## Top wins (do first)

1. Bridge #1: 8× COM catch pair → `WithSessionMap` / route via `RunSnapshotAsync`-style wrapper — ~40 lines (`DteDebugBridge.cs:356-363,424-431,752-759,820-827,961-968,1085-1092,1401-1408,1498-1505`)
2. Tools D1+D2: 17× `ValidateSessionId + MapAsync` → `RunSessionAsync` (+timeout overload) — ~23 lines (`SessionTools.cs:40-42`, `BreakpointTools.cs:24-28`, `ExecutionTools.cs:18-19`, `InspectionTools.cs:19-21`, +14 more sites)
3. Models #6: 7× `catch DOMAIN` in `LiveCheck.cs:51-55,112-116,210-214,263-267,359-363,433-437,507-511` → `ReportDomain` — ~24 lines
4. Bridge #2+#4: session-debugger 2-liner + timeout catch → shared wrapper — ~30 lines (`DteDebugBridge.cs:312+315,391+394,747+750,815+818,1074+1077`, + timeout sites `63-70,366-370,434-441,1411-1415,1720-1724`)
5. Process #6+#11: WMI `ForEachProcess` helper (~8-10, `ProcessOwner.cs:66-74,93-109`) + `RequireNonEmpty` ×7 (~14, `ArgumentValidator.cs:18-40,49-53,73-77,126-130`)
6. Models #8+#12: arg-parse + brace-scan helpers — ~20 lines (`LiveCheck.cs:9-10,62,130-131,228,281,484-485`; `BreakpointOptions.cs:135-152,154-166,227-244`)
7. Fake-vs-live shared guards (bridge #9-13): `ValidateAndNormalize`, `RequireConsent`, `ThrowIfDevenv`, `RequireOwner`, `GuardModes` — ~30 lines total
8. Models #3+#4: generic `FilterAndOrder` (~8-10) + `DebuggerModes.Require` (~6)

Total realistic: ~200 lines + hygiene.

## Ternary / switch-expression candidates

- `Program.cs:7-26` flag dispatch → dict/switch (~6-8 saved)
- `ClampDelay` (`DteDebugBridge.cs:1570-1575`), `ConditionGate` (`FakeDebugBridge.cs:342-350`), step switch (`DteDebugBridge.cs:1078-1083`) → expression bodies (~3-4 saved)
- `RunToCursor.cs:115-116` path norm → `Norm()` (~1); `ProcessLaunch.cs:73,87` `?? new Dict` hoist (~1)
- Already compact, leave: `VariableWrite.cs:58-68` coerce switch, `VariableWrite.cs:95-97` ternary, `BreakpointOptions.cs:68-71`, `LiveCheck.cs:186,207-208,261`

## Hygiene (0-3 lines each)

- Tools D5/D6: `ToolDescriptions` consts, delete 3/4 TODO comments
- Process #2/#3/#7/#9/#10: `IsAbsolutePath`, `BaseName`, `NewestFirst` (`VsDiscovery.cs:19-20,44-46`), `ThrowUnbound` (`RunToCursor.cs:32-35,44-47`), `ReadOnly(inner=null)` (`VariableWrite.cs:22-28`)
- Models #1/#2/#5/#11: envelope base record, `LaunchProcessResult` vs `ExecutionResult`, `Fail()` helper, const holders (leave — wire churn)

## Deliberately skipped

- Poll-loop merge, Attach lock-scan unify, ComDispatcher 1-liners, generic coerce helper, `ContainsAssignment` char-switch, const→enum — behavior/clarity risk > saving. See fragments for rationale.

## Risks

- Bridge #1/#2 routing must preserve per-op `IssueBudgetMs`; tools D1 must preserve validation order; envelope base changes JSON/equality — verify consumers first.
