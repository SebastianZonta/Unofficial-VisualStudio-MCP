# Architecture

- One seam: everything crosses `IDebugBridge` (`src/VsDebugMcp/IDebugBridge.cs`). Tools (`SessionTools`, `BreakpointTools`, `ExecutionTools`, `InspectionTools` in `src/VsDebugMcp/`) stay thin and delegate through it.
- Two bridges: `DteDebugBridge` (live EnvDTE) vs `FakeDebugBridge` (tests). Default run uses live; `--fake` selects the fake (canned values, `[fake]` display names). `--live` is a no-op.
- Live COM runs on a single STA dispatcher (`ComDispatcher.cs`, COM filter registration is `OleMessageFilter` there) and is snapshotted to plain DTOs — never let live handles escape; map COM errors via `ComErrorMapper.cs` / `ErrorCodes.cs`.
- Process helpers: `ProcessOwner.cs` (WMI owner resolution, called from the `DteDebugBridge` list/attach paths) vs `ProcessDiscovery.cs` (result filtering only) — don't confuse the two.
- Bridge region map: attach path (incl. `AlreadyBound`) and `EvaluateAsync` (f0/`,nse`) live in `DteDebugBridge.cs`; the expression gate is `BreakpointOptions.cs` ↔ `ArgumentValidator.cs`.
