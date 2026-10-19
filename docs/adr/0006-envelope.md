# ADR-0006: Uniform result envelope (mode + last-hit + truncation + coded errors)

Status: accepted

## Context

Agents previously needed separate status calls, got flooded by deep stacks
and large collections, and received raw COM dumps on failure.

## Decision

Every session-scoped result carries debugger `mode` plus `lastHit`; every
collection/value carries truncation plus an expandable drill-down path
(500-char cap); every failure carries a machine-readable `code` plus human
`remediation` (`code: detail; remediation: …`). Validated by the throwaway
stub-prototype walkthrough (verdict: pass, no changes) — the prototype file
itself was never merged.

## Consequences

No status polling, bounded context use, actionable errors. Envelope fields
are load-bearing: tests assert their presence, not automation internals.
