# AGENTS.md

Agent entry point — index only. Canonical detail lives in the linked file; do not restate it here.

- Build, test, publish: [docs/agents/build.md](docs/agents/build.md) (slnx, .NET 10, Fake tests, VS resolution, `publish-fresh.ps1`).
- Install, register in a client: [INSTALL.md](INSTALL.md) (one-liner vs manual A/B; Windows + VS 2026 only).
- Architecture: [docs/agents/architecture.md](docs/agents/architecture.md) (`IDebugBridge` seam, live vs fake, STA dispatcher, process helpers).
- Live VS verification: [docs/agents/live-verify.md](docs/agents/live-verify.md) (`--live-check*` harnesses, per-tool verify lines).
- Vocabulary: use `GLOSSARY.md` terms (VS instance vs debug session vs target process, mine/consent, mode/lastHit). Surface ADR conflicts explicitly.
- Specs/issues: `.scratch/` (one dir per feature, `spec.md` + `issues/NN-<slug>.md` with `Status:` line); see [docs/agents/issue-tracker.md](docs/agents/issue-tracker.md). Commits for spec work use conventional commits (`feat:`, `fix:`, `docs:`, …).
- Docs index: [docs/README.md](docs/README.md).
- E2E: after a feature, run the real-VS pass in [docs/agents/e2e.md](docs/agents/e2e.md) (R1/R2/R3 fanout, each its own VS instances; bar: every case PASS).
- `Sandbox/` is the E2E debug target: outside the solution, its own `Directory.Build.props` (no analyzers), breakpoint lines found by `// E2E:<NAME>` markers. When you change a scenario, update the e2e.md marker table too.
- After a feature: follow [docs/agents/definition-of-done.md](docs/agents/definition-of-done.md). Review standards: `CODING_STANDARDS.md`.
- After a feature, update this file if it changed agent-facing facts — one line, no restating docs.
