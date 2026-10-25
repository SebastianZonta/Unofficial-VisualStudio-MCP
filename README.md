# VsDebugMcp

Drive a running Visual Studio 2026 debugger from an MCP client: attach, break, inspect, and resume a local debuggee, all through 21 tools over stdio.

> **Unofficial project. Not affiliated with, sponsored, or endorsed by Microsoft.**
> Visual Studio, EnvDTE, and .NET are trademarks of the Microsoft group of companies.
> This server talks to a VS you already have running via its public COM automation
> (`EnvDTE`) interface. It is a community integration, not a Microsoft product, and it
> ships with no warranty. Licensed under Apache 2.0 — see [LICENSE](LICENSE).
See [INSTALL.md](INSTALL.md) for setup.

## Documentation

Everything is indexed in [docs/README.md](docs/README.md): tool reference ([TOOLS.md](docs/TOOLS.md)), failures ([TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)), decisions ([ADRs](docs/adr/README.md)), agent workflow, and work tracking.

## What it does

An agent workflow looks like this:

1. `list_instances` — find the VS instances with your solutions, matched by project name.
2. `attach` — bind one (optional `instanceOverride` picks which; repeat for more instances, one session each; `list_sessions` inventories them, `detach` releases one).
3. `list_processes` — find the program under test by name; no shell PID hunting.
4. `attach_process` — aim the debugger at it (`consent: true`, same-user only). Or skip steps 3–4: `launch_process` starts the program with the debugger attached from birth, so startup breakpoints hit.
5. `set_breakpoint` → `resume` → `read_stack` / `read_variables` / `evaluate` → `resume`.

## Tools (21)

| Tool | One line |
|---|---|
| `attach` | Bind one running VS 2026 instance; returns a session id. Re-attaching the same instance returns its session. |
| `list_sessions` | Session-less list of live debug sessions, one per attached VS instance. |
| `detach` | Release one session; siblings are unaffected. |
| `list_instances` | Session-less list of VS instances: solution, projects, reachability. |
| `list_processes` | Session-less list of local target processes: pid, name, owner, mine flag. |
| `attach_process` | Attach the debugger to a pid. Needs `consent: true` and same-user. |
| `launch_process` | Start a .NET target process (path, args, working dir, env) attached from birth; breakpoints set before launch hit at startup. |
| `set_breakpoint` | File/line breakpoint, optional read-only `condition`, `hitCount` (`exact`/`every`), or `logMessage` logpoint; returns bind state immediately. |
| `list_breakpoints` | Breakpoints with options, hit counts, and bind states; poll until unbound flips to bound. Carries the session logpoint `Log`. |
| `list_modules` | Target-process modules with loaded flag and symbol state; why a breakpoint stays unbound. Break or run mode. |
| `remove_breakpoint` | Remove a breakpoint by id. |
| `pause` | Pause a running debuggee. |
| `resume` | Run until a breakpoint or the timeout. Over-budget waits return a timeout status, never a hang. |
| `run_to_cursor` | Run to a file/line in one call via a temporary breakpoint that is always removed. Break or run mode. |
| `step` | Step `over`, `into`, or `out`. Requires break mode. |
| `list_exception_settings` | CLR exception types and their break mode (`first-chance`/`unhandled-only`). Any mode. |
| `set_exception_setting` | Break first-chance, unhandled-only, or reset one exception type. Break or design mode. Exception breaks carry type, `$exception` path, and message. |
| `read_stack` | Call stack, newest first. Requires break mode. |
| `read_variables` | Frame locals/args; expandable paths drill into members. Requires break mode. |
| `set_variable` | Write one variable (frame + `read_variables` path). Needs `consent: true` on every call; strict typing. Requires break mode. |
| `evaluate` | Read-only expression. No assignments, no statements. Requires break mode. |

Failures are structured: `code: detail; remediation: …` (e.g. `consent-required`,
`user-mismatch`, `process-not-found`, `already-bound`, `no-session`, `vs-not-running`); the full list is the
[error catalog](docs/TOOLS.md#error-catalog). See
[GLOSSARY.md](GLOSSARY.md) for the vocabulary these messages use
(VS instance vs debug session vs target process vs instance override).

## Requirements

- 64-bit Windows, Visual Studio 2026 running as the same user as the MCP host.
- For running from source: .NET 10 SDK. A non-default VS install path needs
  `VSINSTALLDIR` set ([TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md#build-envdte-hintpath-misses)).
- No VS needed for the unit tests (they run against an in-memory fake bridge).

Full setup — one-line install, manual publish, client
registration, smoke test — lives in [INSTALL.md](INSTALL.md).

## Develop

```powershell
dotnet build                                    # Debug
dotnet test                                     # no VS required
./scripts/publish-fresh.ps1 -OutDir standalone-vNext  # the single publish path, never raw dotnet publish
```
Republishing while the host runs fails: see
[Exe locked on republish](docs/TROUBLESHOOTING.md#exe-locked-on-republish).

`--live-check <file> <line>` and `--live-check-full` run builder-only harnesses
against the real VS and exit; the server itself uses the live bridge by default.
`--fake` opts into the fake bridge (canned values, a stderr warning, and
`[fake]` instance display names); `--live` is accepted as a no-op so old configs keep working.

## Architecture

One seam: everything crosses `IDebugBridge`. Two implementations sit behind it —
`DteDebugBridge` (live EnvDTE automation) and `FakeDebugBridge` (in-memory, for
tests). Tools stay thin and delegate through it. Live COM calls run on a single
STA dispatcher thread and are snapshotted to plain DTOs; live handles never escape.

Safety is fail-closed throughout: process attach requires explicit consent plus a
same-user WMI owner check; unknown owners are excluded by default and flagged when
listed unfiltered; `evaluate` is read-only by construction; the only write, `set_variable`, needs its own
`consent: true` on every call and rejects lossy or mistyped values; listing never attaches
to or disturbs anything.

## Known limits

- Stack frames carry function + module only — EnvDTE exposes no file/line on frames.
  Breakpoint hits are the exception: they report `file:line` via `lastHit`.
- Exception breaks carry only type, `$exception` path, and top-level message
  (null when the no-side-effects evaluator refuses it); inner exceptions and
  stack text stay reachable via `read_variables` / `evaluate` on `$exception`.
- Exception settings change the bound VS instance's own table: they are not
  reverted on `detach` and VS may persist them with the solution. Reset with
  `set_exception_setting(..., "default")`.
- If the debuggee exits, the session reports run-ended-back-in-design; there is no
  post-mortem of a dead process.
- PIDs are recycled by the OS: list, then attach promptly.
- All sessions share one dispatcher thread: one wedged VS stalls every session behind it.
- Breakpoints live in the VS debugger, so sibling debug sessions on the same VS
  instance enumerate the same breakpoints; per-session bookkeeping (logpoint
  templates, session log) is MCP-side, and `detach` deletes the breakpoints the
  session created.
