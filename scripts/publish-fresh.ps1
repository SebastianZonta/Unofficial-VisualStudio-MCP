<# publish-fresh.ps1: the single local publish path (see docs/agents/definition-of-done.md).
Stops stale-dir poison: dotnet publish never cleans the output dir, so a stale
74 KB System.Management placeholder can shadow the ~314 KB Windows impl and break
list_processes / attach_process (see docs/TROUBLESHOOTING.md stale-dir entry). #>
param(
  [Parameter(Mandatory = $true)][string]$OutDir
)
$ErrorActionPreference = 'Stop'
if (Test-Path $OutDir) {
  try { Remove-Item -Recurse -Force $OutDir } catch {
    throw "refusing to publish into locked dir '$OutDir': stop the MCP client first ($($_.Exception.Message))"
  }
}
dotnet publish src/VsDebugMcp/VsDebugMcp.csproj -c Release -r win-x64 --self-contained -o $OutDir
$dll = Join-Path $OutDir 'System.Management.dll'
$len = (Get-Item $dll).Length
if ($len -lt 100KB) { throw "publish poisoned: $dll is $len bytes (placeholder stub, want the ~314 KB Windows impl)" }
$exe = Join-Path $OutDir 'VsDebugMcp.exe'
$mcpLine = "MCP binary: $exe ($((Get-Item $exe).LastWriteTimeUtc.ToString('u')))"
Write-Output $mcpLine
Write-Output "Paste the line above into the E2E run report header."
