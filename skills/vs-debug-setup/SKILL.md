---
name: vs-debug-setup
description: User-only, run once per repo. Records the facts VsDebugMcp flow testing needs (solution, VS instance, target processes, build, elevation, plan and verdict locations) in docs/agents/vs-debug.md and proves them with a smoke check.
disable-model-invocation: true
---

# VS debug setup

Write the **setup doc** (`docs/agents/vs-debug.md`) that every flow-testing session
reads, link it from the repo's agent file, and prove it with one smoke check. Every
fact comes from the repo or from VS; ask the developer only for what neither can tell.

Load the VsDebugMcp tools first (in a client that defers tools, load them by name).

## Steps

1. **Re-run check.** If `docs/agents/vs-debug.md` exists, read it: its values are the
   defaults for every step below, and step 6 rewrites it in place. Done when you hold
   either the existing values or none.
2. **Solution.** Find the `.sln`/`.slnx` files in the repo. One: use it. Several: ask
   which ones flows use. Done when each solution is a repo-relative path.
3. **VS instance and elevation.** Call `list_instances` and match each solution by its
   solution name. The display name changes with every VS start, so the setup doc
   records the solution name as the match.
   - Matched and reachable: VS runs at the agent's elevation. Read it with PowerShell
     `([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrator')`
     and record yes/no.
   - Several instances hold that name: keep the one whose solution path is in this
     repo; still several, ask which one setup uses.
   - An entry's error names `elevation-mismatch`, or the list lacks the solution while
     `Get-Process devenv` shows VS running: this is an elevation mismatch. Tell the
     developer the fix: restart the agent client at the same elevation as VS, both
     elevated or both not, then re-run setup. Stop here.
   - No VS holds the solution: ask the developer to open it in VS 2026, then call
     `list_instances` again.

   Done when every solution has exactly one reachable instance and elevation is recorded.
4. **Target processes.** List the solution's launchable projects (`OutputType` `Exe`,
   or a web SDK project). Ask which ones flows run. Per target, record:
   - the built exe, repo-relative, matching the build command's configuration;
   - default args: the ones a flow run launches with when a plan names none; they must
     keep the target running (a host, a wait loop) so the smoke check sees run mode;
   - env, using `Properties/launchSettings.json` when present: a secret is recorded as
     the name of the env var holding it, never its value;
   - working dir, repo-relative.

   Done when every chosen target has all four.
5. **Build and locations.** Propose the build command (default `dotnet build <solution> -c Debug`)
   and the plan and verdict locations from the [format](#setup-doc-format). Done when
   the developer has accepted or changed each.
6. **Write the setup doc** in the [format](#setup-doc-format). On a re-run, replace the
   sections this skill writes and keep every other section as it is. Done when every
   placeholder holds a value.
7. **Link line.** Use `CLAUDE.md` if it exists, else `AGENTS.md`; with neither, ask
   which to create. The link is the line naming `docs/agents/vs-debug.md`: replace it if
   present, else append it:

   ```markdown
   - Flow testing (VsDebugMcp): read [docs/agents/vs-debug.md](docs/agents/vs-debug.md) before planning or running a flow.
   ```

   Done when the file holds that line exactly once.
8. **Gitignore line.** Root `.gitignore` (create it if missing) holds the verdict folder
   exactly once; on a re-run that moved the
   folder, replace the old line.
9. **Smoke check.** Each item is green or red with the tool output that shows it:
   1. `list_instances` finds each solution (step 3).
   2. The build command exits 0.
   3. Per target: `attach(instanceOverride)` with the matched entry's display name. A
      `mode` other than `design` means the developer is debugging: report red and leave
      that instance alone. Otherwise
      `launch_process(sessionId, executablePath, arguments, workingDirectory, environment, timeoutMs: 10000)`
      with absolute paths. Green when `mode` is `run`. `design` means the target exited:
      check the exe and default args. `break` on a breakpoint that isn't yours:
      `resume(sessionId, timeoutMs: 10000)` once and re-check. `break` on an exception:
      red, quote its `type` and `message`.
   4. Stop what you launched, on every path after `attach`: `Stop-Process -Id <target PID>`
      with the target PID `launch_process` returned (never any other process), then
      `detach(sessionId)`.
10. **Report** a table: each smoke item green/red with its evidence, the files written,
    and a fix for every red. Setup is done when every item is green.

## Setup doc format

```markdown
# VS debug setup

Written by /vs-debug-setup. Re-run it when the solution, targets or build change.

## Solutions
| Solution | VS instance (matched by solution name) |
|---|---|
| `<repo-relative solution path>` | `<solution name>`, VS <version> |

## Target processes
| Name | Executable | Default args | Env | Working dir |
|---|---|---|---|---|
| <name> | `<repo-relative exe>` | `<args>` | `<NAME=value; secrets as NAME only>` | `<repo-relative dir>` |

## Build
`<build command>`

## Elevation
VS runs elevated: <yes/no>. The agent client must run at the same elevation.

## Locations
- Flow plans: `docs/agents/flows/<flow>.md` (committed)
- Verdicts: `docs/agents/flows/runs/<flow>-<yyyy-MM-dd-HHmm>.md` (gitignored)
```
