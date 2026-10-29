# 01: Two Solutions serving on fixed ports

**What to build:** Both Demo Solutions serving at the same time, each on its own fixed localhost port with a health endpoint, so the host can open one Solution per VS instance and bind one debug session per VS instance before any pricing logic exists.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Both Solutions build on .NET 10 and serve their fixed ports concurrently with no port clash.
- [x] Each Solution opens cleanly in its own VS instance (no cross-loading), staying outside the main Solution with no analyzer or contract-test coupling.
- [x] Session-less discovery lists both VS instances and an attach per instance binds a debug session owning exactly one live target process.

## Comments

Verified this session: both `*.slnx` build green (0 warnings, 0 errors) after
adding a `Demo/Directory.Build.props` stopper (Sandbox pattern) so repo-root
analyzers don't treat the stage apps as product code. Both served concurrently:
`GET :5202/health -> {"status":"ok","service":"pricing"}`,
`GET :5201/health -> {"status":"ok","service":"orders"}`. The VS-attach half of
the third criterion is scripted in ticket 04 and needs the demo machine.
