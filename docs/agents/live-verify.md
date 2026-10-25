# Live VS verification

Builder-only harnesses against a real VS 2026 (`LiveCheck.cs`) — not MCP tools. Require VS running as the same Windows user (same elevation).

- `--live-check <file> <line>` and `--live-check-full <file> <line>`.
- `list_instances` PIDs are `devenv` IDE processes — `attach_process` refuses them with `invalid-argument` (never a self-attach); match the target by name in `list_processes`, attach promptly (OS recycles PIDs), with `consent: true`.
- `launch_process` starts suspended, `Attach2`s the CoreCLR engine, then resumes (default `Attach()` picks native and stops in the ntdll loader): .NET 5+ targets only. Arg table is `docs/TOOLS.md:19` (`executablePath`, absolute — never `executable`). Verify live with `--live-check-launch <exe> <file> <line>`.
- `list_modules` (EnvDTE90 `Process3.Modules`) lists loaded modules only; live symbol state is `loaded` (`SymbolFile` set) or `missing` — `failed`/`loaded:false` come from the fake only. Printed by `--live-check-launch`.
- `Breakpoints.Add` on an occupied location returns the existing breakpoint: `set_breakpoint` checks first (own repeat → same id, foreign → `breakpoint-exists`); verify with `--live-check-occupied <instance> <file> <line>`.
- `run_to_cursor` is a composed set-breakpoint/continue/remove (`RunToCursor.cs`), so it inherits their COM behavior; verify live with `--live-check-run-to <exe> <file> <line> <toLine> [<runLine>]` (Sandbox: TICK, AFTER_LOOP, HEARTBEAT).
- `evaluate` is read-only (no assignments/`++`/`--`/statements/`;`; live evaluations get `, nse`, ADR-0010) and always runs in `f0`; inspection tools need break mode. Verify ids/`nse`/frames live with `--live-check-audit <exe> <file> <line>`. Frames carry function + module only; `lastHit` (`file:line`) exists only for breakpoint hits. Values truncate at 500 chars — drill via expandable `path`.
- `set_variable` is the only write: per-call `consent` (never attach consent), break mode, strict literal coercion in `VariableWrite.cs`; live it probes the path under `, nse`, assigns `Expression.Value`, and re-reads to catch silent refusals (ADR-0012). Verify live with E2E W1–W10.
- Exception settings use `Debugger3.ExceptionGroups["Common Language Runtime Exceptions"]`; exception breaks are detected by `LastBreakReason` and read `$exception`/`$exception.Message` under `, nse` (ADR-0011). Verify live with `--live-check-exception <Sandbox.exe>` (restores the setting it changes).
