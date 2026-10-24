# ADR-0012: `set_variable` is a consent-gated, strictly typed single write

Status: accepted (refines ADR-0010)

## Context

The set-variable spec asks for one way to mutate target state (fault
injection) while `evaluate` stays read-only. ADR-0010 states one read-only
rule for "every expression the agent sends"; a write tool conflicts with that
wording, so its boundary has to be explicit.

EnvDTE's only write primitive is the `Expression.Value` setter: the evaluator
parses the assigned text as an expression. It reports nothing for some
refused assignments (a const or get-only member can keep its old value with
no exception), and it exposes no "is writable" flag.

## Decision

- **ADR-0010 covers reads; this is the one write.** The `set_variable` `path`
  still gets the text rule at the tool boundary and is resolved under `, nse`
  before anything is written, so a path whose read alone has side effects
  never reaches the write. The written text is never the agent's: it is the
  literal produced by strict coercion below.
- **Own consent, every call.** `consent: true` is required per call, checked
  before mode or any debugger state, and never derived from `attach_process`
  consent (ADR-0004). Missing consent is `consent-required`.
- **Strict coercion, narrow types.** The value is parsed culture-invariant
  against the type the evaluator reports: integral types (range-checked, no
  separators), `float`/`double` (finite), `decimal` (no exponent), `bool`,
  `char` and `string` literals, and `null` for `string` and `T?`. Anything
  else, including every other reference or struct type, is `type-mismatch`
  carrying expected vs provided. Wider types need a typed literal language;
  out of scope until asked for.
- **Read-only by observation.** A refused setter, an optimized-away local, or
  a re-read that still shows the old value (when the agent asked for a
  different value) is `read-only-target`. Numeric values compare by value
  (`41.8` equals `41.80`) so rewriting the shown value is not misreported.

## Consequences

- One variable per call, no batch and no undo: rewriting the old value is the
  rollback.
- A non-numeric value written in a different spelling than VS displays can be
  misreported as `read-only-target`; TROUBLESHOOTING documents it.
- Live behaviour is covered by the E2E W cases, not unit tests: the fake models
  the contract (consent, mode, path, read-only, type) only.
