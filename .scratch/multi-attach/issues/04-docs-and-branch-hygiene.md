Type: task
Status: resolved
Labels: wayfinder:task
Blocked by: 03

## Question

Update `docs/TOOLS.md` (session inventory, no more single-binding replacement rule), `GLOSSARY.md` (debug session = one of many live bindings), and `docs/TROUBLESHOOTING.md` (per-session recovery: re-attach only the dead session) on `feature/multi-attach`, and confirm the branch contains only this effort's change set.

## Answer

Docs done on `feature/multi-attach`: `TOOLS.md` (15 tools, `attach`/`list_sessions`/`detach` rows, `stale-session` row deleted, `vs-not-running` marked per-session), `GLOSSARY.md` (debug session = one of many isolated handles, released with `detach`), `TROUBLESHOOTING.md` (new per-session dead-VS section; head-of-line warning on the shared dispatcher), new ADR-0008 (accepted, supersedes ADR-0003, whose status now points at it), `README.md` (15 tools, multi-attach workflow, `stale-session` dropped from the error list). Hygiene: branch diff vs `main` is the effort set plus two pre-existing non-functional rename/whitespace hunks carried from `main`'s dirty tree (`ArgumentValidator.cs`, `Program.cs`) — untouched, not this effort's. Standing rule learned: never state test counts in `README.md`.
