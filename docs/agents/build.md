# Build & test

```powershell
dotnet build VsDebugMcp.slnx              # solution is .slnx, not .sln; .NET 10 SDK (see global.json)
dotnet test VsDebugMcp.slnx --no-build    # same order as CI (.github/workflows/ci.yml)
dotnet format whitespace VsDebugMcp.slnx --include <touched-file.cs> --verify-no-changes --no-restore  # format gate for touched files only
dotnet test --filter "FullyQualifiedName~<TestClass>"  # single suite
```

- Tests run against `FakeDebugBridge` — no VS needed.
- `src/` has `TreatWarningsAsErrors=true` + analysis; `tests/` deliberately opts out (`TreatWarningsAsErrors=false`, `AnalysisMode=None`). Do not "fix" the asymmetry.
- CI resolves VS via `vswhere` → `$env:VSINSTALLDIR`; locally the csproj falls back to the VS 2026 Enterprise, Professional, then Community default path.
- EnvDTE refs are build-time only (embedded interop). If build fails resolving `EnvDTE*`, retarget the `HintPath`s in `src/VsDebugMcp/VsDebugMcp.csproj` to your edition/path; runtime binds DTE from the ROT, never a hardcoded ProgID.

## Publish

- Publish the MCP via `scripts/publish-fresh.ps1` to a fresh **empty** folder before the E2E pass (src/bin is locked while hosts run: stop the clients first; repoint client config after publishing). Paste its `MCP binary:` line into the report header. Release builds add `-Version <semver without v> -SingleFile` (see `.github/workflows/release.yml`); local E2E publishes use neither.
- Republishing while the MCP host runs fails (exe locked): stop the client first, or publish to a fresh **empty** folder and repoint config (`dotnet publish` never cleans the output dir — a stale `System.Management.dll` placeholder breaks WMI tools, see [Stale publish dir](../TROUBLESHOOTING.md#stale-publish-dir-wmi-tools-throw-platformnotsupportedexception)). `standalone*/` output is gitignored.
