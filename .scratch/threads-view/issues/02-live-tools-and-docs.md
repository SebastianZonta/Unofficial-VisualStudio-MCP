# 02: Live EnvDTE threads plus tools and docs

**What to build:** the live bridge maps EnvDTE threads and `list_threads`, `freeze_thread`, and `thaw_thread` tools expose them, verified manually on a multithreaded target.

**Blocked by:** 01 (needs the seam contract to implement).

**Status:** ready-for-agent

- [ ] Freeze-all-but-one then step shows per-thread stacks differing; default thread is the hitting thread.
- [ ] Freeze is scheduling suppression only: no detach, terminate, or cross-session effects.
- [ ] `TOOLS.md`, `GLOSSARY.md`, and `README.md` document the tools (no test counts in `README.md`).
