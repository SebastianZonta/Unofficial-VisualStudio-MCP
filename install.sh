#!/usr/bin/env bash
# install.sh: one-shot VsDebugMcp install.
# Downloads the latest GitHub Release zip, verifies its checksum, extracts it,
# smoke-tests the exe, then detects installed agentic tools and registers the
# server in each one's config so it connects out of the box.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/SebastianZonta/Unofficial-VisualStudio-MCP/main/install.sh | bash
#   # or, from a clone:
#   ./install.sh [--dest DIR] [--exe PATH] [--config-only]
#
# The server itself is win-x64-only: it must run on the same Windows box as
# VS 2026, as the same user. Under Git Bash/WSL this script installs and wires
# fully; on Linux/macOS it still wires client configs but skips the exe smoke
# test with a warning. Close the MCP host first: it holds VsDebugMcp.exe locked.
set -euo pipefail

REPO='SebastianZonta/Unofficial-VisualStudio-MCP'
BASE="https://github.com/$REPO/releases/latest/download"
EXE_NAME='VsDebugMcp.exe'
DEST=''; EXE=''; CONFIG_ONLY=0; DEST_GIVEN=0
if [ -z "${HOME:-}" ]; then
  echo 'HOME is not set — refusing to run (cannot resolve install paths).' >&2; exit 1
fi

usage() {
  echo 'usage: install.sh [--dest DIR] [--exe PATH] [--config-only]'
  echo '  --dest DIR     install folder (default /c/Tools/VsDebugMcp on Windows, ~/.local/share/vs-debug-mcp elsewhere)'
  echo '  --exe PATH     server exe to wire (default $DEST/VsDebugMcp.exe; an explicit relative --exe resolves against cwd, not DEST)'
  echo '  --config-only  skip download: only (re)wire client configs'
}

while [ $# -gt 0 ]; do
  case "$1" in
    --dest|--exe)
      if [ $# -lt 2 ]; then usage >&2; exit 1; fi
      if [ "$1" = --dest ]; then DEST="$2"; DEST_GIVEN=1; else EXE="$2"; fi
      shift 2 ;;
    --config-only) CONFIG_ONLY=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "unknown flag: $1" >&2; usage >&2; exit 1 ;;
  esac
done

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) IS_WIN=1 ;;
  *) IS_WIN=0 ;;
esac
# Single canonicalization pipeline (was 3 copies): ~ expand, absolutize
# against cwd, realpath -m, cygpath -m. DEST, HOME and --exe all flow through
# it so the HOME comparison cannot drift (Git Bash: $HOME=/c/... vs C:/...).
resolve_path() {
  _p="$1"
  case "$_p" in '~/'*) _p="$HOME/${_p#'~/'}" ;; '~') _p="$HOME" ;; esac
  case "$_p" in /*|?:/*|?:\\*|\\\\*) ;; *) _p="$(pwd)/$_p" ;; esac
  if command -v realpath >/dev/null 2>&1; then
    _r="$(realpath -m "$_p" 2>/dev/null || true)"
    if [ -n "$_r" ]; then _p="$_r"; fi
    unset _r
  fi
  if command -v cygpath >/dev/null 2>&1; then
    _c="$(cygpath -m "$_p" 2>/dev/null || true)"
    if [ -n "$_c" ]; then _p="$_c"; fi
    unset _c
  fi
  printf '%s' "$_p"
}
if [ "$DEST_GIVEN" -eq 1 ]; then
  case "$DEST" in [A-Za-z]:) echo "refusing to install into '$DEST'" >&2; exit 1 ;; esac  # bare drive letter (cwd-on-drive): refuse before resolve turns it into cwd
  if [ -z "$(printf '%s' "$DEST" | tr -d '[:space:]')" ]; then
    echo "refusing to install into '$DEST'" >&2; exit 1
  fi
  case "$DEST" in //|//[^/]*|//[^/]*/[^/]*) echo "refusing to install into '$DEST'" >&2; exit 1 ;; esac  # UNC roots (realpath collapses //, so check before resolve; deeper paths allowed)
fi
if [ -z "$DEST" ]; then
  if [ "$IS_WIN" -eq 1 ]; then DEST='/c/Tools/VsDebugMcp'; else DEST="$HOME/.local/share/vs-debug-mcp"; fi
fi
EXE_GIVEN=1; if [ -z "$EXE" ]; then EXE_GIVEN=0; fi
# Client configs store the command verbatim: absolutize against cwd so the
# wired entry works no matter where the client starts. On Git Bash/MSYS also
# normalize to mixed C:/ form (cygpath -m) so native Win32 hosts can spawn it.
DEST="$(resolve_path "$DEST")"
_norm="$(printf '%s' "$DEST" | tr '\\' '/')"
while [ "${_norm%/}" != "$_norm" ]; do _norm="${_norm%/}"; done
case "$_norm" in ''|'/'|[A-Za-z]:|/[A-Za-z]) echo "refusing to install into '$DEST'" >&2; exit 1 ;; esac
case "$_norm" in //[^/]*|//[^/]*/[^/]*) echo "refusing to install into '$DEST'" >&2; exit 1 ;; esac  # UNC roots in C:/ form (ps1 parity)
unset _norm
case "$DEST" in /|?:/|?:\\*) ;; *) DEST="${DEST%/}"; DEST="${DEST%\\}" ;; esac  # no trailing sep (realpath already did it when available)
_hd="$(resolve_path "$HOME")"; _hd="${_hd%/}"
_dd="$DEST"; while [ "${_dd%/}" != "$_dd" ]; do _dd="${_dd%/}"; done; while [ "${_dd%\\}" != "$_dd" ]; do _dd="${_dd%\\}"; done
if [ -n "$_hd" ] && [ "$_dd" = "$_hd" ]; then echo "refusing to install into '$DEST'" >&2; exit 1; fi  # never wipe $HOME itself (subfolders are fine)
case "$_dd" in /mnt/?) echo "refusing to install into '$DEST'" >&2; exit 1 ;; esac  # WSL drive roots (/mnt/c)
unset _hd _dd
if [ "$EXE_GIVEN" -eq 0 ]; then
  EXE="$DEST/$EXE_NAME"  # explicit relative --exe resolves against cwd, not DEST (see --help)
else
  EXE="$(resolve_path "$EXE")"
fi

wire_clients() {
  local exe="$1"
  if ! command -v python3 >/dev/null; then
    echo 'python3 not found: skipping auto-wire. Register manually:'
    echo "  \"vs-debug\": { \"type\": \"local\", \"command\": [\"$exe\"] }"
    return
  fi
  echo 'Detecting agentic tools...'
  TARGET_EXE="$exe" python3 - <<'EOF'
import json, os, shutil

exe = os.environ['TARGET_EXE']
home = os.path.expanduser('~')
def appdata():
    a = os.environ.get('APPDATA', '')
    return a if a else os.path.join(home, 'AppData', 'Roaming')

def upsert(parent, key, sub):
    # Merge into parent[key]['vs-debug'], preserving user customizations
    # (env, cwd, ...): only our keys are set. A non-object intermediate node
    # is a skip, not a wipe (root parity): raise so the file is left untouched.
    d = parent.get(key)
    if key in parent and not isinstance(d, dict):
        raise TypeError(f"'{key}' is not a JSON object; left untouched")
    if not isinstance(d, dict):
        d = parent[key] = {}
    entry = dict(d.get('vs-debug')) if isinstance(d.get('vs-debug'), dict) else {}
    entry.update(sub)
    d['vs-debug'] = entry
    return parent

def opencode(cfg):
    if 'mcp' in cfg and not isinstance(cfg.get('mcp'), dict):
        raise TypeError("'mcp' is not a JSON object; left untouched")
    if not isinstance(cfg.get('mcp'), dict):
        cfg['mcp'] = {}
    mcp = cfg['mcp']
    if 'servers' in mcp or len(mcp) == 0:  # INSTALL.md shape (also used for fresh files)
        upsert(mcp, 'servers', {'type': 'local', 'command': [exe]})
    else:  # new-style top-level mcp entries: keep the existing shape
        entry = dict(mcp.get('vs-debug')) if isinstance(mcp.get('vs-debug'), dict) else {}
        entry.update({'type': 'local', 'command': [exe]})
        mcp['vs-debug'] = entry
    return cfg

def mcp_servers(typed=False):
    def merge(cfg):
        sub = {'command': exe, 'args': []}
        if typed:
            sub['type'] = 'stdio'
        return upsert(cfg, 'mcpServers', sub)
    return merge

def vscode(cfg):  # user mcp.json uses {"servers": ...}
    return upsert(cfg, 'servers', {'type': 'stdio', 'command': exe, 'args': []})

# (label, config path, merge fn, detection: CLI on PATH or path exists)
targets = [
    ('opencode',       os.path.join(home, '.config', 'opencode', 'opencode.json'), opencode,           ('opencode', None)),
    ('Claude Code',    os.path.join(home, '.claude.json'),                             mcp_servers(True), ('claude', os.path.join(home, '.claude.json'))),
    ('Claude Desktop', os.path.join(appdata(), 'Claude', 'claude_desktop_config.json'), mcp_servers(), ('claude-desktop', None)),
    ('Cursor',         os.path.join(home, '.cursor', 'mcp.json'),                       mcp_servers(), ('cursor', os.path.join(home, '.cursor', 'mcp.json'))),
    ('VS Code',        os.path.join(appdata(), 'Code', 'User', 'mcp.json'),             vscode,        ('code', None)),
    ('Windsurf',       os.path.join(home, '.codeium', 'windsurf', 'mcp_config.json'),   mcp_servers(), ('windsurf', os.path.join(home, '.codeium'))),
    ('Gemini CLI',     os.path.join(home, '.gemini', 'settings.json'),                 mcp_servers(), ('gemini', os.path.join(home, '.gemini'))),
]

wired = 0
for label, path, merge, (cli, probe) in targets:
    detected = shutil.which(cli) or (probe and os.path.exists(probe)) or os.path.exists(path)
    if not detected:
        continue
    try:
        cfg = {}
        if os.path.exists(path):
            with open(path, encoding='utf-8-sig') as f:  # -sig: BOM-prefixed JSON (Windows editors) parses instead of skipping
                raw = f.read()
            if raw.strip() == '':
                cfg = {}  # empty file wires as a fresh object (matches install.ps1)
            else:
                cfg = json.loads(raw)
            if not isinstance(cfg, dict):
                print(f'  [skip] {label}: root of {path} is not a JSON object; left untouched')
                continue
        cfg = merge(cfg)  # merge first: the file is only rewritten on success
        if os.path.exists(path) and not os.path.exists(path + '.bak'):
            shutil.copyfile(path, path + '.bak')  # keep the pre-installer original
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(cfg, f, indent=2)
            f.write('\n')
        print(f'  [wired] {label} ({path})')
        wired += 1
    except (json.JSONDecodeError, OSError, UnicodeDecodeError, TypeError, AttributeError, ValueError) as e:
        print(f'  [skip] {label}: cannot merge {path} ({e})')

if wired == 0:
    print('No supported client detected. Register manually:')
    print(f'  "vs-debug": {{ "type": "local", "command": ["{exe}"] }}')
else:
    print(f'Wired {wired} client(s). Restart each client, open VS 2026, and verify: attach should return a sessionId.')
EOF
}

if [ "$CONFIG_ONLY" -eq 0 ]; then
  for cmd in curl unzip mktemp; do
    command -v "$cmd" >/dev/null || { echo "missing required tool: $cmd" >&2; exit 1; }
  done
  if command -v sha256sum >/dev/null; then SHA256='sha256sum -c';
  elif command -v shasum >/dev/null; then SHA256='shasum -a 256 -c';
  else echo 'missing required tool: sha256sum or shasum' >&2; exit 1; fi  # macOS ships shasum, not sha256sum
  if [ "$IS_WIN" -eq 1 ] && tasklist //FI "IMAGENAME eq VsDebugMcp.exe" 2>/dev/null | grep -qi VsDebugMcp; then
    echo 'Close the MCP host first — VsDebugMcp.exe is still running.' >&2; exit 1
  fi
  if [ "$IS_WIN" -eq 0 ]; then
    echo 'warning: non-Windows box — cannot check whether VsDebugMcp.exe is running; close the MCP host first.' >&2
  fi
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
  echo "Downloading $BASE/VsDebugMcp-win-x64.zip ..."
  curl -fsSL "$BASE/VsDebugMcp-win-x64.zip" -o "$tmp/VsDebugMcp-win-x64.zip"
  curl -fsSL "$BASE/SHA256SUMS.txt" -o "$tmp/sums.txt"
  tr -d '\r' < "$tmp/sums.txt" > "$tmp/sums.lf" && mv "$tmp/sums.lf" "$tmp/sums.txt"  # defense: tolerate CRLF sums; the release writes LF-only
  line="$(grep -F 'VsDebugMcp-win-x64.zip' "$tmp/sums.txt" | grep -E '^[0-9a-fA-F]{64}[[:space:]]+[* ]?VsDebugMcp-win-x64\.zip[[:space:]]*$' | head -n 1 || true)"
  if [ -z "$line" ]; then echo 'No VsDebugMcp-win-x64.zip entry in SHA256SUMS.txt — has a release been published yet?' >&2; exit 1; fi
  ( cd "$tmp" && $SHA256 <(printf '%s\n' "$line") )
  rm -rf "$DEST"; mkdir -p "$DEST"
  unzip -q -o "$tmp/VsDebugMcp-win-x64.zip" -d "$DEST"
  trap - EXIT; rm -rf "$tmp"
  if [ "$IS_WIN" -eq 1 ]; then
    code=0; "$EXE" --live-check || code=$?  # expect usage printout (exit 2), proves the runtime loads
    if [ "$code" -ne 2 ]; then echo "smoke test failed: exit $code, want 2 (usage line)" >&2; exit 1; fi
  else
    echo 'warning: non-Windows box — skipping --live-check (server needs Windows + VS 2026).' >&2
  fi
  echo "Installed to $DEST."
fi

if [ ! -f "$EXE" ]; then
  echo "No server at $EXE — run without --config-only first, or pass --dest <folder with $EXE_NAME>." >&2; exit 1
fi
wire_clients "$EXE"
