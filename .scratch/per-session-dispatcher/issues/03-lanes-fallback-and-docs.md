# 03: Lane implementation plus fallback and docs

**What to build:** per-session dispatcher lanes (or the ADR-approved fair-queue alternative) with the single-lane dispatcher kept as a startup-selectable fallback, so one wedged VS degrades to per-session errors while siblings keep working.

**Blocked by:** 01 (needs the ADR design), 02 (needs the contract tests to satisfy).

**Status:** ready-for-agent

- [ ] Wedged session reports per-session `vs-busy`/`timeout`; healthy siblings keep normal latency; recovery needs no re-attach.
- [ ] Error codes, result envelopes, and safety gates unchanged; existing retry logic keeps working.
- [ ] `TROUBLESHOOTING.md` and `README.md` Known limits describe the new behavior (no test counts in `README.md`).
