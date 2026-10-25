<# publish-fresh.ps1: the single local publish path (see docs/agents/definition-of-done.md).
Stops stale-dir poison: dotnet publish never cleans the output dir, so a stale
74 KB System.Management placeholder can shadow the ~314 KB Windows impl and break
list_processes / attach_process (see docs/TROUBLESHOOTING.md stale-dir entry). #>
param(
  [Parameter(Mandatory = $true)][string]$OutDir,
  [string]$Version = '',       # stamped as /p:Version (assembly + package); empty = SDK default
  [switch]$SingleFile          # self-contained single-file win-x64 (what the GitHub Release ships)
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutDir)) { throw "refusing to publish into '$OutDir'" }
if ($OutDir -match '^[A-Za-z]:$') { throw "refusing to publish into '$OutDir'" }  # bare drive letter: refuse before GetFullPath resolves it to cwd
$full = [System.IO.Path]::GetFullPath($OutDir).TrimEnd('\', '/')
if ($full -match '^[A-Za-z]:$' -or $full -eq '\' -or $full -eq '/' -or $full -match '^\\\\[^\\]+(\\[^\\]+)?$') { throw "refusing to publish into '$OutDir'" }
if ($HOME -and ($full -ieq $HOME.TrimEnd('\', '/'))) { throw "refusing to publish into '$OutDir'" }  # never wipe $HOME itself
if (Test-Path -LiteralPath $OutDir) {
  try { Remove-Item -LiteralPath $OutDir -Recurse -Force } catch {
    throw "refusing to publish into locked dir '$OutDir': stop the MCP client first ($($_.Exception.Message))"
  }
}
$pubArgs = @('publish', 'src/VsDebugMcp/VsDebugMcp.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', '-o', $OutDir)
if ($SingleFile) { $pubArgs += @('-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true') }
if ($Version) { $pubArgs += @("-p:Version=$Version") }
& dotnet @pubArgs
$exe = Join-Path $OutDir 'VsDebugMcp.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "publish incomplete: $exe missing" }
if ($SingleFile) {
  # Single-file bundles managed assemblies into the exe: no System.Management.dll on disk by design.
} else {
  $dll = Join-Path $OutDir 'System.Management.dll'
  $len = (Get-Item -LiteralPath $dll).Length
  if ($len -lt 100KB) { throw "publish poisoned: $dll is $len bytes (placeholder stub, want the ~314 KB Windows impl)" }
}
$mcpLine = "MCP binary: $exe ($((Get-Item $exe).LastWriteTimeUtc.ToString('u')))"
Write-Output $mcpLine
Write-Output "Paste the line above into the E2E run report header."
