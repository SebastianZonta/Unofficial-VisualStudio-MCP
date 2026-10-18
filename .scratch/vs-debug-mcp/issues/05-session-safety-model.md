Type: grilling
Status: resolved
Labels: wayfinder:grilling
Blocked by: 01, 02

## Question

What is the v1 session and safety model: how does the MCP find/attach to a VS 2026 instance, launch vs attach, single active debug session, cancellation/timeouts, attach consent, and guardrails for side-effecting evaluate and long steps? Decide the rules the spec must enforce.

## Answer

Decided via grilling (two rounds, all confirmed). Discovery: enumerate ROT, bind newest running VS 2026, explicit instance override, clear error when absent (never hardcode ProgID). Scope: drive bound VS active solution — F5 launch/resume + local attach-by-ID; remote/multi-process out with naming-the-limitation errors. Single active session: new `attach` replaces old binding (old id goes stale). Consent: arbitrary-PID attach needs `consent: true` + same-user; F5 frictionless. Timeouts: every blocking DTE call wrapped in `timeoutMs` (default 5000) + `CancellationToken`; `timeout` is a first-class status. One dispatcher thread serializes all DTE calls. Break-mode gating via `CurrentMode` (`not-in-break-mode` error, never raw COM). Named environment errors for elevation mismatch and VS-busy with remediation. Evaluate stays read-only per [Minimal tool surface](04-minimal-tool-surface.md).
