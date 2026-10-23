# AGENTS.md

## Build & test

```powershell
dotnet build VsDebugMcp.slnx              # solution is .slnx, not .sln; .NET 10 SDK (see global.json)
dotnet test VsDebugMcp.slnx --no-build    # same order as CI (.github/workflows/ci.yml)
dotnet test --filter "FullyQualifiedName~<TestClass>"  # single suite
```

- Tests run against `FakeDebugBridge` — no VS needed.
- `src/` has `TreatWarningsAsErrors=true` + analysis; `tests/` deliberately opts out (`TreatWarningsAsErrors=false`, `AnalysisMode=None`). Do not "fix" the asymmetry.
- CI resolves VS via `vswhere` → `$env:VSINSTALLDIR`; locally the csproj falls back to VS 2026 Community default path.

## Architecture

- One seam: everything crosses `IDebugBridge` (`src/VsDebugMcp/IDebugBridge.cs`). Tools (`SessionTools`, `BreakpointTools`, `ExecutionTools`, `InspectionTools` in `src/VsDebugMcp/`) stay thin and delegate through it.
- Two bridges: `DteDebugBridge` (live EnvDTE) vs `FakeDebugBridge` (tests). Default run uses fake; `--live` selects the real bridge.
- Live COM runs on a single STA dispatcher (`ComDispatcher.cs`) and is snapshotted to plain DTOs — never let live handles escape; map COM errors via `ComErrorMapper.cs` / `ErrorCodes.cs`.

## VS / EnvDTE gotchas

- EnvDTE refs are build-time only (embedded interop). If build fails resolving `EnvDTE*`, retarget the `HintPath`s in `src/VsDebugMcp/VsDebugMcp.csproj` to your edition/path; runtime binds DTE from the ROT, never a hardcoded ProgID.
- `--live-check <file> <line>` and `--live-check-full <file> <line>` (`LiveCheck.cs`) are builder-only harnesses against a real VS 2026 — not MCP tools. Require VS running as the same Windows user (same elevation).
- Republishing while the MCP host runs fails (exe locked): stop the client first, or publish to a fresh folder and repoint config. `standalone/` output is gitignored.
- `list_instances` PIDs are `devenv` IDE processes — never pass one to `attach_process`. Match the target by name in `list_processes`, attach promptly (OS recycles PIDs), with `consent: true`.
- `evaluate` is read-only (no assignments/statements/`;`); inspection tools need break mode. Frames carry function + module only; `lastHit` (`file:line`) exists only for breakpoint hits. Values truncate at 500 chars — drill via expandable `path`.

## Conventions

- Vocabulary: use `GLOSSARY.md` terms (VS instance vs debug session vs target process, mine/consent, mode/lastHit). Surface ADR conflicts explicitly.
- Issues/specs live in `.scratch/` (one dir per feature, `spec.md` + `issues/NN-<slug>.md` with `Status:` line). See `docs/agents/issue-tracker.md`.
- Triage labels are the five canonical strings (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.
- Commits for spec work use conventional commits (`feat:`, `fix:`, `docs:`, …).
- Tool/parameter reference: `docs/TOOLS.md`; failure playbook: `docs/TROUBLESHOOTING.md`.
- E2E: after a feature, run `docs/agents/e2e.md` (two-VS regression).
- Publish the MCP before the E2E pass so it runs against the fresh binary (src/bin is locked while hosts run: stop the clients first).
- After a feature, update this file if it changed agent-facing facts (build/architecture/gotchas/conventions) — one line, no restating docs.
