# ADR-0004: Consent plus same-user gate on process attach

Status: accepted

## Context

Attaching the debugger to an arbitrary local process is a sensitive
operation that must be deliberate and auditable, while the common
solution-launch loop should stay frictionless.

## Decision

`attach_process` requires explicit `consent: true` plus a same-user WMI
owner check; unknown owners fail closed. `list_processes` defaults to
owned-only with a per-entry `mine` flag predicting the gate. Solution
processes attach without extra friction.

## Consequences

Arbitrary attach is always a conscious act; cross-user attach is refused by
construction. Agents must list-then-attach promptly (PIDs recycle).
