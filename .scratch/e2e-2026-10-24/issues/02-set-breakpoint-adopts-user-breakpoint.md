# 02: set_breakpoint on a hand-set location adopts the user's breakpoint

**Suspected:** EnvDTE `Breakpoints.Add` at a location that already has a breakpoint returns that existing breakpoint (confirmed live for two breakpoints of one session: removing the second deleted the first). `set_breakpoint` owns whatever `Add` returns, so on a line where the user set a breakpoint by hand the debug session would mint a `bp<N>` for the user's breakpoint, and `remove_breakpoint` or `detach` would delete it. That breaks the ownership rule (GLOSSARY "Breakpoint id"; E2E Z1/H5: detach removes only what the session created). `run_to_cursor` already avoids it by reusing a breakpoint found at the destination.

**Blocked by:** None.

**Status:** resolved

- [x] Confirm live: user sets a breakpoint by hand at TICK; `set_breakpoint` at TICK; `list_breakpoints` (one entry or two?); `remove_breakpoint` the new id; does the user's breakpoint survive?
- [x] If confirmed: decide the contract (refuse with a named error, or return a list-only/foreign handle) and fix `SetBreakpointAsync` in both bridges.

## Comments

Confirmed live 2026-10-24 (VS 17440 on Sandbox, design mode, published 02:18 binary): a breakpoint added at Scenarios.cs:19 through DTE outside any MCP session (equivalent to a hand-set one) -> `set_breakpoint` at :19 returned `bp1`; `list_breakpoints` showed only `bp1` (the foreign entry vanished, no `other1`); `remove_breakpoint(bp1)` -> VS breakpoint count 0. The user's breakpoint was deleted. Same mechanism makes two `set_breakpoint` calls on one line share a VS breakpoint (removing either deletes both). Needs the contract decision before the fix.

Decision (user, 2026-10-24): foreign occupied location -> refuse with new `breakpoint-exists` naming its `other<N>`, nothing created or claimed; own repeat -> return the existing id unchanged, different options -> `invalid-argument`.

Fixed in both bridges (live checks the location before `Add`, plus a safety net when `Add` hands back an existing breakpoint: never owned, never deleted). Fake tests in `OccupiedLocationTests`. Live: `--live-check-occupied '!VisualStudio.DTE.18.0:17440' Scenarios.cs 19` -> repeat returns bp1, second session refused with breakpoint-exists other1, the breakpoint survives the attempt and that session's detach; VS left with 0 breakpoints. E2E cases added: B11 (own repeat) and H1b (user's breakpoint refused).
