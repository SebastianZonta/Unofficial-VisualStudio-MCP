# ADR-0010: Evaluations run with no side effects; the text rule is the first line

Status: accepted (supersedes ADR-0005)

## Context

ADR-0005 made `evaluate` read-only by leaving assignment and statements out,
and accepted method calls with side effects as "the known edge" because
nothing enforced more. The 2026-10-23 audit found that edge was wider than
documented:

- The text rule let `x++`, `--x`, `<<=` and `>>=` through, so a condition
  like `n++ > 0` changed `n` on every hit.
- `read_variables` `path` went straight to the evaluator with no check at all,
  so `path: "total = 0"` wrote to the target process through a "read" tool.
- Method calls with side effects (`list.Clear()`) ran.

The C# expression evaluator has a format specifier, `nse` ("no side
effects"), that refuses any evaluation that would change state instead of
running it.

## Decision

One read-only rule governs every expression the agent sends: `evaluate`,
`read_variables` `path`, breakpoint `condition`, and logpoint `{holes}`.

1. **Text rule (both bridges, tool boundary):** no `;`, no newline, no
   assignment. Assignment now includes `++`, `--`, `<<=`, `>>=` (and `>>>=`).
   `==`, `!=`, `<=`, `>=` and `=>` stay allowed. Each case fails with
   `invalid-argument` before the debugger is touched.
2. **No-side-effects mode (live bridge):** every agent-supplied expression
   that the bridge evaluates (`evaluate`, drill-down `path`, logpoint holes)
   is sent as `<expression>, nse`. A refusal surfaces as `invalid-expression`
   (logpoint holes render `(unavailable)`). Results echo the agent's
   expression, never the `, nse` form.

Breakpoint conditions are evaluated natively by VS on every hit, outside the
bridge, so they keep only the text rule.

## Consequences

- Method calls are no longer the known edge: one that would mutate state is
  refused rather than executed. The text rule stays as the first line of
  defence, because it fails fast with a precise message and also covers
  conditions, which `nse` cannot reach.
- `nse` is a C# evaluator feature. Other languages' evaluators may reject or
  ignore the specifier; confirm live before relying on it there.
- Risk: `nse` may refuse property getters that VS considers side-effecting.
  The remediation points at fields and plain property reads; record live
  findings in `docs/TROUBLESHOOTING.md`.
- The text rule is conservative: a string literal containing `=`, `++` or
  `--` is rejected too. Accepted, since a read-only probe rarely needs one.
- The only sanctioned write path remains a separate, explicit tool (the
  `set_variable` spec), never `evaluate`.
