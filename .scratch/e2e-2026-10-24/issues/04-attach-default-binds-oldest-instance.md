# 04: `attach()` with no override binds oldest same-version instance, not newest (M1)

**What broke (E2E 2026-10-24-1315):** M1 — `attach()` with no `instanceOverride`
returned the session on A (pid 24876, started first) instead of binding the
newest instance B (pid 3744). `attach(B)` explicitly works; repeat `attach()`
keeps returning S on A.

**Root cause (code read):** `VsDiscovery.SelectCandidate`
(`src/VsDebugMcp/VsDiscovery.cs:17`) orders by `VersionOf` descending, then
display name ascending. Both instances are `!VisualStudio.DTE.18.0:<pid>`,
so the tiebreak is lexicographic name order = lowest pid = the OLDEST
instance. Nothing in the selection prefers recency. Docs (`AGENTS.md`: "the
server binds the newest running VS 2026"; `e2e.md` M1) promise newest.

**Fix (done):** tiebreak same-version instances by pid descending (pids rise
with start order, so this is the newest running instance); `OrderForListing`
matches. Unit tests updated + new
`Same_version_resolves_to_highest_pid_newest_instance` (10/10 VsDiscovery
green, 296/296 full suite). Published clean to `standalone-v5/` (verified
314,152-byte `System.Management.dll`, direct-stdio `list_processes` OK) and
repointed client config; needs client restart + scoped M0–M3 re-run to close.

**Blocked by:** None.

**Status:** ready-for-agent

- [x] Code fix + unit tests green.
- [x] Published v5, config repointed.
- [x] Restarted the client on v5, then scoped E2E re-run: M0 (B reachable),
      M1 (no-override `attach()` → NEW session on B, pid 33088), M2 (same SB),
      M3 (S on A + SB on B). Detached both, killed both VS, deleted
      `%TEMP%/SandboxB`; no devenv remains.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1315.md` row M1 (also M2/M3 show
explicit `attach(B)` works fine, so only the default pick is wrong).
