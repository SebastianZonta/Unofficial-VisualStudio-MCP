<# install.ps1: one-shot VsDebugMcp install for Windows.
Downloads the latest GitHub Release zip, verifies its checksum, extracts it,
smoke-tests the exe, then detects installed agentic tools and registers the
server in each one's config so it connects out of the box.

Usage:
  irm https://raw.githubusercontent.com/SebastianZonta/Unofficial-VisualStudio-MCP/main/install.ps1 | iex
  # or, from a clone:
  .\install.ps1 [-Dest C:\Tools\VsDebugMcp] [-Exe <path-to-exe>] [-ConfigOnly]

Run AFTER closing the MCP host (opencode/VS Code/...): it holds VsDebugMcp.exe locked.
Requires: 64-bit Windows, Visual Studio 2026 running as the same user as the MCP host. #>
param(
  [string]$Dest = 'C:\Tools\VsDebugMcp', # install folder; override with -Dest <path>
  [string]$Exe = '',                      # server exe to wire (default $Dest\VsDebugMcp.exe)
  [switch]$ConfigOnly                     # skip download: only (re)wire client configs
)
$ErrorActionPreference = 'Stop'

# Client configs store the command verbatim: resolve a relative -Dest against
# cwd so the wired entry works no matter where the client starts.
if ([string]::IsNullOrWhiteSpace($Dest)) { throw "refusing to install into '$Dest'" }
if ($Dest -match '^[A-Za-z]:$') { throw "refusing to install into '$Dest'" }  # bare drive letter (cwd-on-drive): refuse before GetFullPath resolves it to cwd
if ($Dest -eq '~') { $Dest = $HOME } elseif ($Dest -like '~/*' -or $Dest -like '~\*') { $Dest = Join-Path $HOME ($Dest.Substring(2)) }
if ($Exe -eq '~') { $Exe = $HOME } elseif ($Exe -like '~/*' -or $Exe -like '~\*') { $Exe = Join-Path $HOME ($Exe.Substring(2)) }
$Dest = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Dest)
$full = [System.IO.Path]::GetFullPath($Dest).TrimEnd('\', '/')  # GetFullPath also normalizes C:\. / C:\.. to the root
if ($full -match '^[A-Za-z]:$' -or $full -eq '\' -or $full -eq '/' -or $full -match '^\\\\[^\\]+(\\[^\\]+)?$') { throw "refusing to install into '$Dest'" }
if ($HOME -and ($full -ieq $HOME.TrimEnd('\', '/'))) { throw "refusing to install into '$Dest'" }  # never wipe $HOME itself (subfolders are fine)

$Repo = 'SebastianZonta/Unofficial-VisualStudio-MCP'
$Base = "https://github.com/$Repo/releases/latest/download"
$ExeName = 'VsDebugMcp.exe'

function To-Hashtable($o) {
  # Recursive PSCustomObject -> hashtable so this runs on Windows PowerShell 5.1
  # (ConvertFrom-Json -AsHashtable needs PowerShell 7+).
  if ($o -is [System.Management.Automation.PSCustomObject]) {
    $h = @{}
    foreach ($p in $o.PSObject.Properties) { $h[$p.Name] = To-Hashtable $p.Value }
    return $h
  }
  if ($o -is [System.Collections.IList] -and $o -isnot [string]) { return @($o | ForEach-Object { To-Hashtable $_ }) }
  return $o
}

function Merge-JsonFile([string]$Path, [scriptblock]$Merge) {
  $dir = [System.IO.Path]::GetDirectoryName($Path)  # Split-Path -LiteralPath -Parent is ambiguous on Windows PowerShell 5.1
  if ($dir -and -not (Test-Path -LiteralPath $dir)) { [System.IO.Directory]::CreateDirectory($dir) | Out-Null }
  $obj = @{}
  if (Test-Path -LiteralPath $Path) {
    try {
      $raw = [System.IO.File]::ReadAllText($Path, (New-Object System.Text.UTF8Encoding $false, $true))  # strict: invalid bytes throw (skip) instead of silently sanitizing, matching install.sh
      if (-not [string]::IsNullOrWhiteSpace($raw)) { $obj = To-Hashtable ($raw | ConvertFrom-Json) }
      if (-not ($obj -is [hashtable])) {
        Write-Warning "Skipping ${Path}: root is not a JSON object; left untouched"
        return $false
      }
    }
    catch { Write-Warning "Skipping ${Path}: not valid JSON ($($_.Exception.Message))"; return $false }
  }
  try { $obj = & $Merge $obj }
  catch { Write-Warning "Skipping ${Path}: $($_.Exception.Message)"; return $false }
  if ((Test-Path -LiteralPath $Path) -and -not (Test-Path -LiteralPath "$Path.bak")) { Copy-Item -LiteralPath $Path "$Path.bak" -Force }  # keep the pre-installer original, written once
  [System.IO.File]::WriteAllText($Path, (($obj | ConvertTo-Json -Depth 100) + "`n"), (New-Object System.Text.UTF8Encoding $false))  # BOM-less + trailing LF (matches install.sh)
  return $true
}

function Add-McpServer([string]$Client, [string]$Path, [string]$Style, [string]$Exe) {
  # Preserve user customizations (env, cwd, ...): only our keys are set.
  $setEntry = {
    param($section, $entry)
    $cur = $section['vs-debug']
    if (-not ($cur -is [hashtable])) { $cur = @{} } else { $cur = $cur.Clone() }
    foreach ($k in $entry.Keys) { $cur[$k] = $entry[$k] }
    $section['vs-debug'] = $cur
  }.GetNewClosure()
  $ok = Merge-JsonFile $Path {
    param($c)
    if ($Style -eq 'opencode') {
      if ($c.ContainsKey('mcp') -and -not ($c['mcp'] -is [hashtable])) { throw "'mcp' is not a JSON object; left untouched" }
      if (-not ($c['mcp'] -is [hashtable])) { $c['mcp'] = @{} }
      if ($c['mcp'].ContainsKey('servers') -or $c['mcp'].Count -eq 0) {
        # INSTALL.md shape: mcp.servers (fresh files use it too)
        if ($c['mcp'].ContainsKey('servers') -and -not ($c['mcp']['servers'] -is [hashtable])) { throw "'mcp.servers' is not a JSON object; left untouched" }
        if (-not ($c['mcp']['servers'] -is [hashtable])) { $c['mcp']['servers'] = @{} }
        & $setEntry $c['mcp']['servers'] @{ type = 'local'; command = @($Exe) }
      } else {
        # new-style top-level mcp entries: keep the existing shape
        & $setEntry $c['mcp'] @{ type = 'local'; command = @($Exe) }
      }
    } elseif ($Style -eq 'vscode') {
      if ($c.ContainsKey('servers') -and -not ($c['servers'] -is [hashtable])) { throw "'servers' is not a JSON object; left untouched" }
      if (-not ($c['servers'] -is [hashtable])) { $c['servers'] = @{} }
      & $setEntry $c['servers'] @{ type = 'stdio'; command = $Exe; args = @() }
    } else {
      if ($c.ContainsKey('mcpServers') -and -not ($c['mcpServers'] -is [hashtable])) { throw "'mcpServers' is not a JSON object; left untouched" }
      if (-not ($c['mcpServers'] -is [hashtable])) { $c['mcpServers'] = @{} }
      $entry = @{ command = $Exe; args = @() }
      if ($Style -eq 'mcpServersTyped') { $entry['type'] = 'stdio' }
      & $setEntry $c['mcpServers'] $entry
    }
    $c
  }.GetNewClosure()
  if ($ok) { Write-Host "  [wired] $Client ($Path)" }
  return $ok
}

function Test-Client([string]$Cmd, [string]$ProbePath) {
  if (Get-Command $Cmd -ErrorAction SilentlyContinue) { return $true }
  if ($ProbePath -and (Test-Path -LiteralPath $ProbePath)) { return $true }
  return $false
}

function Wire-Clients([string]$Exe) {
  Write-Host 'Detecting agentic tools...'
  $homeDir = $HOME
  $appData = $env:APPDATA; if (-not $appData) { $appData = Join-Path $homeDir 'AppData\Roaming' }
  $wired = 0

  # opencode (INSTALL.md: %USERPROFILE%\.config\opencode\opencode.json)
  $p = Join-Path $homeDir '.config\opencode\opencode.json'
  if (Test-Client 'opencode' $p) { if (Add-McpServer 'opencode' $p 'opencode' $Exe) { $wired++ } }

  # Claude Code (user scope: ~/.claude.json top-level mcpServers)
  $p = Join-Path $homeDir '.claude.json'
  if (Test-Client 'claude' $p) { if (Add-McpServer 'Claude Code' $p 'mcpServersTyped' $Exe) { $wired++ } }

  # Claude Desktop (no CLI ships with it: the config file itself is the signal)
  $p = Join-Path $appData 'Claude\claude_desktop_config.json'
  if (Test-Path -LiteralPath $p) { if (Add-McpServer 'Claude Desktop' $p 'mcpServers' $Exe) { $wired++ } }

  # Cursor (global)
  $p = Join-Path $homeDir '.cursor\mcp.json'
  if (Test-Client 'cursor' $p) { if (Add-McpServer 'Cursor' $p 'mcpServers' $Exe) { $wired++ } }

  # VS Code (user mcp.json: {"servers": ...})
  $p = Join-Path $appData 'Code\User\mcp.json'
  if (Test-Client 'code' $p) { if (Add-McpServer 'VS Code' $p 'vscode' $Exe) { $wired++ } }

  # Windsurf (probe the parent dir, like install.sh: dir-exists-but-no-file still wires)
  $p = Join-Path $homeDir '.codeium\windsurf\mcp_config.json'
  if (Test-Client 'windsurf' ([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetDirectoryName($p)))) { if (Add-McpServer 'Windsurf' $p 'mcpServers' $Exe) { $wired++ } }

  # Gemini CLI (probe the parent dir, like install.sh)
  $p = Join-Path $homeDir '.gemini\settings.json'
  if (Test-Client 'gemini' ([System.IO.Path]::GetDirectoryName($p))) { if (Add-McpServer 'Gemini CLI' $p 'mcpServers' $Exe) { $wired++ } }

  if ($wired -eq 0) {
    Write-Host 'No supported client detected. Register manually:'
    Write-Host "  `"vs-debug`": { `"type`": `"local`", `"command`": [`"$($Exe -replace '\\', '\\')`"] }"
  } else {
    Write-Host "Wired $wired client(s). Restart each client, open VS 2026, and verify: attach should return a sessionId."
  }
}

$exe = if ($Exe) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Exe) } else { Join-Path $Dest $ExeName }
if (-not $ConfigOnly) {
  if (Get-Process VsDebugMcp -ErrorAction SilentlyContinue) { throw 'Close the MCP host first - VsDebugMcp.exe is still running.' }
  $zip = "$env:TEMP\VsDebugMcp-win-x64.zip"
  Write-Host "Downloading $Base/VsDebugMcp-win-x64.zip ..."
  Invoke-WebRequest -Uri "$Base/VsDebugMcp-win-x64.zip" -OutFile $zip -UseBasicParsing
  # Windows PowerShell 5.1 may hand back bytes for .Content: decode before splitting
  $resp = Invoke-WebRequest -Uri "$Base/SHA256SUMS.txt" -UseBasicParsing
  $sums = if ($resp.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($resp.Content) } else { [string]$resp.Content }
  $line = ($sums -split "`n") | Where-Object { $_ -match '^[0-9a-fA-F]{64}\s+[* ]?VsDebugMcp-win-x64\.zip\s*$' } | Select-Object -First 1
  if (-not $line) { throw 'No VsDebugMcp-win-x64.zip entry in SHA256SUMS.txt - has a release been published yet?' }
  $expected = ($line -split '\s+')[0].ToLower()
  $actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
  if ($actual -ne $expected) { throw "Checksum mismatch - delete $zip and retry." }
  if (Test-Path -LiteralPath $Dest) { Remove-Item -LiteralPath $Dest -Recurse -Force }
  Expand-Archive -LiteralPath $zip -DestinationPath $Dest
  Remove-Item -LiteralPath $zip -Force
  & $exe --live-check   # expect usage printout (exit 2), proves the runtime loads
  if ($LASTEXITCODE -ne 2) { throw "Smoke test failed: exit $LASTEXITCODE, want 2 (usage line)." }
  Write-Host "Installed to $Dest."
}

if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "No server at $exe - run without -ConfigOnly first, or pass -Dest <folder with $ExeName>." }
Wire-Clients $exe
