# ADR-0002: Single `IDebugBridge` seam with snapshotted DTOs

Status: accepted

## Context

Thirteen tools need debugger automation that is testable without a running
VS, while live COM handles must never leak into tool code.

## Decision

All automation crosses one interface, `IDebugBridge` (`src/VsDebugMcp/IDebugBridge.cs`).
Two implementations sit behind it: `DteDebugBridge` (live EnvDTE, single STA
dispatcher thread) and `FakeDebugBridge` (in-memory, for the 63 unit tests).
The bridge snapshots live COM objects to plain DTOs on every call; frame and
thread ids are session-scoped tokens re-fetched after any execution-control
call. Tools stay thin and delegate through it.

## Consequences

Full tool surface testable via fakes; COM confined to one place. Cost: the
fake must mirror live semantics (modes, staleness, bind warnings) or tests
prove little.
