# 08: Multi-solution: cross-solution flows and parallel runs

**What to build:** In a microservices repo, setup records targets across several solutions, a flow plan's checkpoints may name targets in different solutions, and the flow runner opens one debug session per VS instance the flow touches, following the request across services. `run-flow` computes each flow's solution set and dispatches flow runners in parallel only when their solution sets are disjoint, queueing the rest. See [spec](../spec.md).

**Blocked by:** 04 (run-flow: one flow, one solution, launch)

**Status:** done

- [x] Setup records several solutions, each with its targets (its VS instance override is resolved at run time, per the spec)
- [x] Planner writes a plan whose checkpoints span two targets in two solutions
- [x] Flow runner holds one debug session per VS instance and reaches a verdict over two VS instances (second solution wrapping a Sandbox copy)
- [x] Two flows with disjoint solution sets run concurrently; two sharing a solution queue
- [x] Every debug session the flow runner opened is detached on every exit

## Comments

Dogfood (2026-10-30, Sandbox copy plus `second/Second.slnx` wrapping a copy built as `Sandbox2`, each open in its own VS instance). The VS instance override is resolved at run time through `list_instances`, never recorded (spec decision), so "each with its VS instance override" means each solution row binds by its own entry.

- Setup rerun: doc gained the `Second` solution row, the `sandbox2` target and a build of both solutions.
- `plan-flow` "greet-pair in area pair": two `launch` lines plus one `command`, checkpoint 1 on `sandbox`, checkpoint 2 on `sandbox2` in `second/Scenarios.cs`.
- `greet-pair` run: PASS, two sessions, each with its breakpoints removed and detached.
- `run-flow calc` with step (Sandbox), stack (Sandbox) and step2 (Second): step and step2 dispatched together, stack queued until step returned; all PASS.
- A plan whose `attach` target was not running: BLOCKED at bind after both sessions were attached; both detached, `list_sessions` empty.

Not covered: a request crossing services over HTTP (Sandbox has none; the two targets share one `command` trigger instead). Parallel runners each run the whole build command; no conflict was seen with an up-to-date build, but concurrent builds of a changed tree may collide (a failure shows as BLOCKED "build fails", rerun fixes).
