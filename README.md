# VsDebugMcp

Drive a running Visual Studio 2026 debugger from an MCP client: attach, break, inspect, and resume a local debuggee, all through 13 tools over stdio.

> **Unofficial project. Not affiliated with, sponsored, or endorsed by Microsoft.**
> Visual Studio, EnvDTE, and .NET are trademarks of the Microsoft group of companies.
> This server talks to a VS you already have running via its public COM automation
> (`EnvDTE`) interface. It is a community integration, not a Microsoft product, and it
> ships with no warranty. Licensed under Apache 2.0 — see [LICENSE](LICENSE).
See [INSTALL.md](INSTALL.md) for setup.

## What it does

An agent workflow looks like this:

1. `list_instances` — find the VS instance with your solution, matched by project name.
2. `attach` — bind it (optional `instanceOverride` routes multi-instance setups).
3. `list_processes` — find the program under test by name; no shell PID hunting.
4. `attach_process` — aim the debugger at it (`consent: true`, same-user only).
5. `set_breakpoint` → `resume` → `read_stack` / `read_variables` / `evaluate` → `resume`.

## Tools (13)

| Tool | One line |
|---|---|
| `attach` | Bind the running VS 2026 instance; returns a session id. |
| `list_instances` | Session-less list of VS instances: solution, projects, reachability. |
| `list_processes` | Session-less list of local target processes: pid, name, owner, mine flag. |
| `attach_process` | Attach the debugger to a pid. Needs `consent: true` and same-user. |
| `set_breakpoint` | File/line breakpoint; returns bind state immediately. |
| `list_breakpoints` | Breakpoints with bind states; poll until unbound flips to bound. |
| `remove_breakpoint` | Remove a breakpoint by id. |
| `pause` | Pause a running debuggee. |
| `resume` | Run until a breakpoint or the timeout. Over-budget waits return a timeout status, never a hang. |
| `step` | Step `over`, `into`, or `out`. Requires break mode. |
| `read_stack` | Call stack, newest first. Requires break mode. |
| `read_variables` | Frame locals/args; expandable paths drill into members. Requires break mode. |
| `evaluate` | Read-only expression. No assignments, no statements. Requires break mode. |

Failures are structured: `code: detail; remediation: …` (e.g. `consent-required`,
`user-mismatch`, `process-not-found`, `no-session`, `stale-session`). See
[GLOSSARY.md](GLOSSARY.md) for the vocabulary these messages use
(VS instance vs debug session vs target process vs instance override).

## Requirements

- 64-bit Windows, Visual Studio 2026 running as the same user as the MCP host.
- For running from source: .NET 10 SDK. The five EnvDTE `HintPath`s in
  `src/VsDebugMcp/VsDebugMcp.csproj` point at VS 2026 Community in the default
  install path; retarget them for other editions.
- No VS needed for the unit tests (they run against an in-memory fake bridge).

Full setup — self-contained `standalone` folder or source build, client
registration, smoke test — lives in [INSTALL.md](INSTALL.md).

## Develop

```powershell
dotnet build                                    # Debug
dotnet test                                     # 63 tests, no VS required
dotnet publish src/VsDebugMcp/VsDebugMcp.csproj -c Debug -o src/VsDebugMcp/bin/Fixed
```

Notes on the publish step: the `bin/Fixed` output is what a local opencode config
typically points at. The file is locked while the MCP host runs, so stop the host
(opencode restart) before republishing, then restart opencode to load the new binary.

`--live-check <file> <line>` and `--live-check-full` run builder-only harnesses
against the real VS and exit; without `--live`, the server starts on the fake bridge.

## Architecture

One seam: everything crosses `IDebugBridge`. Two implementations sit behind it —
`DteDebugBridge` (live EnvDTE automation) and `FakeDebugBridge` (in-memory, for
tests). Tools stay thin and delegate through it. Live COM calls run on a single
STA dispatcher thread and are snapshotted to plain DTOs; live handles never escape.

Safety is fail-closed throughout: process attach requires explicit consent plus a
same-user WMI owner check; unknown owners are excluded by default and flagged when
listed unfiltered; `evaluate` is read-only by construction; listing never attaches
to or disturbs anything.

## Known limits

- Stack frames carry function + module only — EnvDTE exposes no file/line on frames.
  Breakpoint hits are the exception: they report `file:line` via `lastHit`.
- No dedicated exception object: on an exception break (`[Exception]` frames, no
  `lastHit`), inspect the `$exception` local via `read_variables` / `evaluate`
  (`$exception.Message` works; method calls like `$exception.ToString()` do not).
- If the debuggee exits, the session reports run-ended-back-in-design; there is no
  post-mortem of a dead process.
- PIDs are recycled by the OS: list, then attach promptly.
