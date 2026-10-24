# Install VsDebugMcp on another PC

Two ways to get the server onto a machine: copy the `standalone` folder (no build tools needed)
or build from source.

## Option A — copy `standalone` (recommended)

`standalone/` is a local publish artifact (gitignored — a fresh clone does
not have it). Build it first:

```powershell
dotnet publish src/VsDebugMcp/VsDebugMcp.csproj -c Release -r win-x64 --self-contained -o standalone
```

Requirements on the target PC: 64-bit Windows, Visual Studio 2026 (running, same user as the
MCP host). No .NET install needed — the runtime is bundled (~85 MB).

0. Publish (see above), then copy the entire `standalone` folder to the target, e.g. `C:\Tools\VsDebugMcp`.
   Never copy just `VsDebugMcp.dll` — it needs its sibling DLLs, `VsDebugMcp.deps.json`,
   and `VsDebugMcp.runtimeconfig.json` from the same folder.
2. Smoke test: run `VsDebugMcp.exe --live-check` with no further args. It should print
   `usage: VsDebugMcp --live-check <file> <line>` and exit. That proves the bundled
   runtime loads.
3. Register it in the MCP client. For opencode, edit
   `%USERPROFILE%\.config\opencode\opencode.json` and add under `mcp.servers`:
   ```json
   "vs-debug": {
     "type": "local",
     "command": ["C:\\Tools\\VsDebugMcp\\VsDebugMcp.exe"]
    }
    ```
    Live is the default; `--live` is still accepted as a no-op, `--fake` selects the canned demo bridge.
   }
   ```
   Other clients (Claude Code, VS Code `mcp.json`) take the same command + args shape.
4. Restart the client, open VS 2026, and verify: `attach` should return a `sessionId`.

## Option B — build from source

Requirements: .NET 10 SDK, VS 2026. Note the five EnvDTE `HintPath`s in
`src/VsDebugMcp/VsDebugMcp.csproj` point at VS 2026 **Community** in the default path
(`C:\Program Files\Microsoft Visual Studio\18\Community\…`) — retarget them if you use a
different edition or install path. They are build-time only (embedded interop).

```powershell
dotnet build                                    # Debug, for development
dotnet test                                     # no VS required (fake bridge)
dotnet publish src/VsDebugMcp/VsDebugMcp.csproj -c Release -r win-x64 --self-contained -o standalone
```

## Debug-time notes

- Same Windows user for the MCP host and VS; elevate VS if the target process is elevated.
- The server binds the newest running VS 2026 (`instanceOverride` pins a specific one).
- Attaching to an arbitrary PID needs `consent: true`; solution processes attach frictionlessly.
- Updating the server: the client locks the exe while running — overwrite the folder with the
  client closed, or publish to a fresh folder and repoint the config.
