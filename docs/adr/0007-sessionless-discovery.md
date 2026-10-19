# ADR-0007: Session-less discovery; agent-side matching

Status: accepted

## Context

Both attach keys lacked discovery: `attach` bound newest-wins with no view
of open VS instances, and `attach_process` needed a pid with no tool
returning pids — forcing agents out to the shell.

## Decision

`list_instances` and `list_processes` are session-less, read-only tools on
the bridge seam. Matching stays agent-side: find the instance whose project
list contains X and attach by display name; find the program by name and
attach by pid with consent. No server-side project-hint or name-routing
parameters; no behavior change to `attach`/`attach_process`.

## Consequences

Discovery is safe to call freely and never disturbs state. Failure is
isolated per entry (unreachable instances flagged, unreadable processes
skipped) so one bad entry never blinds the list.
