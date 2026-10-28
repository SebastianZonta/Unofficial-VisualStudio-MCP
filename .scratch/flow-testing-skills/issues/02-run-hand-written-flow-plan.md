# 02: Run a hand-written flow plan

**What to build:** A developer (or an agent) says "run the inspect flow plan".
`vs-debug-run-flow` executes a flow plan in the `Format: 1` template against the
developer's open VS instance and writes a verdict file. The plan is written by hand
here; the planner comes in ticket 03.

This ticket fixes the flow plan format (header, references, use cases as ordered
trigger and checkpoint rows, `UC<n>-T<k>` / `UC<n>-<m>` ids, anchors) and the verdict
format. Only launch triggers and a single target process are in scope.
Spec: [Flow plan format](../spec.md#flow-plan-format),
[Run](../spec.md#run-vs-debug-run-flow-adr-0013), ADR-0013.

**Blocked by:** 01 (Setup skill writes and proves the setup doc)

**Status:** ready-for-agent

- [ ] The flow plan format is written down in the run skill (it carries a `Format: 1` line); an unknown format is BLOCKED "re-plan".
- [ ] With no setup doc, the run stops with one line pointing to `/vs-debug-setup`.
- [ ] Preflight: build, resolve every anchor (missing or ambiguous is BLOCKED "re-plan", never a guessed line), find the VS instance by solution, attach with `instanceOverride`; an instance not in `design` is BLOCKED.
- [ ] Checks work end to end on the Sandbox `inspect` and `loop` scenarios: reached, not reached, order, values (value, not decoration), and hit count (a logpoint when uncorrelated).
- [ ] Statuses follow the spec: a failed check is FAIL and the use case continues; never reached is FAIL; a foreign-breakpoint stop is OBS and resumes; retry once on `vs-busy` or a timeout, never retry a check; a target that never starts is BLOCKED.
- [ ] Use cases run one at a time in plan order, or only the named ones.
- [ ] On every exit path: the session is detached (its breakpoints deleted), foreign breakpoints are untouched, and processes the run launched are stopped.
- [ ] The verdict file lands in the gitignored verdict folder in the spec's format. The run reports the verdict, counts, every FAIL/BLOCKED with evidence and the path, offers next steps, and never edits the plan or the code.
- [ ] Multi-threaded logpoints (issue 10): a logpoint stop is OBS and resumes; an `(unavailable)` entry counts as a hit with no value.
- [ ] Skills pass cases: a hand-written `inspect` plan gives PASS; an edited snippet gives BLOCKED "re-plan"; a foreign breakpoint on the route gives OBS; VS is left as found.
