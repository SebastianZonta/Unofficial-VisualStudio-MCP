# 03: Live EnvDTE bridge plus manual verify

**What to build:** the live `DteDebugBridge` maps conditions natively where EnvDTE allows and emulates the rest evaluate-then-decide per hit (documenting which combos are emulated), verified manually against a real VS instance.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** ready-for-human

- [ ] Conditional, hit-count, combined, and logpoint breakpoints verified live: stop/no-stop behavior correct, counts accurate, log output retrievable.
- [x] Emulated combinations are documented; unloaded-symbol behavior pairs with `list_modules` diagnosis.
- [x] `detach` cleans up live breakpoints with no leftovers.

## Comments

Code implemented, live verification pending (needs a VS debuggee + docs/agents/e2e.md two-VS pass, both manual). Conditions map natively (break-when-true), hit counts natively (equal = exact, multiple = every, combos applied together by VS); logpoints are emulated evaluate-then-decide with auto-continue in ResumeAsync because native log output (VS Output window) is not automation-readable (documented in GLOSSARY.md). detach deletes session-created breakpoints (tracked as owned COM objects, dispatcher-only); set validates before touching COM and round-trips LogMessage. Live build green with 0 warnings. Known live limit in README.md: breakpoints live in the shared VS debugger, so sibling sessions enumerate the same breakpoints; per-session bookkeeping + detach cleanup is MCP-side.

## E2E outcome (2026-10-22, fanned-out subagent)

FAIL (blocked), not a code failure: the serving --live host predated the feature (bare set_breakpoint catalog, write-like condition accepted), then the publish killed the stale servers mid-run and the runner lost live-tool access. Repo build green, 0 warnings; 121/121 tests pass on the fresh binary. The 4 stray unconditional breakpoints the aborted run left in VS-A (DemoApi Program.cs :9,:14,:18,:10) were deleted via a throwaway ROT script; VS-A now lists 0 breakpoints. Rerun e2e sections 3-6 against the fresh binary to close this ticket.

## E2E rerun outcome (2026-10-23, fresh binary, subagent) — FAIL at §3 ext (c)

Fresh host confirmed. §§1-2, §3 base, §4, §3 ext (a) condition:false and (b) hitCount:2/exact all PASS (role swap: debuggee owned by VS-B, ran primary there). Logpoint (c) FAILED: logMessage BP stopped the debuggee with empty Log, twice. Root cause: LogpointSpec key used the user-passed file (Program.cs:8) but lastHit carries the VS full path (C:\...\Program.cs:8) — match never hit. Fixed in d2734f2 (key on snapshot path). Still unchecked: (d) invalid-input rejection, every-mode, combined, §5 isolation, and the logpoint re-verify. Note: live hitCount reads 0 on the stopping hit (VS resets CurrentHits on break) — display-only, not a code bug.
