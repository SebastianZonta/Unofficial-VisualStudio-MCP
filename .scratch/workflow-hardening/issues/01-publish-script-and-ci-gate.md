# 01 — Publish script + CI bridge gate

Status: ready-for-agent

Bridge diff (`DteDebugBridge.cs`, `FakeDebugBridge.cs`, `BreakpointOptions.cs`, `ArgumentValidator.cs`, `VariableWrite.cs`, `ComErrorMapper.cs`) without an `AuditE2ETests.cs` change fails CI with "bridge diff needs E2E case". Add `scripts/publish-fresh.ps1` (clean empty dir, publish Release win-x64 self-contained, assert `System.Management.dll` > 100KB, print path + write time, refuse locked-exe dir). E2E preflight pastes the size line into the run report `MCP binary:` field.

## Comments
