<# test-installers.ps1: seam 2, both installers against a temp home (Windows, PowerShell 7).
Each run gets a fresh home holding a fake install (stub exe + the repo's skills\), an existing
config per client and an unrelated skill per client skill dir, then checks only what lands on
disk. HOME, USERPROFILE and APPDATA all point at the temp home: install.sh's Python resolves ~
through USERPROFILE, so a HOME-only override would wire the real configs.
Usage: pwsh -NoProfile -File scripts\test-installers.ps1 [-Bash <path to Git Bash bash.exe>] #>
param([string]$Bash = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Bash) { $Bash = Join-Path (Split-Path (Split-Path (Get-Command git).Source)) 'bin\bash.exe' }
$skills = Get-ChildItem -LiteralPath (Join-Path $repo 'skills') -Directory | ForEach-Object Name
# Skills go to Claude Code only (INSTALL.md); Cursor and opencode also load this directory.
$skillDir = '.claude\skills'
$failures = [System.Collections.Generic.List[string]]::new()

function Check([string]$Run, [scriptblock]$Test, [string]$What) {
  $passed = try { [bool](& $Test) } catch { $false }
  if ($passed) { Write-Host "  ok   $What" } else { Write-Host "  FAIL $What"; $failures.Add("${Run}: $What") }
}

function New-TempHome {
  $h = Join-Path ([System.IO.Path]::GetTempPath()) ("vsdbg-inst-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
  $inst = Join-Path $h 'install'
  New-Item -ItemType Directory -Force (Join-Path $h 'AppData\Roaming'), $inst | Out-Null
  Set-Content -LiteralPath (Join-Path $inst 'VsDebugMcp.exe') -Value 'stub'
  Copy-Item -LiteralPath (Join-Path $repo 'skills') -Destination $inst -Recurse
  $files = @{
    # Claude Code writes one projects key per cwd as typed: drive-letter case differs on Windows.
    '.claude.json' = '{"projects":{"C:/Users/u/repo":{"a":1},"c:/Users/u/repo":{"a":2}},"mcpServers":{"other":{"type":"stdio","command":"o","args":[]}}}'
    '.config\opencode\opencode.json' = '{"mcp":{"servers":{"other":{"type":"local","command":["o"]}}}}'
    '.cursor\mcp.json' = '{"mcpServers":{"other":{"command":"o","args":["a"]}}}'
    '.codex\config.toml' = "model = `"m`"`n`n[mcp_servers.other]`ncommand = `"o`"`nargs = []`n"
  }
  foreach ($f in $files.Keys) {
    $p = Join-Path $h $f
    New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null
    [System.IO.File]::WriteAllText($p, $files[$f])
  }
  New-Item -ItemType Directory -Force (Join-Path $h "$skillDir\my-own") | Out-Null
  Set-Content -LiteralPath (Join-Path $h "$skillDir\my-own\SKILL.md") -Value 'keep'
  return $h
}

function Invoke-WithHome([string]$TempHome, [scriptblock]$Run) {
  $saved = @{ HOME = $env:HOME; USERPROFILE = $env:USERPROFILE; APPDATA = $env:APPDATA; CODEX_HOME = $env:CODEX_HOME }
  try {
    $env:HOME = $TempHome; $env:USERPROFILE = $TempHome; $env:APPDATA = Join-Path $TempHome 'AppData\Roaming'
    $env:CODEX_HOME = Join-Path $TempHome '.codex'  # a set CODEX_HOME would otherwise wire the real Codex config
    $out = & $Run 2>&1 | Out-String
    return @{ Out = $out; Code = $LASTEXITCODE }
  } finally { foreach ($k in $saved.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) } }  # null unsets
}

function Read-Json([string]$Path) { try { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -AsHashtable } catch { $null } }

function Assert-Home([string]$Run, [string]$TempHome, [hashtable]$Result, [bool]$Wired) {
  $exe = (Join-Path $TempHome 'install\VsDebugMcp.exe') -replace '\\', '/'
  $same = { param($v) ($v -replace '\\', '/') -eq $exe }
  Check $Run { $Result.Code -eq 0 } "exit 0 (got $($Result.Code))"
  if ($Wired) {
    $c = Read-Json (Join-Path $TempHome '.claude.json')
    Check $Run { $c.projects.Keys.Count -eq 2 } 'Claude Code: case-duplicate projects keys both kept'
    Check $Run { & $same $c.mcpServers['vs-debug'].command } 'Claude Code: vs-debug wired'
    Check $Run { $c.mcpServers.other.args -is [array] -and $c.mcpServers.other.args.Count -eq 0 } 'Claude Code: other server keeps args []'
    $o = Read-Json (Join-Path $TempHome '.config\opencode\opencode.json')
    Check $Run { & $same $o.mcp.servers['vs-debug'].command[0] } 'opencode: vs-debug wired'
    Check $Run { $o.mcp.servers.other.command -is [array] -and $o.mcp.servers.other.command[0] -eq 'o' } 'opencode: other server keeps command ["o"]'
    $u = Read-Json (Join-Path $TempHome '.cursor\mcp.json')
    Check $Run { & $same $u.mcpServers['vs-debug'].command } 'Cursor: vs-debug wired'
    Check $Run { $u.mcpServers.other.args -is [array] -and $u.mcpServers.other.args[0] -eq 'a' } 'Cursor: other server keeps args ["a"]'
    $t = Get-Content -Raw -LiteralPath (Join-Path $TempHome '.codex\config.toml')
    $m = [regex]::Match($t, '(?m)^\[mcp_servers\.vs-debug\]\r?\ncommand = "((?:[^"\\]|\\.)*)"\r?\nargs = \[\]')
    Check $Run { $m.Success -and (& $same ($m.Groups[1].Value -replace '\\\\', '\')) } 'Codex: [mcp_servers.vs-debug] with the exe'
    Check $Run { ([regex]::Matches($t, '(?m)^\[mcp_servers\.vs-debug\]')).Count -eq 1 } 'Codex: one vs-debug table after a re-run'
    Check $Run { $t -like "model = `"m`"*" -and $t -match '(?m)^\[mcp_servers\.other\]\r?\ncommand = "o"\r?\nargs = \[\]' } 'Codex: rest of config.toml untouched'
  }
  $dst = Join-Path $TempHome $skillDir
  $hashes = { param($dir) Get-ChildItem -Recurse -File $dir -ErrorAction SilentlyContinue | Get-FileHash | ForEach-Object Hash }
  $landed = @($skills | Where-Object { (Test-Path (Join-Path $dst $_)) -and -not (Compare-Object @(& $hashes (Join-Path $repo "skills\$_")) @(& $hashes (Join-Path $dst $_))) })
  Check $Run { $landed.Count -eq $skills.Count } "${skillDir}: the $($skills.Count) skills landed"
  Check $Run { (Get-Content -Raw -LiteralPath (Join-Path $dst 'my-own\SKILL.md')).Trim() -eq 'keep' } "${skillDir}: my-own untouched"
}

$ps1 = Join-Path $repo 'install.ps1'
$sh = (Join-Path $repo 'install.sh') -replace '\\', '/'
# A python3/python that fails like the Microsoft Store stub ("Python was not found").
$stub = Join-Path ([System.IO.Path]::GetTempPath()) 'vsdbg-python-stub'
New-Item -ItemType Directory -Force $stub | Out-Null
foreach ($n in 'python3', 'python') { [System.IO.File]::WriteAllText((Join-Path $stub $n), "#!/bin/sh`necho 'Python was not found' >&2`nexit 9009`n") }

$runs = @(
  @{ Name = 'install.ps1 (pwsh 7)'; Wired = $true; Run = { param($h) pwsh -NoProfile -File $ps1 -ConfigOnly -Dest "$h\install" } }
  @{ Name = 'install.ps1 (Windows PowerShell 5.1)'; Wired = $true; Run = { param($h) powershell -NoProfile -ExecutionPolicy Bypass -File $ps1 -ConfigOnly -Dest "$h\install" } }
  @{ Name = 'install.sh'; Wired = $true; Run = { param($h) & $Bash $sh --config-only --dest "$h/install" } }
  @{ Name = 'install.sh (Store python stub only)'; Wired = $false; Run = { param($h) & $Bash -c "PATH='$(($stub -replace '\\','/') -replace '^([A-Za-z]):', '/$1')':`$PATH '$sh' --config-only --dest '$($h -replace '\\','/')/install'" } }
)
foreach ($r in $runs) {
  Write-Host $r.Name
  $h = New-TempHome
  try {
    $installer = $r.Run
    # Twice: a re-run must replace, never duplicate (spec story 4).
    $null = Invoke-WithHome $h { & $installer $h }
    $res = Invoke-WithHome $h { & $installer $h }
    if ($res.Code -ne 0) { Write-Host $res.Out }
    Assert-Home $r.Name $h $res $r.Wired
  } finally { Remove-Item -LiteralPath $h -Recurse -Force -ErrorAction SilentlyContinue }
}
Remove-Item -LiteralPath $stub -Recurse -Force -ErrorAction SilentlyContinue

if ($failures.Count) { Write-Host "`n$($failures.Count) failure(s)"; $failures | ForEach-Object { Write-Host "  $_" }; exit 1 }
Write-Host "`nall passed"
