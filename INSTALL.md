# Install VsDebugMcp on another PC

## One-line install (recommended)

Requires 64-bit Windows + VS 2026 running as the same user as the MCP host.
Close the MCP host first (it locks the exe). Creating `C:\Tools\VsDebugMcp`
normally needs an elevated shell (UAC) — or pass `-Dest`/`--dest` under your profile.

```powershell
irm https://raw.githubusercontent.com/SebastianZonta/Unofficial-VisualStudio-MCP/main/install.ps1 | iex
```

Git Bash / WSL:

```bash
curl -fsSL https://raw.githubusercontent.com/SebastianZonta/Unofficial-VisualStudio-MCP/main/install.sh | bash
```

This downloads the latest GitHub Release zip (`VsDebugMcp-win-x64.zip`, checksum-verified)
to `C:\Tools\VsDebugMcp` (`/c/Tools/VsDebugMcp` under Git Bash; `~/.local/share/vs-debug-mcp`
elsewhere), smoke-tests it on Windows (usage printout, exit 2; skipped with a warning
elsewhere — the server needs Windows + VS 2026), and auto-registers the server in every
detected client (opencode, Claude Code, Claude Desktop, Cursor, VS Code, Windsurf,
Gemini CLI) with a `.bak` backup of each touched config (the pre-installer original,
kept once — a second run never overwrites `.bak`). Codex CLI isn't
auto-detected (TOML `~/.codex/config.toml`, not JSON) — add it by hand
(substitute your install folder if it isn't the default):
```toml
[mcp_servers.vs-debug]
command = "C:\\Tools\\VsDebugMcp\\VsDebugMcp.exe"
```
then restart the client and verify like the Manual-install step 4 below.
A plain re-run deletes the install folder, reinstalls it, and re-merges every detected
client config — nothing else is touched.
Needs a published release —
push a `vX.Y.Z` tag (semver without the `v`; see `.github/workflows/release.yml`).
Re-run with `-ConfigOnly`
(`--config-only`) to re-wire configs without re-downloading (needs the exe already
in place). `install.sh`-only flags: `--exe PATH` wires a different exe (an explicit
relative `--exe` resolves against cwd, not `--dest`); `-h`/`--help` lists flags.
`install.ps1` instead takes `-Exe <path>` for the same rewire job.

## Manual install

Two ways to get the server onto a machine: copy a local publish folder (no build tools needed)
or build from source. All docs: [docs/README.md](docs/README.md).

## Option A — copy a local publish folder

`standalone-vNext/` is a local publish artifact (gitignored — a fresh clone does
not have it). Build it first with the single publish path:

```powershell
./scripts/publish-fresh.ps1 -OutDir standalone-vNext
```

Publish to a fresh empty folder, never over an existing one: the script refuses
locked dirs and trips if a stale `System.Management.dll` placeholder shadows the
real Windows implementation (see
[TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md#stale-publish-dir-wmi-tools-throw-platformnotsupportedexception)).
The client locks the exe while running — stop it before publishing
([Exe locked on republish](docs/TROUBLESHOOTING.md#exe-locked-on-republish)).

Requirements on the target PC: 64-bit Windows, Visual Studio 2026 (running, same user as the
MCP host). No .NET install needed — the runtime is bundled (~85 MB).

1. Publish (see above), then copy the entire folder to the target, e.g. `C:\Tools\VsDebugMcp`.
   Never copy just `VsDebugMcp.dll` from a local (non-`-SingleFile`) publish — it needs
   its sibling DLLs, `VsDebugMcp.deps.json`,
   and `VsDebugMcp.runtimeconfig.json` from the same folder. (The release zip is
   `-SingleFile`: everything is bundled into the exe by design.)
2. Smoke test: run `VsDebugMcp.exe --live-check` with no further args. It should print
   `usage: VsDebugMcp --live-check <file> <line>` and exit with code 2. That proves the bundled
   runtime loads.
3. Register it in the MCP client: re-run the installer with `-Dest <folder> -ConfigOnly`
   (`--dest <folder> --config-only`) to auto-wire every detected client.
   To register by hand instead, add under `mcp.servers` in
   `%USERPROFILE%\.config\opencode\opencode.json`:
   ```json
   "vs-debug": {
     "type": "local",
     "command": ["C:\\Tools\\VsDebugMcp\\VsDebugMcp.exe"]
   }
   ```
   Live is the default; `--live` is still accepted as a no-op, `--fake` selects the canned demo bridge.
   The installer writes each client's own shape for the same exe (`command`/`args`;
   opencode uses `type: local` + `command: [...]`).
4. Restart the client, open VS 2026, and verify: `attach` should return a `sessionId`.

## Option B — build from source

Requirements: .NET 10 SDK, VS 2026. A non-default install path needs `VSINSTALLDIR`
set ([TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md#build-envdte-hintpath-misses)).

```powershell
dotnet build                                    # Debug, for development
dotnet test                                     # no VS required (fake bridge)
./scripts/publish-fresh.ps1 -OutDir standalone-vNext  # the single publish path; fresh empty folder, see above
```

## Debug-time notes

- Same Windows user for the MCP host and VS; elevate VS if the target process is elevated.
- The server binds the newest running VS 2026 (`instanceOverride` pins a specific one).
- Attaching to an arbitrary PID needs `consent: true`; solution processes attach frictionlessly.
- Updating the server: the client locks the exe while running; see
  [Exe locked on republish](docs/TROUBLESHOOTING.md#exe-locked-on-republish).
