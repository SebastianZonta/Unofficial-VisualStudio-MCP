# ADR-0001: Out-of-proc COM automation, not a VSIX

Status: accepted

## Context

The server must drive a running VS 2026 debugger without demanding VS
expertise from the agent and without an extension install.
Candidates: hand-rolled DTE scripts, a VSIX, engine-level APIs
(Concord/MIEngine).

## Decision

Reach the EnvDTE debugger family (`Debugger2/3/5`, `Breakpoint2`,
`Expression`, `StackFrame`) out-of-proc through the DTE object of the
discovered instance. No in-proc VSIX; no custom debug-engine authorship.
The VSIX remains only a documented fallback if build-time validation hits
unresolvable targeting, hang, event, or elevation walls.

## Consequences

No install surface and no in-proc failure modes. Trade-off: automation-only
fidelity (e.g. no file/line on stack frames) and dependence on ROT
discovery plus same-integrity-level execution.
