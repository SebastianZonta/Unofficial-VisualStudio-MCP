# 03: Extract the execution driver

**What to build:** Drive execution control for a debug session from one module behind the bridge seam, so pause, resume with budget, and step directions report the same debugger mode and last-hit as today, and mode-gated calls fail with guidance instead of raw automation behavior.

**Blocked by:** 01 (needs the session entry the registry owns; independent of 02).

**Status:** ready-for-agent

- [ ] Pause reports break mode with the current last-hit
- [ ] Resume runs the target process with a time budget and reports timeout distinctly from a stop
- [ ] Step over/into/out moves only its own debug session's target process
- [ ] Mode-gated calls fail with a clear error and remediation
- [ ] Build plus existing unit suite stays green
