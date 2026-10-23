# 02: Live launch plus tool and docs

**What to build:** the live bridge starts the target process under the VS debugger from birth and a `launch_process` tool exposes it, so entry-point breakpoints bind at startup with no external shell.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** ready-for-agent

- [ ] Breakpoints set before launch hit on startup; live verify covers args, working directory, and env passthrough.
- [ ] Same-user gate applies; launching while bound is an explicit error (no implicit kill).
- [ ] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the tool (no test counts in `README.md`).
