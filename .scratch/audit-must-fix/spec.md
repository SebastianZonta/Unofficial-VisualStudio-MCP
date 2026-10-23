# Audit must-fix: stable breakpoint ids, frame-true inspection, a read-only gate that holds, no ghost COM calls
Status: ready-for-agent

## Problem Statement

A full audit (2026-10-23) found five defects. In each one the server either gives an agent a wrong answer without any error, or changes the target process while promising it would not.

1. **Breakpoint ids are positions, not identities.** The live bridge names a breakpoint after its position in the VS instance's global breakpoint list. After any removal, every later id points at a different breakpoint. An agent that saved its own ids can delete the wrong breakpoint, including one the user set by hand in VS. The fake bridge uses a stable counter, so the test suite cannot see this.
2. **`read_variables` `path` bypasses the read-only gate.** On the live bridge, `path` goes straight to the debugger's expression evaluator and is never validated. `path: "total = 0"` writes to the target process through a tool named "read". This contradicts ADR-0005.
3. **Inspection ignores the frame it was asked about.** A drill-down with a `path` is evaluated in whatever frame the debugger last selected, not in the `frameId` passed. Reading one frame's variables also changes the current frame, so a later `evaluate` silently runs in that frame instead of the top frame.
4. **The read-only rule lets real mutations through.** Increment and decrement (`x++`, `--x`) and compound shift-assignments (`<<=`, `>>=`) pass the check used by `evaluate`, breakpoint conditions and logpoint holes. So a condition like `n++ > 0` changes `n` on every hit. Method calls with side effects also pass. ADR-0005 accepted that last case only because no enforcement was available. The C# evaluator's no-side-effects mode now provides that enforcement.
5. **Timed-out COM calls still run later.** When a call waits on the dispatcher queue past its budget (for example, VS shows a modal dialog), the caller gets `timeout`, but the queued work stays in the queue. When VS frees up, a stale Go or Step runs and the target process resumes or steps with nobody asking.

## Solution

- Breakpoint ids become stable identities per debug session. They are never reused or renumbered. `remove_breakpoint` only removes breakpoints the debug session created.
- `path` gets the same read-only validation as `evaluate` at the tool boundary.
- `read_variables` always runs in the frame named by `frameId`, both for the top level and for a drill-down. `evaluate` always runs in the top frame (`f0`), no matter which frame was inspected before.
- The read-only rule rejects increment, decrement and compound shift-assignment. On the live bridge, every expression evaluated on the agent's behalf (`evaluate`, `read_variables` paths, logpoint holes) runs in the evaluator's no-side-effects mode, so a method call that would mutate state is refused.
- The COM dispatcher skips any queued work whose caller has already given up (timed out or cancelled). A reported `timeout` therefore means nothing was issued later.

The tool contracts stay the same: names, parameters, error codes, and the `mode`/`lastHit` envelope.

## User Stories

1. As an agent, I want a breakpoint id returned by `set_breakpoint` to keep meaning the same breakpoint for the life of my debug session, so that I can remove it later without re-listing.
2. As an agent, I want removing one breakpoint to leave every other id unchanged, so that ids I saved earlier stay valid.
3. As an agent, I want `remove_breakpoint` with an id I already removed to return `breakpoint-not-found`, so that a stale id can never hit a different breakpoint.
4. As an agent, I want ids never to be reused within a debug session, so that a removed id can't come back to life on a new breakpoint.
5. As a developer using VS by hand, I want my own breakpoints in the VS instance never to be removed by an agent's `remove_breakpoint`, so that the agent can't silently destroy my debugging setup.
6. As an agent, I want `list_breakpoints` to keep showing breakpoints the debug session did not create, so that I can still see everything that will stop the target process.
7. As an agent, I want breakpoints the debug session did not create to be clearly marked as not removable through my debug session, so that I don't try to remove them.
8. As an agent, I want `list_breakpoints` ids to match the ids `set_breakpoint` returned, so that I can join the two results without guessing.
9. As an agent, I want the session log entries for logpoints to carry the same stable breakpoint id, so that I can tell which logpoint produced each entry.
10. As an agent, I want `detach` to still delete exactly the breakpoints my debug session created, so that cleanup behaviour is unchanged.
11. As an agent, I want a write-like `path` (e.g. `x = 1`) on `read_variables` rejected with `invalid-argument` before the debugger is touched, so that a read tool can never mutate the target process.
12. As an agent, I want a `path` with `;`, a newline, increment or decrement rejected the same way `evaluate` rejects it, so that one read-only rule governs every expression I send.
13. As an agent, I want `read_variables` with `frameId: f2` and a `path` to resolve that path in frame `f2`, so that drill-down values belong to the frame I asked about.
14. As an agent, I want `read_variables` on any frame to leave `evaluate` running in the top frame, so that the order in which I inspect frames never changes what `evaluate` returns.
15. As an agent, I want an unknown `frameId` combined with a `path` to fail with `frame-not-found`, so that a bad frame is reported, not silently swapped for the current frame.
16. As an agent, I want `evaluate("counter++")` rejected with `invalid-argument`, so that probing a value never increments it.
17. As an agent, I want `--x`, `x--` and `++x` rejected too, so that every increment and decrement form is covered.
18. As an agent, I want `a <<= 1` and `a >>= 1` rejected as assignment, so that compound shift-assignment can't slip past the comparison allowance.
19. As an agent, I want comparisons (`==`, `!=`, `<=`, `>=`) and lambdas (`=>`) still accepted, so that the stricter rule doesn't break legitimate read-only expressions.
20. As an agent, I want a condition like `n++ > 0` rejected at `set_breakpoint` with nothing created, so that a breakpoint condition can't mutate state on every hit.
21. As an agent, I want a logpoint hole like `{i++}` rejected at `set_breakpoint`, so that logging never mutates state.
22. As an agent, I want `evaluate` of a method call with side effects (e.g. `list.Clear()`) refused by the live debugger with `invalid-expression`, so that the read-only promise holds even for calls the text check can't judge.
23. As an agent, I want side-effect-free method calls and property reads (e.g. `list.Count`, `s.Length`) to keep working, so that no-side-effects mode doesn't break normal inspection.
24. As an agent, I want a drill-down `path` and logpoint holes to run in the same no-side-effects mode, so that every evaluation made for me follows one rule.
25. As an agent, I want `evaluate` results to echo the expression I sent, not the evaluator's internal form, so that the no-side-effects mechanism is invisible in results.
26. As an agent, I want a `resume` or `step` that reported `timeout` while VS was blocked never to be issued later, so that the target process doesn't run or step behind my back.
27. As an agent, I want any call that timed out or was cancelled before it reached VS to be dropped, not run late, so that `timeout` means "nothing happened later".
28. As an agent, I want calls that were not cancelled to still run in submission order, so that dropping stale work doesn't reorder live work.
29. As an agent, I want a launch that timed out before starting to never start the target process later, so that no orphaned target process appears after a reported failure.
30. As a builder, I want the fake bridge to model the global breakpoint list (including breakpoints the debug session did not create) and per-frame variables, so that tests at the tool seam express these contracts the same way the live bridge must honour them.
31. As a builder, I want every fix to keep error codes and the result envelope unchanged, so that existing agent retry logic keeps working.

## Implementation Decisions

- **Breakpoint identity.** Each debug session keeps its own mapping from a stable id to the live breakpoint it created. A per-session counter mints ids that only go up, in the existing `bp<N>` form. This mapping replaces the current list of owned breakpoints and becomes the single record used for remove, list and detach cleanup.
- **Breakpoints the debug session did not create** appear in `list_breakpoints` under a separate id form that `remove_breakpoint` rejects (`breakpoint-not-found`, with remediation saying the breakpoint belongs to another debug session or to the user). They are listed by their position each time and are never stored.
- **Logpoint specs** are keyed by the stable id; the existing `file:line` match against `lastHit` is still how a hit is recognised. The session log uses the stable id.
- **Read-only gate.** There is one rule for every expression the agent sends: `evaluate`, `read_variables` `path`, breakpoint conditions and logpoint holes. The tool boundary validates `path` when it is present. The assignment check additionally rejects `++`, `--`, `<<=` and `>>=`. `==`, `!=`, `<=`, `>=` and `=>` are still allowed.
- **No-side-effects evaluation (live bridge only).** The live bridge adds the C# evaluator's no-side-effects format specifier (`nse`) to every agent-supplied expression it evaluates: `evaluate`, drill-down `path` and logpoint holes. A refusal surfaces as `invalid-expression` with remediation pointing at property reads. Breakpoint conditions are evaluated natively by VS and keep only the text rule. The specifier is a C# evaluator feature, so behaviour for other languages has to be confirmed live and documented.
- **Frame selection.** `read_variables` first resolves `frameId` (or fails with `frame-not-found`), then selects that frame for both the top-level and the drill-down branch. Afterwards it re-selects the top frame, so `evaluate` always runs in `f0`. This is also documented in the tool reference.
- **Dispatcher.** Before running a queued item, the COM dispatcher checks whether that item's caller has already completed (cancelled or timed out) and skips it. Work that has already started is unaffected; the launch "abandoned" handshake stays for in-flight launches. This sits under the planned per-session dispatcher lanes (`per-session-dispatcher` spec) and has to survive that change.
- **Docs.** Supersede ADR-0005 with a new ADR: the gate now enforces no side effects for evaluations; method calls are no longer "the known edge". The ADR records that the text rule stays as the first line of defence. Update the tool reference (`path` validation, the `f0` rule for evaluate, how breakpoints the debug session did not create are shown).

## Testing Decisions

- A good test drives a tool (or the bridge seam) and asserts on returned results and error codes, never on internal collections or COM calls.
- **Seam: the MCP tool layer over `IDebugBridge` with the fake bridge.** This is the existing single seam. The fake gains two contract-modelling features, with no new seam:
  - a test hook to add a breakpoint the debug session did not create (a "user" breakpoint) to the fake global list;
  - per-frame canned variables, so frames `f0` and `f1` return different values for the same name.
- Tool-level tests:
  - set three breakpoints, remove the first, remove the third by its original id; the right ones are gone and the middle id still resolves;
  - a removed id returns `breakpoint-not-found`;
  - a user breakpoint is listed but can't be removed;
  - drill-down on `f1` returns `f1` values;
  - `evaluate` after reading `f1` returns `f0` values;
  - `path` assignment, `;`, `++`, `--`, `<<=`, `>>=` → `invalid-argument`;
  - condition and logpoint holes containing `++` → `invalid-argument`, nothing created;
  - comparisons and lambdas still accepted.
- Dispatcher unit test: an item whose caller has already timed out is never run, and later items still run in order. Prior art: the existing dispatcher tests.
- Prior art for validation: the existing tool-validation and conditional-breakpoint suites.
- Live verification: no-side-effects refusal, frame selection and stable ids against a real VS instance, through the `--live-check-full` harness or the `docs/agents/e2e.md` pass. The `audit-should-fix` spec automates this as an E2E suite.

## Out of Scope

- A sanctioned way to write state. That is the `set_variable` spec, which stays the only write path.
- Per-session dispatcher lanes (`per-session-dispatcher` spec).
- Everything in the `audit-should-fix` spec: default bridge selection, the attach race, evaluate budget, COM message filter, the one-target rule for `attach_process`, the E2E suite, dead-code removal.
- Making breakpoint conditions side-effect-free beyond the text rule (VS evaluates them natively).
- File/line on stack frames (EnvDTE doesn't expose them).

## Further Notes

- These come from audit findings 1–5 (2026-10-23 conversation). The fake hiding finding 1 is the reason the fake must model breakpoints the debug session did not create.
- Suggested ticket order: dispatcher skip (smallest, independent) → read-only gate (text rule, then `path`) → frame selection → stable ids → no-side-effects evaluation (needs live confirmation) → ADR and docs.
- Risk: `nse` refuses some property getters that VS considers side-effecting. Record what the live check shows in the new ADR and in `docs/TROUBLESHOOTING.md`.

## Comments

- 2026-10-23: Implemented findings 1–5 with tool-seam tests, ADR-0010 and doc updates. Live verification is still pending: VS ran elevated and the implementing shell did not, so the bridge could not reach the ROT. Run `--live-check-audit <AuditTarget.exe> <Program.cs> 9` from an elevated shell (the target is a throwaway console app whose `Helper(items, total)` is called from top-level code). Record what `nse` does in ADR-0010 and TROUBLESHOOTING. Open live questions: COM identity of enumerated breakpoints (owned ones must not also list as `other<N>`), how a deleted-by-hand breakpoint is detected, and whether `Breakpoints.Add` returns the created breakpoint.
