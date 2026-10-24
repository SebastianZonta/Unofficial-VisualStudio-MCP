# 03: Stale `standalone/` publish poisoned System.Management (D3/D4/A3)

**What broke (E2E 2026-10-24-1315):** D3/D4 — every `list_processes` call
fails with a bare `An error occurred invoking 'list_processes'.` (no code).
A3 — consent:true `attach_process` fails the same way (`consent-required` and
pid-validation paths work, so only the success path is broken). BLOCKED: A4–A7
(all need A3's bound target).

**Root cause (isolated live, deterministic):** the `standalone/` publish from
13:01 contained a stale 74,024-byte `System.Management.dll` (NuGet
`lib/net10.0` placeholder asset, hash `58603A…`) at top level plus a leftover
`runtimes/win/lib/net10.0/` copy from an older publish. `dotnet publish` does
not clean the output dir, so the stale files survived. The placeholder throws
`PlatformNotSupportedException: System.Management currently is only supported
for Windows desktop applications` from `ManagementObjectSearcher..ctor`
(server stderr confirms; full stack in E2E triage notes). Framework-dependent
build (`bin/Debug/…/VsDebugMcp.exe`) and `dotnet VsDebugMcp.dll` never hit it;
a fresh self-contained publish to an empty dir produces the real 314,152-byte
implementation at top level and `list_processes` returns `IsError = False`
(~12 s for the WMI owner scan). Deleting `runtimes/` alone did NOT fix it;
only the top-level DLL content mattered.

**Fix (done operationally):** published clean to `standalone-v4/` (verified
`System.Management.dll` = 314,152 bytes, direct-stdio `list_processes` OK)
and repointed `%USERPROFILE%\.config\opencode\opencode.json` vs-debug to
`standalone-v4\VsDebugMcp.exe`. Added TROUBLESHOOTING entry
(Stale publish dir). Leftover poisoned `standalone/`, `standalone-v2/`,
`standalone-v3/` dirs still on disk (gitignored).

**Blocked by:** None.

**Status:** needs-triage

- [x] Restarted the client on v4: `list_processes` returns proper results live.
- [x] Scoped E2E re-run on v4 binary: D3, D4, A1–A7 all PASS (fresh VS A
      pid 24712, waitPid 26956; foreign `other1@STACK_BOTTOM` from persisted
      `.vs` state noted, then `.vs` deleted).
- [ ] Optional: delete `standalone/`, `standalone-v2/`, `standalone-v3/`,
      `standalone-v4/` (v5 is current).
- [x] Publish-hygiene guard added in CI (`Publish hygiene` step: clean-dir
      publish + `System.Management.dll` > 100 KB + `--live-check` smoke),
      proven green locally.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1315.md` rows D3, D4, A3, A4–A7.
Diagnosis: direct-stdio probes (`%TEMP%\opencode\dbg_probe.py`, since removed);
matrix was standalone-13:01 FAIL, `bin/Debug` exe OK, `dotnet dll` OK,
fresh-SC-publish OK, standalone-after-`runtimes/`-delete still FAIL,
`standalone-v4` OK.
