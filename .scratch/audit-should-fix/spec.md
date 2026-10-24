# Audit should-fix: live-by-default, attach race, honest budgets and modes, busy-VS retry, one target per session, reproducible E2E, lean
Status: ready-for-agent

## Problem Statement

The 2026-10-23 audit found a second group of problems. None of them silently corrupts results the way the must-fix group does, but each one either misleads agents, breaks a documented rule under parallel calls, or keeps the project from proving that it works:

1. **Fake by default.** Without `--live`, the server starts on the fake bridge and serves canned values (`41.80`, `Order.Total()`) that look real. One missing flag in an MCP config and agents act on made-up data.
2. **Parallel `attach` binds one VS instance twice.** Two `attach` calls at the same time for the same VS instance both see "no session yet" and both create one. This breaks ADR-0008's "one live session per VS instance", and the two sessions' breakpoint cleanups then collide.
3. **`evaluate` budget is cut short.** `timeoutMs` up to 60000 is accepted, but the live bridge wraps the call in a fixed 30-second limit. A 45-second budget fails at 30 seconds while the COM thread stays blocked and later calls queue behind it.
4. **`attach` lies about mode when VS is busy.** If reading the debugger state times out or fails during `attach`, the result says `design`. An agent then believes nothing is running and gets `already-bound` from `launch_process`. Other paths already report `unknown` in this situation.
5. **Every agent must hand-roll `vs-busy` retries.** No COM retry filter is registered, so VS rejects calls the moment it is busy. The repo's own live-check harness needs a ten-attempt retry loop right after launch.
6. **One-target rule applied two ways.** The glossary says a debug session owns at most one live target process. `launch_process` enforces that rule; `attach_process` doesn't, so two attaches in a row bind two target processes and then block every launch.
7. **The E2E pass can't be reproduced.** The two-VS regression depends on a `DemoApi` app that isn't in the repo, is driven by hand, and never exercises `launch_process`. Live-only logic (breakpoint id lookup, frame lookup) has no automated coverage, which is how the must-fix defects went unseen.
8. **Dead code.** Two unused enums and their wire-string helpers sit next to the error codes. Nothing in src, tests or docs uses them.
9. **Duplicated read-only rule.** The tool-boundary expression validator re-implements the shared expression check with different messages, so the two can drift apart.

## Solution

- **Live becomes the default.** The fake becomes opt-in with an explicit `--fake` flag. `--live` is still accepted as a no-op, so existing MCP configs keep working. When the fake is running, the server says so in startup output and every VS instance display name it reports marks it as fake.
- **`attach` is atomic per VS instance.** Parallel calls for the same VS instance return the same `sessionId`.
- **`evaluate` gets the whole `timeoutMs`,** plus a small margin for the COM round trip.
- **`attach` reports `unknown`** when it can't read the debugger state, as the other paths already do.
- **A COM message filter on the dispatcher thread** retries calls a busy VS rejects, for a bounded time, before surfacing `vs-busy`.
- **`attach_process` refuses with `already-bound`** when the debug session already has a target process, the same as `launch_process`.
- **The repo ships a small target app and an opt-in automated E2E suite.** The suite drives the real server over stdio and covers every tool plus each audit finding. The manual two-VS guide is rewritten around the in-repo target.
- **Dead enums are deleted,** and the expression validator delegates to the shared check.

## User Stories

1. As an agent developer, I want the server to use the live VS bridge when started with no flags, so that a minimal MCP config can't serve fake data.
2. As an agent developer, I want my existing config with `--live` to keep working unchanged, so that upgrading needs no config edit.
3. As a builder, I want to start the fake bridge explicitly with `--fake`, so that demos and client wiring tests still work without VS.
4. As an agent, I want a fake-backed server to announce itself in its output and in VS instance display names, so that I can never mistake canned values for a real target process.
5. As an agent, I want two parallel `attach` calls for the same VS instance to return the same `sessionId`, so that parallel tool calls can't create duplicate bindings.
6. As an agent, I want `list_sessions` to show exactly one debug session per VS instance after parallel attaches, so that the inventory matches ADR-0008.
7. As an agent, I want parallel `attach` calls for different VS instances to still create distinct sessions, so that multi-instance debugging is unaffected.
8. As an agent, I want `evaluate` with `timeoutMs: 45000` to wait up to 45 seconds before `timeout`, so that the documented budget is the real one.
9. As an agent, I want an `evaluate` timeout not to leave later calls queued behind a still-blocked COM call any longer than my budget, so that one slow expression doesn't stall the session.
10. As an agent, I want `attach` to report `mode: unknown` when the debugger state can't be read, so that I don't act on a false `design`.
11. As an agent, I want a subsequent call to report the real mode once VS is free, so that `unknown` is temporary, not sticky.
12. As an agent, I want calls a busy VS rejects to be retried for a short bounded time inside the server, so that I don't have to wrap every tool in my own `vs-busy` loop.
13. As an agent, I want `vs-busy` to still surface when VS stays busy past the retry window, so that a real modal dialog is reported, not hidden.
14. As an agent, I want the retry window to count against my call's budget, so that retries never stretch a call past its `timeoutMs`.
15. As an agent, I want a second `attach_process` on a debug session that already has a target process to fail with `already-bound` naming the bound PID, so that one rule governs both ways of binding a target process.
16. As an agent, I want `attach_process` to work again once the bound target process has ended or the debug session was re-attached, so that the rule doesn't strand the debug session.
17. As an agent, I want the consent and same-user checks on `attach_process` to run in their current order, before the one-target check is reported, so that the trust gate is unchanged.
18. As a maintainer, I want a small target app in the repo with known locals, a multi-frame call chain with a distinct value per frame, a side-effect counter, and a value longer than the truncation limit, so that every live behaviour has a deterministic place to test.
19. As a maintainer, I want an automated E2E suite that starts the real server binary in live mode and talks to it over stdio, so that the JSON tool layer, validation and live bridge are tested together.
20. As a maintainer, I want the E2E suite skipped unless explicitly enabled, so that CI and plain `dotnet test` stay VS-free.
21. As a maintainer, I want the suite to use `launch_process` on the in-repo target app, so that no manual PID matching or external app is needed.
22. As a maintainer, I want the suite to cover `attach_process` by starting the target app itself and finding it through `list_processes`, so that the consent and same-user path is exercised live.
23. As a maintainer, I want the two-VS isolation cases to run when a second VS instance is named in configuration and be skipped otherwise, so that one-VS machines can still run the rest.
24. As a maintainer, I want one E2E case per must-fix and should-fix behaviour (stable ids, frame-true inspection, read-only gate including no-side-effects, no ghost resume, parallel attach, evaluate budget, attach mode, one-target rule), so that every audit fix has a live regression guard.
25. As a maintainer, I want the conditional breakpoint, hit-count, logpoint, isolation and cleanup sections of the current guide ported to the in-repo target, so that nothing the manual pass checks today is lost.
26. As a maintainer, I want the suite to leave no breakpoints, sessions or target processes behind, even on failure, so that reruns start clean.
27. As a maintainer, I want the manual two-VS guide rewritten around the in-repo target, so that the human/subagent pass and the automated suite check the same things.
28. As a maintainer, I want the unused mode/status enums deleted, so that nobody builds on a second, unused vocabulary for wire values.
29. As a maintainer, I want the tool-boundary expression validator to delegate to the shared expression check, so that the read-only rule has exactly one implementation.
30. As an agent, I want the validation error codes and wire format unchanged after the dedupe, so that my error handling keeps working.

## Implementation Decisions

- **Bridge selection.** Live is the default. `--fake` selects the fake. `--live` stays accepted and does nothing. The fake writes a warning to stderr at startup and adds a fake marker to the instance display names it reports. Update README, INSTALL and AGENTS.md (one line) to match.
- **Atomic attach.** The live bridge re-checks for an existing session with the same display name under the same lock that inserts the new session. If one now exists, the newly bound DTE is released on the dispatcher and the existing session is returned. The liveness probe and rebinding of a dead session stay as they are.
- **Evaluate budget.** The snapshot helper takes a caller-supplied budget. `evaluate` passes `timeoutMs` plus a fixed margin; other inspection calls keep the current default. The validator range (1000–60000) is unchanged.
- **Attach mode.** When reading state during `attach` hits a timeout or a COM failure, the mode is `unknown` and `lastHit` is null, matching resume/step. A disconnect still marks the session dead as it does today.
- **COM message filter.** The dispatcher thread registers an OLE message filter when it starts and unregisters it on dispose. Calls rejected as busy are retried with a short interval for at most a bounded window (a few seconds), and never past the caller's remaining budget. After the window, the existing `vs-busy` mapping applies. The live-check harness's hand-rolled retry loop is removed once the filter is in place. This is compatible with the planned per-session lanes: every lane thread registers its own filter.
- **One target per debug session.** Both bridges check whether the debug session already has a target process before attaching and raise the existing `already-bound` error. The check runs after consent and same-user, so the trust gate is evaluated first. The glossary already states the rule; no ADR change is needed.
- **E2E target app.** A .NET 10 console app in the tests area, built by the solution but not run by unit tests. It contains a loop with known locals, a three-level call chain where each frame holds a different value of the same name, a counter that a side-effecting method bumps, and a collection whose display text is longer than the 500-character truncation limit. It runs until killed.
- **E2E suite.** An opt-in test project that starts the published server in live mode over stdio, using the client side of the MCP SDK the server already depends on. An environment flag enables it; optional environment variables name VS instances A and B. Each test cleans up in a finally step (remove breakpoints, detach, kill the target process). It never runs in CI. The manual guide in the agents docs is rewritten to use the same target app and to point at the suite.
- **Lean.** Delete the unused mode/status enums and their extensions. The tool-boundary expression validator becomes a thin wrapper over the shared expression check, the same way breakpoint-option validation already works; error code and wire format stay the same.

## Testing Decisions

- A good test drives a tool (or the bridge seam) and asserts on returned results and error codes, never on internals.
- **Seam 1 (existing): the MCP tool layer over `IDebugBridge` with the fake bridge.** It covers the one-target rule (`attach_process` twice → `already-bound`, consent still checked first), the delegating validator (same codes and messages for every existing invalid expression), and fake-marker display names.
- **Seam 2 (new, the highest possible): the real server process over MCP stdio in live mode.** This is the only way to cover live-only behaviour: parallel attach, evaluate budget, attach mode under a busy VS, the message filter, and every must-fix behaviour. It is one seam for the whole live surface rather than a seam per class.
- Dispatcher-level unit test for the message filter: registration happens on the dispatcher thread and doesn't break existing dispatcher behaviour. Prior art: the existing dispatcher tests.
- Prior art: the existing tool-validation, attach-process and multi-session suites for seam 1; the live-check harness and the manual E2E guide for what seam 2 asserts.
- Some cases need a human: busy-VS cases need a modal dialog open in VS. They stay as a manual step in the guide and are marked as manual in the suite.

## Out of Scope

- Everything in the `audit-must-fix` spec: stable breakpoint ids, path validation, frame selection, increment/shift rule, no-side-effects evaluation, skipping stale dispatcher work.
- Per-session dispatcher lanes (`per-session-dispatcher` spec). This spec only has to stay compatible with it.
- Running the E2E suite in CI (needs an interactive VS).
- Removing the fake bridge. It stays for tests and explicit demos.
- Splitting or reshaping the live-check harnesses beyond removing the retry loop.

## Further Notes

- These come from audit findings 6–14 (2026-10-23 conversation). Findings 15–16 (the fake's `listsession.Breakpoints` typo and the duplicated ILogger TODO comments) are trivial and can ride along in any ticket.
- The E2E suite is the verification vehicle for the `audit-must-fix` spec too. If must-fix lands first, its live checks run by hand via the existing guide, and this suite then locks them in.
- Suggested ticket order: lean (dead enums, validator delegation) → one-target rule → attach mode → evaluate budget → atomic attach → live-by-default → E2E target app → E2E suite → message filter (uses the suite to prove it).
- Behaviour change: live-by-default changes what a flagless start does. Call it out in release notes and README.
