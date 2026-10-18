Type: grilling
Status: resolved
Labels: wayfinder:grilling
Blocked by: 01, 02, 03

## Question

What exactly is in the v1 minimal core that meets the simplicity bar (generic agent debugs without VS docs)? Decide the final tool list, names, required vs optional params, defaults, and what is explicitly deferred (conditional breakpoints, watch, threads/tasks views, ENC). A throwaway stub may be used to react to, but this ticket resolves a decision, not code.

## Answer

Decided via grilling (two rounds, all confirmed). v1 slate (10 tools, snake_case, `sessionId`-scoped, every result carries `mode` + last-hit): `attach`, `set_breakpoint`, `remove_breakpoint`, `list_breakpoints`, `pause`, `resume`, `step` (single tool, `direction: over | into | out`), `read_stack`, `read_variables` (frame id + expandable path, locals/args unified), `evaluate` (read-only). Defaults: `timeoutMs = 5000`, `maxFrames = 20`, `maxVariables = 50`; values truncated with `expandable` flag, drill down by path. Breakpoints are set/remove/list only (no enable/disable, conditions, hit-counts). Deferred past v1: value-assignment, statement-execution, watches, threads/tasks views, conditional breakpoints, ENC. Attach/instance/timeout rules belong to [Session and safety model](05-session-safety-model.md).
