# 08: Multi-solution: cross-solution flows and parallel runs

**What to build:** In a microservices repo, setup records targets across several solutions, a flow plan's checkpoints may name targets in different solutions, and the flow runner opens one debug session per VS instance the flow touches, following the request across services. `run-flow` computes each flow's solution set and dispatches flow runners in parallel only when their solution sets are disjoint, queueing the rest. See [spec](../spec.md).

**Blocked by:** 04 (run-flow: one flow, one solution, launch)

**Status:** ready-for-agent

- [ ] Setup records several solutions, each with its VS instance override and targets
- [ ] Planner writes a plan whose checkpoints span two targets in two solutions
- [ ] Flow runner holds one debug session per VS instance and reaches a verdict over two VS instances (second solution wrapping a Sandbox copy)
- [ ] Two flows with disjoint solution sets run concurrently; two sharing a solution queue
- [ ] Every debug session the flow runner opened is detached on every exit
