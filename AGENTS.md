# AGENTS.md

## Build & test

```powershell
dotnet build VsDebugMcp.slnx              # solution is .slnx, not .sln; .NET 10 SDK (see global.json)
dotnet test VsDebugMcp.slnx --no-build    # same order as CI (.github/workflows/ci.yml)
dotnet test --filter "FullyQualifiedName~<TestClass>"  # single suite
```

- Tests run against `FakeDebugBridge` — no VS needed.
- `src/` has `TreatWarningsAsErrors=true` + analysis; `tests/` deliberately opts out (`TreatWarningsAsErrors=false`, `AnalysisMode=None`). Do not "fix" the asymmetry.
- CI resolves VS via `vswhere` → `$env:VSINSTALLDIR`; locally the csproj falls back to the VS 2026 Enterprise, Professional, then Community default path.

## Architecture

- One seam: everything crosses `IDebugBridge` (`src/VsDebugMcp/IDebugBridge.cs`). Tools (`SessionTools`, `BreakpointTools`, `ExecutionTools`, `InspectionTools` in `src/VsDebugMcp/`) stay thin and delegate through it.
- Two bridges: `DteDebugBridge` (live EnvDTE) vs `FakeDebugBridge` (tests). Default run uses live; `--fake` selects the fake (canned values, `[fake]` display names). `--live` is a no-op.
- Live COM runs on a single STA dispatcher (`ComDispatcher.cs`) and is snapshotted to plain DTOs — never let live handles escape; map COM errors via `ComErrorMapper.cs` / `ErrorCodes.cs`.

## VS / EnvDTE gotchas

- EnvDTE refs are build-time only (embedded interop). If build fails resolving `EnvDTE*`, retarget the `HintPath`s in `src/VsDebugMcp/VsDebugMcp.csproj` to your edition/path; runtime binds DTE from the ROT, never a hardcoded ProgID.
- `--live-check <file> <line>` and `--live-check-full <file> <line>` (`LiveCheck.cs`) are builder-only harnesses against a real VS 2026 — not MCP tools. Require VS running as the same Windows user (same elevation).
- Republishing while the MCP host runs fails (exe locked): stop the client first, or publish to a fresh folder and repoint config. `standalone/` output is gitignored.
- `list_instances` PIDs are `devenv` IDE processes — never pass one to `attach_process`. Match the target by name in `list_processes`, attach promptly (OS recycles PIDs), with `consent: true`.
- `launch_process` starts suspended, `Attach2`s the CoreCLR engine, then resumes (default `Attach()` picks native and stops in the ntdll loader): .NET 5+ targets only. Verify live with `--live-check-launch <exe> <file> <line>`.
- `list_modules` (EnvDTE90 `Process3.Modules`) lists loaded modules only; live symbol state is `loaded` (`SymbolFile` set) or `missing` — `failed`/`loaded:false` come from the fake only. Printed by `--live-check-launch`.
- `Breakpoints.Add` on an occupied location returns the existing breakpoint: `set_breakpoint` checks first (own repeat → same id, foreign → `breakpoint-exists`); verify with `--live-check-occupied <instance> <file> <line>`.
- `run_to_cursor` is a composed set-breakpoint/continue/remove (`RunToCursor.cs`), so it inherits their COM behavior; verify live with `--live-check-run-to <exe> <file> <line> <toLine> [<runLine>]` (Sandbox: TICK, AFTER_LOOP, HEARTBEAT).
- `evaluate` is read-only (no assignments/`++`/`--`/statements/`;`; live evaluations get `, nse`, ADR-0010) and always runs in `f0`; inspection tools need break mode. Verify ids/`nse`/frames live with `--live-check-audit <exe> <file> <line>`. Frames carry function + module only; `lastHit` (`file:line`) exists only for breakpoint hits. Values truncate at 500 chars — drill via expandable `path`.
- Exception settings use `Debugger3.ExceptionGroups["Common Language Runtime Exceptions"]`; exception breaks are detected by `LastBreakReason` and read `$exception`/`$exception.Message` under `, nse` (ADR-0011). Verify live with `--live-check-exception <Sandbox.exe>` (restores the setting it changes).

## Conventions

- Vocabulary: use `GLOSSARY.md` terms (VS instance vs debug session vs target process, mine/consent, mode/lastHit). Surface ADR conflicts explicitly.
- Issues/specs live in `.scratch/` (one dir per feature, `spec.md` + `issues/NN-<slug>.md` with `Status:` line); conventions and labels in `docs/agents/issue-tracker.md`, `docs/agents/triage-labels.md`.
- Commits for spec work use conventional commits (`feat:`, `fix:`, `docs:`, …).
- Docs index (tools, troubleshooting, ADRs, e2e, work tracking): `docs/README.md`.
- E2E: after a feature, run the real-VS pass in `docs/agents/e2e.md`: fan out one subagent on Sonnet 5.5 (Agent tool, `model: "sonnet"`) against `Sandbox/`, which starts its own VS instances and never touches yours. Then the main agent runs phase H with the user. Bar: every case PASS.
- `Sandbox/` is the E2E debug target: outside the solution, its own `Directory.Build.props` (no analyzers), breakpoint lines found by `// E2E:<NAME>` markers. When you change a scenario, update the e2e.md marker table too.
- Publish the MCP before the E2E pass so it runs against the fresh binary (src/bin is locked while hosts run: stop the clients first).
- After a feature: a spec deviation gets an ADR (`docs/adr/`), a new failure mode a `docs/TROUBLESHOOTING.md` entry, changed behaviour an e2e case; `DocsContractTests` checks tool/error-code rows and tool counts. Review standards: `CODING_STANDARDS.md`.
- After a feature, update this file if it changed agent-facing facts (build/architecture/gotchas/conventions) — one line, no restating docs.
