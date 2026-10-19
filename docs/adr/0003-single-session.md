# ADR-0003: Single active session with stale-on-reattach

Status: accepted

## Context

Multiple VS instances and repeated attaches could make "which session is
live" ambiguous.

## Decision

One active session at a time: a new `attach` replaces the old binding and the
old session id fails with `stale-session`. Multi-instance work is
list → attach → work → re-attach. No session-per-instance map.

## Consequences

Binding is never ambiguous; agents re-attach explicitly. Stale ids fail
loudly instead of silently operating on the wrong instance.
