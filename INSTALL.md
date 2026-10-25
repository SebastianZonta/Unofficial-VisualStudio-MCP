# Install VsDebugMcp

> Pick your path first: the server runs only on 64-bit Windows with VS 2026
> (same user as the MCP host). The one-liner downloads the latest GitHub
> Release — with no published release yet it has nothing to install, so use
> Option A/B instead. On Linux/macOS follow [docs/agents/build.md](docs/agents/build.md)
> (build + test only); the installer there only wires client configs.
> Close the MCP host first (it locks the exe).

| Situation | Path |
|---|---|
| Fresh install on Windows, release exists | One-line install below |
| No release yet, or local build to copy | Option A — copy a local publish folder |
| Hack on the server itself | Option B — build from source |

## One-line install (recommended)

Requires 64-bit Windows + VS 2026 running as the same user as the MCP host.
Creating `C:\Tools\VsDebugMcp`
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

| Flag | `install.ps1` | `install.sh` | Meaning |
|---|---|---|---|
| dest | `-Dest <folder>` | `--dest <folder>` | Install folder (default `C:\Tools\VsDebugMcp`; `/c/Tools/VsDebugMcp` under Git Bash; `~/.local/share/vs-debug-mcp` elsewhere) |
| exe | `-Exe <path>` | `--exe PATH` | Wire a different exe (an explicit relative `--exe` resolves against cwd, not `--dest`) |
| config-only | `-ConfigOnly` | `--config-only` | Re-wire configs without re-downloading (needs the exe already in place) |
| help (`install.sh` only) | — | `-h` / `--help` | List flags |

## Manual install

Two ways to get the server onto a machine: copy a local publish folder (no build tools needed)
or build from source. All docs: [docs/README.md](docs/README.md).

## Option A — copy a local publish folder

`standalone-vNext/` is a local publish artifact (gitignored — a fresh clone does
not have it). Build it first with the single publish path
(see [docs/agents/build.md](docs/agents/build.md#publish)):

```powershell
./scripts/publish-fresh.ps1 -OutDir standalone-vNext
```

Publish to a fresh empty folder and stop the client first (it locks the exe) —
see `build.md` for why (`System.Management.dll` placeholder, stale-dir poison).

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

Requirements and commands live in [docs/agents/build.md](docs/agents/build.md):
.NET 10 SDK, VS 2026 (`VSINSTALLDIR` for non-default paths),
`dotnet build` / `dotnet test` (fake bridge, no VS needed), then the same
`publish-fresh.ps1` publish path as Option A.

## Debug-time notes

- Same Windows user for the MCP host and VS; elevate VS if the target process is elevated.
- The server binds the newest running VS 2026 (`instanceOverride` pins a specific one).
- Attaching to an arbitrary PID needs `consent: true`; solution processes attach frictionlessly.
- Updating the server: the client locks the exe while running; see
  [Exe locked on republish](docs/TROUBLESHOOTING.md#exe-locked-on-republish).
