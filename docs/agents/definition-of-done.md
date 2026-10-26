# Definition of done (after a feature)

1. Publish via `scripts/publish-fresh.ps1` to a fresh empty dir; paste its `MCP binary:` line into the report.
2. `DocsContractTests` green (tool rows, error-code rows, counts). Changed behaviour gets an e2e case in the `docs/agents/e2e.md` phase files.
3. Coverage statement on the fix: UT (`FakeDebugBridge`) vs `AuditE2ETests` vs manual pass — add the missing case, or why neither fits. Bridge diffs need an `AuditE2ETests` case + live run (CI enforces the presence gate).
4. Spec deviation gets an ADR in `docs/adr/` with a pointer from the spec line; new failure mode gets a `docs/TROUBLESHOOTING.md` entry.
5. If agent-facing facts changed, update `AGENTS.md` — one line, no restating docs.
6. After pushing, fan out a subagent to babysit the Actions run for the pushed SHA until it concludes (`gh run list --commit <sha>`, then `gh run watch <id>`); done means every job green on that SHA. A red job comes back with the failed step's logs (`gh run view <id> --log-failed`) plus a triage: fix-forward commit or a filed issue.
