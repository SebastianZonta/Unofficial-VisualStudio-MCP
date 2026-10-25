# Publishing options for the VsDebugMcp server

**Recommendation:** publish a prebuilt self-contained single-file `win-x64` zip as a
**GitHub Release**, built by a tag-triggered GitHub Actions workflow, plus small
install scripts (`install.ps1` + `install.sh`) that coworkers run once. This keeps the current
local-publish-folder distribution model from `INSTALL.md` but removes the "clone the repo
and run `dotnet publish`" step. Optionally add a `.NET tool` package (NuGet) later —
it is viable since .NET 10, and it unlocks the official MCP Registry entry for free.

Why this fits: the server is Windows-x64-only, must run on the same box as VS 2026
as the same user (COM/EnvDTE via the ROT), and is a plain stdio process
(`INSTALL.md`, `AGENTS.md`). Anything remote/hosted or in-IDE is the wrong shape
(see "Ruled out" below).

## Options at a glance

| Option | Coworker effort | Owner effort | Fits? |
|---|---|---|---|
| GitHub Release zip + install script | Download zip, run script | One workflow file | **Yes — do this** |
| .NET tool via NuGet (`dotnet tool install`) | One command, needs .NET SDK | `PackAsTool` + `ToolPackageRuntimeIdentifiers` | Viable, good second step |
| MCP Registry (`registry.modelcontextprotocol.io`) | Discoverable metadata | `server.json` + `mcp-publisher publish` | Viable *via* the NuGet package; remote-only entry does not apply |
| MCPB bundle (`.mcpb`) | Double-click install | `mcpb init` + `mcpb pack` with `server.type: binary` | Partial — only helps Claude Desktop users |
| Winget (`winget-pkgs` PR) or local-manifest install | `winget install` | Manifest authoring + PR review | OK, heavier / public |
| Private share / intranet zip | Copy folder | Zero infra | OK, lowest-effort internal fallback |
| VSIX / VS Marketplace | — | — | No (wrong vehicle) |
| Docker / OCI image | — | — | No (must run on the VS box) |
| npm / PyPI wrapper | — | — | No (pointless indirection) |

Detail and sources for each follow; implementation snippets are in
[Recommended implementation](#recommended-implementation).

## a. GitHub Releases + install script (recommended)

GitHub Releases exist exactly for this: bundle iterations of a project with
release notes and binary files for other people to use
([docs](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)).
The owner builds once in CI; coworkers never touch `dotnet`.

- **Single-file publish.** `<PublishSingleFile>true</PublishSingleFile>` bundles
  all app files into one platform-specific executable; note that setting it
  implicitly sets `PublishSelfContained` to `true`
  ([dotnet-publish](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish),
  [single-file overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)).
  Single-file apps are always OS/architecture-specific — publish per RID
  ([deploying overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)).
- **Framework-dependent vs self-contained.** Self-contained includes the runtime
  (no .NET needed on target, larger download); framework-dependent needs the
  target .NET runtime installed but is small and rolls forward to patched
  runtimes
  ([deploying overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)).
  For coworkers without an SDK, **self-contained win-x64** is the right default —
  it matches today's ~85 MB local publish folder (`INSTALL.md`).
- **Coworker install** is the one-line flow in `INSTALL.md` (`install.ps1` /
  `install.sh` against `releases/latest/download`): checksum-verified zip,
  `--live-check` smoke test, then auto-registration of every detected client
  (Codex CLI stays manual-only: TOML, not JSON — see `INSTALL.md`).

## b. .NET tool via NuGet — viable since .NET 10

A .NET tool is a special NuGet package installed with `dotnet tool install -g <id>`
into `%USERPROFILE%\.dotnet\tools` (Windows), a per-user location added to PATH
([global tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools)).
Packaging one is `<PackAsTool>true</PackAsTool>` + `<ToolCommandName>` in the
csproj and `dotnet pack`
([tutorial](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create)).

The old objection (tools were framework-dependent only) is gone: starting with
.NET 10, `ToolPackageRuntimeIdentifiers` (e.g. `win-x64`) produces
**self-contained, RID-specific tool packages** — explicitly documented as useful
"for command-line tools like MCP servers" — and the CLI picks the right RID
transparently; adding `any` keeps a framework-dependent fallback
([RID-specific tools](https://learn.microsoft.com/en-us/dotnet/core/tools/rid-specific-tools),
[breaking-change note](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-tool-pack-publish)).
The .NET 10 `dnx` helper can even run the tool in one shot without a prior
install (`dnx <package-id> --yes`, same doc).

Fit notes: the install command needs the .NET SDK on the coworker box, and the
registration path becomes `%USERPROFILE%\.dotnet\tools\<command>.exe`. Also mark
the package `<PackageType>McpServer</PackageType>` — NuGet documents that
`McpServer` packages are always accompanied by `DotnetTool` "because a local MCP
server is distributed as a .NET tool", and nuget.org renders MCP consumption
snippets for them
([set-package-type](https://learn.microsoft.com/en-us/nuget/create-packages/set-package-type),
[nuget-mcp](https://learn.microsoft.com/en-us/nuget/concepts/nuget-mcp)).
For a purely internal audience, any private feed (e.g. Azure Artifacts) can host
the package instead of nuget.org (same nuget-mcp page). `mcpserver` template
projects already default to self-contained multi-RID tool packaging
([quickstart](https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server)).

## c. MCP Registry and MCPB

**MCP Registry** (`registry.modelcontextprotocol.io`) stores *metadata pointing
at packages*, not binaries
([about](https://modelcontextprotocol.io/registry/about)).
Publishing is a `server.json` file (schema + `name`, `version`, `packages[]`
with `registryType`/`identifier`/`transport: stdio`) via the `mcp-publisher` CLI:
`init` → `login github` → `publish`, which authenticates the `io.github.<user>/*`
namespace
([quickstart](https://github.com/modelcontextprotocol/registry/blob/main/docs/modelcontextprotocol-io/quickstart.mdx),
[.NET quickstart](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/publish-mcp-registry)).
Two constraints decide the design:

- Only trusted public registries are accepted; for NuGet that is
  `https://api.nuget.org/v3/index.json` only
  ([official-registry-requirements](https://github.com/modelcontextprotocol/registry/blob/main/docs/reference/server-json/official-registry-requirements.md)).
- NuGet ownership is verified by an `mcp-name: <server-name>` string in the
  package README
  ([package-types](https://modelcontextprotocol.io/registry/package-types.md)).

So the registry entry rides on option (b): publish the NuGet tool package first,
then `mcp-publisher publish`. A "remote-only" entry (no package, just a URL)
does not apply — this server has no public endpoint and must run beside VS.
`description` is limited to 100 chars and published versions are immutable
([quickstart](https://github.com/modelcontextprotocol/registry/blob/main/docs/modelcontextprotocol-io/quickstart.mdx),
[FAQ](https://modelcontextprotocol.io/registry/faq)).

**MCPB** (`.mcpb`) is a zip of server + `manifest.json` (`mcpb init` /
`mcpb pack` via `npm install -g @anthropic-ai/mcpb`); the spec supports
`"server": { "type": "binary" }`, which covers a prebuilt exe
([MANIFEST.md](https://github.com/anthropics/mcpb/blob/main/MANIFEST.md),
[CLI](https://github.com/modelcontextprotocol/mcpb/blob/main/CLI.md)).
But its consumer story is single-click install **in Claude Desktop** on
macOS/Windows
([Anthropic](https://www.anthropic.com/engineering/desktop-extensions),
[Claude docs](https://claude.com/docs/connectors/building/mcpb)).
Worth doing only if coworkers use Claude Desktop; it does not register the
server in opencode / VS Code / Claude Code.

## d. Winget and private share

**Winget:** submit a YAML manifest PR to the public
[`winget-pkgs`](https://github.com/microsoft/winget-pkgs) repo (authored by hand
or `wingetcreate new`, validated with `winget validate --manifest`)
([manifest](https://learn.microsoft.com/en-us/windows/package-manager/package/manifest),
[submit](https://learn.microsoft.com/en-us/windows/package-manager/package/repository)).
For purely internal use, skip the public repo: coworkers can
`winget install --manifest <path-to-yaml>` directly
([install](https://learn.microsoft.com/en-us/windows/package-manager/winget/install)).
Heavier than a release zip for the same outcome; consider only if the team
already standardizes on winget.

**Private share / intranet zip:** no tooling or approvals — keep doing
`INSTALL.md` Option A with the zip on a file share. Zero owner effort, but no
versioning, no discoverability, manual updates.

## Ruled out (one line each)

- **VSIX / VS Marketplace:** a `.vsix` contains Visual Studio *extensions* that
  run inside VS
  ([anatomy](https://learn.microsoft.com/en-us/visualstudio/extensibility/anatomy-of-a-vsix-package?view=vs-2022));
  this server is an external stdio process — wrong vehicle.
- **Docker / OCI image:** the server must run on the same Windows box as VS as
  the same user over COM (`INSTALL.md`); containerizing it breaks that.
- **npm / PyPI wrapper:** the registry does support `npm`/`pypi` package types
  ([package-types](https://modelcontextprotocol.io/registry/package-types.md)),
  but wrapping a win-only .NET exe in a Node/Python launcher only adds a
  runtime dependency and indirection.

## Recommended implementation

### 1. Publish flags (CI command line only)

Pass single-file flags on the CI command line, not in the csproj, so a plain
local `dotnet publish` keeps working. `PublishSingleFile` implies
self-contained per the `dotnet-publish` doc linked above, but pass
`--self-contained` explicitly anyway since the doc recommends stating it
explicitly. Do **not** add `PublishTrimmed`: trimming + COM interop /
reflection is a classic breakage source, and the size win is irrelevant next
to the runtime (never ship just the dll — the whole output folder is the unit,
`INSTALL.md`).

Version flow: `Version` drives `AssemblyVersion`/`InformationalVersion` code
generation
([SDK targets](https://github.com/dotnet/sdk/blob/master/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.GenerateAssemblyInfo.targets))
and `PackageVersion` defaults to `Version`
([NuGet MSBuild targets](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets)) —
so one `/p:Version=<tag>` stamps assembly + package together.

### 2. Publish command (superseded sketch — CI runs `publish-fresh.ps1`)

The raw command below was the starting point; the landed workflow runs
`./scripts/publish-fresh.ps1 -OutDir publish/win-x64 -Version $version -SingleFile`
instead (fresh-dir guard + poison check included).

```powershell
dotnet publish src/VsDebugMcp/VsDebugMcp.csproj -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=1.2.3 -o publish/win-x64
```

Keep shipping the whole output folder as the zip (single-file collapses most
of it into one exe, but the folder stays the unit — never ship just the dll,
`INSTALL.md`).
Do **not** add `PublishTrimmed`: trimming + COM interop / reflection is a
classic breakage source, and the size win is irrelevant next to the runtime.

### 3. GitHub Actions release workflow (`.github/workflows/release.yml`, landed)

Uses only first-party actions, pinned to commit SHAs
(which installs the `global.json` SDK version when `dotnet-version: 10.0.x` is given —
see its [`action.yml`](https://github.com/actions/setup-dotnet/blob/main/action.yml)).
`ref_name` flows via `env:`, never interpolated into `run:` (script-injection safe).
Release creation uses `gh release create` (asset upload included —
see the [`gh release create` manual](https://cli.github.com/manual/gh_release_create)).
Reproduced here verbatim — edit the workflow file, not this copy:

```yaml
name: release
on:
  push:
    tags: ['v*']          # push v1.2.3 to ship
permissions:
  contents: write         # needed to create the GitHub Release
jobs:
  publish:
    runs-on: windows-latest   # win-x64 RID must build on Windows (EnvDTE refs)
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4 (pinned SHA: supply-chain anchoring)

      - name: Validate tag
        shell: pwsh
        env:
          GITHUB_REF_NAME: ${{ github.ref_name }}  # via env: never interpolate ${{ }} inside run: (script-injection safe)
        run: |
          $version = $env:GITHUB_REF_NAME.Substring(1)
          if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Bad tag: $env:GITHUB_REF_NAME" }

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4 (pinned SHA)
        with:
          dotnet-version: 10.0.x

      - name: Resolve VS install path (vswhere)
        shell: pwsh
        run: |
          $installPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
          if (-not $installPath) { throw 'vswhere found no Visual Studio installation.' }
          "VSINSTALLDIR=$installPath\" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8

      - name: Test
        run: dotnet test VsDebugMcp.slnx -c Release

      - name: Publish win-x64
        shell: pwsh
        env:
          GITHUB_REF_NAME: ${{ github.ref_name }}
        run: |
          $version = $env:GITHUB_REF_NAME.Substring(1)
          ./scripts/publish-fresh.ps1 -OutDir publish/win-x64 -Version $version -SingleFile
          Compress-Archive -Force publish/win-x64/* 'VsDebugMcp-win-x64.zip'
          # LF-only sums: macOS shasum chokes on the CRLF that Out-File -Encoding ascii writes
          [System.IO.File]::WriteAllText('SHA256SUMS.txt', ((Get-FileHash 'VsDebugMcp-win-x64.zip' -Algorithm SHA256).Hash.ToLower() + '  VsDebugMcp-win-x64.zip' + "`n"))

      - name: Smoke-test single-file bundle
        shell: pwsh
        run: |
          & ./publish/win-x64/VsDebugMcp.exe --live-check  # expect usage printout (exit 2), proves the shipped bundle loads
          if ($LASTEXITCODE -ne 2) { throw "Smoke test failed: exit $LASTEXITCODE, want 2 (usage line)." }

      - name: Create release
        shell: pwsh
        run: gh release create $env:GITHUB_REF_NAME --generate-notes 'VsDebugMcp-win-x64.zip' 'SHA256SUMS.txt' 'install.ps1' 'install.sh' || gh release upload $env:GITHUB_REF_NAME 'VsDebugMcp-win-x64.zip' 'SHA256SUMS.txt' 'install.ps1' 'install.sh' --clobber
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          GITHUB_REF_NAME: ${{ github.ref_name }}
```

Notes: `windows-latest` is required, not incidental — the csproj resolves
EnvDTE from the VS install (`VsDebugMcp.csproj`), so the build needs VS
present; the workflow resolves it via `vswhere` → `VSINSTALLDIR` (same as
`ci.yml`). Releases attach binaries to a tag
([managing releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)).

### 4. Coworker install scripts (landed: `install.ps1` + `install.sh` at repo root)

The sketch below was the starting point; the landed scripts additionally
auto-detect installed clients (opencode, Claude Code, Claude Desktop, Cursor,
VS Code, Windsurf, Gemini CLI) and merge a `vs-debug` entry into each config
(`.bak` backup of the pre-installer original, idempotent, corrupt JSON skipped with a warning,
existing entry keys such as `env` preserved). `install.sh`
is the same flow in bash for Git Bash/WSL (without `python3` it prints a manual
snippet instead of wiring).
Re-wire without re-downloading via `-ConfigOnly` / `--config-only`.

```powershell
# Sketch only — the starting point, not the landed scripts. Differences from
# the landed install.ps1: the checksum here takes the whole file's first token
# instead of the anchored VsDebugMcp-win-x64.zip entry line, Compress-Archive
# lacks -Force, and there is no client auto-wire (see the scripts).
param([string]$Dest = 'C:\Tools\VsDebugMcp') # install folder; override with -Dest <path>
# Run AFTER closing the MCP host (opencode/VS Code): it holds VsDebugMcp.exe locked.
$ErrorActionPreference = 'Stop'
$dest = $Dest
$base = 'https://github.com/SebastianZonta/Unofficial-VisualStudio-MCP/releases/latest/download'
$zip = "$env:TEMP\VsDebugMcp-win-x64.zip"
if (Get-Process VsDebugMcp -ErrorAction SilentlyContinue) { throw 'Close the MCP host first — VsDebugMcp.exe is still running.' }
Invoke-WebRequest -Uri "$base/VsDebugMcp-win-x64.zip" -OutFile $zip -UseBasicParsing
$expected = (Invoke-WebRequest -Uri "$base/SHA256SUMS.txt" -UseBasicParsing).Content.Split()[0].ToLower()
$actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
if ($actual -ne $expected) { throw "Checksum mismatch — delete $zip and retry." }
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Expand-Archive $zip $dest
Remove-Item $zip -Force
& "$dest\VsDebugMcp.exe" --live-check   # expect: usage printout (exit 2), proves runtime loads
# ...then Wire-Clients auto-registers every detected client (see the script).
Write-Host "Installed to $dest."
```

Registration snippet (unchanged from `INSTALL.md`):

```json
"vs-debug": {
  "type": "local",
  "command": ["C:\\Tools\\VsDebugMcp\\VsDebugMcp.exe"]
}
```

### 5. Update story

The MCP host locks the exe while running, so updates are: close the client,
re-run `install.ps1` (or close the client, publish to a clean empty folder, and copy it over),
restart the client
([TROUBLESHOOTING.md](TROUBLESHOOTING.md#exe-locked-on-republish)).
`--live-check` (no further args, exit 2) after each update proves the new binary loads
(`INSTALL.md`).
