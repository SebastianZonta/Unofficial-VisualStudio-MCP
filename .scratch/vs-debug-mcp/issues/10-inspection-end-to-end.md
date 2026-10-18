# 10: Inspection end to end

**What to build:** the look-at-state loop an agent can demo alone: capped call stack, unified variables with drill-down, and read-only expression evaluation — all gated on break mode, all truncated.

**Blocked by:** 07 (Host skeleton plus attach round-trip).

**Status:** resolved

- [x] Stack reads capped at a small default frame count; variables read by frame id plus optional expandable path (locals, arguments, members unified)
- [x] Values truncated with an expandable flag; drill-down by path, never full dumps
- [x] Evaluate is read-only; assignment and statement execution are absent, not merely discouraged
- [x] Any inspect call outside break mode returns the named break-mode error, never automation internals
- [x] Scripted tool-call tests cover stack, variables, drill-down, evaluate, and the break-mode violation path

## Comments

Implemented 2026-10-18. `InspectionTools` (`read_stack` maxFrames 20, `read_variables` frameId + optional path maxVariables 50, `evaluate` expression + timeoutMs 5000) + DTOs, all results carrying Mode/LastHit. Fake serves the stub-prototype's canned session (Order.Total/Program.Main frames, truncated expandable `items`, `41.80` eval) with caps honored via Take, `frame-not-found`/`path-not-found` named errors, and break-mode gating on all three. Read-only by construction (no assign/execute tools exist) plus `';'` rejection for statement-looking input. Judgment call recorded: evaluate timeout surfaces as a named `timeout` error rather than 09-style status-data — snapshot reads should not routinely time out; timeoutMs is still plumbed explicitly. Tests: 25/25 pass (3 attach + 6 breakpoints + 8 execution + 8 inspection). Server smoke-tested. No git repo, nothing to commit.
