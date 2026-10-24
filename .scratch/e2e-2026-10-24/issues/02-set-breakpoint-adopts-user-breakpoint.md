# 02: set_breakpoint on a hand-set location adopts the user's breakpoint

**Suspected:** EnvDTE `Breakpoints.Add` at a location that already has a breakpoint returns that existing breakpoint (confirmed live for two breakpoints of one session: removing the second deleted the first). `set_breakpoint` owns whatever `Add` returns, so on a line where the user set a breakpoint by hand the debug session would mint a `bp<N>` for the user's breakpoint, and `remove_breakpoint` or `detach` would delete it. That breaks the ownership rule (GLOSSARY "Breakpoint id"; E2E Z1/H5: detach removes only what the session created). `run_to_cursor` already avoids it by reusing a breakpoint found at the destination.

**Blocked by:** None.

**Status:** needs-triage

- [ ] Confirm live: user sets a breakpoint by hand at TICK; `set_breakpoint` at TICK; `list_breakpoints` (one entry or two?); `remove_breakpoint` the new id; does the user's breakpoint survive?
- [ ] If confirmed: decide the contract (refuse with a named error, or return a list-only/foreign handle) and fix `SetBreakpointAsync` in both bridges.
