# ADR-0005: Read-only evaluator; assignment absent, not discouraged

Status: accepted

## Context

Expression evaluation could mutate debuggee state by accident if assignment
or statement execution were merely discouraged.

## Decision

`evaluate` is read-only by construction: value assignment and statement
execution are absent tools (deferred as separate consent-gated tools, not
in v1). The tool boundary already rejects empty expressions and anything
containing `;`.

## Consequences

Hypothesis-testing without state mutation. Method calls with side effects
through the evaluator remain the known edge (documented: prefer property
reads like `$exception.Message`).
