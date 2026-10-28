# Documentation index

Root files: [README](../README.md) (what the server is, tool list) · [AGENTS.md](../AGENTS.md) (agent entry point).

## Using the server

- [INSTALL.md](../INSTALL.md): Install or build the server and register it in an MCP client; read before first use or when moving it to another PC.
- [TROUBLESHOOTING.md](TROUBLESHOOTING.md): Failure playbook by symptom (elevation, busy VS, unbound breakpoints, evaluator refusals); read when a call fails.

## Reference

- [TOOLS.md](TOOLS.md): Every tool's parameters and defaults, the result envelope, and the error-code catalog; read before calling or changing a tool.
- [GLOSSARY.md](../GLOSSARY.md): Domain vocabulary (VS instance vs debug session vs target process, mine/consent, mode/lastHit); read before naming anything.
- [CODING_STANDARDS.md](../CODING_STANDARDS.md): Judgement rules for review (bridge parity, shared rules, spec deviations); read before reviewing or finishing a change.

## Decisions

- [adr/README.md](adr/README.md): Index of the fourteen ADRs with status and one-line decision; read before changing an area, and flag any conflict.

## Agent workflow

- [agents/domain.md](agents/domain.md): How skills consume GLOSSARY and ADRs; read when exploring unfamiliar code.
- [agents/build.md](agents/build.md): Build, test, VS resolution, publish; read before first build or when the binary looks stale.
- [agents/architecture.md](agents/architecture.md): `IDebugBridge` seam, live vs fake, STA dispatcher; read before changing bridge or tool code.
- [agents/live-verify.md](agents/live-verify.md): `--live-check*` harnesses and per-tool live verify lines; read before verifying against real VS.
- [agents/e2e.md](agents/e2e.md): Index for the real-VS pass against `Sandbox/` (roles, slice map, report format); it points at the runner and phase files — read the index plus only your slice. Read after a feature or when a case fails.
- [agents/issue-tracker.md](agents/issue-tracker.md): The `.scratch/` file conventions (spec, issues, `Status:` values, wayfinding maps); read before creating or claiming work.

## Work tracking

- [../.scratch/README.md](../.scratch/README.md): Table of every feature, spec and issue with status; read to find what is open or how a feature was specified.
