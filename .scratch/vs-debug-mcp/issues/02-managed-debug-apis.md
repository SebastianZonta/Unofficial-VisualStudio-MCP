Type: research
Status: resolved
Labels: wayfinder:research

## Question

What managed C# debugger API surface is actually available to automation in Visual Studio 2026 for the minimal core: set/clear/list breakpoints (bind states), break/continue, step in/out/over, threads/call stacks, scopes/locals/args, and expression evaluate? For each, name the concrete API (Debugger2/EnvDTE, Concord, or extension SDK) and note VS 2026 deltas, what is unreliable or missing, and what a same-machine bridge must wrap.

## Answer

Resolved via background research. Automate via EnvDTE (`Debugger2/3/5`, `Breakpoint2`, `Expression`, `StackFrame`, `Thread`) — the only automation-facing API covering the full minimal core; do not build on Concord Dkm* or legacy SDM IDebug* (engine-implementor surfaces). VS 2026 changes nothing (no Debugger6; verify ROT/ProgID, bind fidelity, C# EE skew at build). Wrap: eventual bind states, blocking wait-calls with timeouts, evaluate side effects/func-eval deadlocks, stale COM handles (snapshot DTOs), break-mode gating, single-process session, one dispatcher thread. Full detail in `.scratch/vs-debug-mcp/research/02-managed-debug-apis.md`.
