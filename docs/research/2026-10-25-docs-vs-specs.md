# Docs vs specs audit, 2026-10-25

Question: given all specs in `.scratch/`, are the docs up to date?
Convention note: prior research notes live per-feature in `.scratch/<slug>/research/`
(e.g. `.scratch/vs-debug-mcp/research/`) and `.scratch/e2e-runs/*-research.md`; no
`docs/research/` dir existed, so this cross-cutting report creates it per the task fallback.

## Summary verdict: NO — docs are behaviorally complete, trackers are stale

- All 21 shipped tools' contracts are documented: code has 21 `McpServerTool`
  (`src/VsDebugMcp/SessionTools.cs:9-69`, `BreakpointTools.cs:11-54`,
  `ExecutionTools.cs:11-79`, `InspectionTools.cs:11-59`); `docs/TOOLS.md` and
  root `README.md:26-50` list the same 21. No shipped tool is undocumented.
- The 12 `done`/`resolved` feature specs' user-facing behavior is covered in
  `docs/TOOLS.md`, `docs/TROUBLESHOOTING.md`, `docs/agents/e2e.md`,
  `GLOSSARY.md`, `INSTALL.md`, and `docs/adr/0001-0012` (index `docs/adr/README.md:1-18`).
- 13 discrete stale/conflict items found (S1–S13 below): 6 stale `Status:` lines /
  index rows, 1 vocabulary violation, 3 doc-text conflicts, 3 minor doc gaps.
  Estimated behavioral coverage of shipped surface: 100%; index/tracker accuracy: no.

## Per-spec table

| Spec dir | Spec `Status:` | Doc coverage | Gap |
|---|---|---|---|
| vs-debug-mcp | `done` (spec.md:1) + 12/12 issues resolved | Full: TOOLS (21 tools), TROUBLESHOOTING, ADRs 0001–0008, e2e.md, GLOSSARY | none |
| audit-must-fix | `done` (spec.md:2) | Full: stable `bp<N>` (TOOLS:25,28; GLOSSARY:76-84), read-only `path` (TOOLS:48,50), frame-true (TOOLS:48), `nse` (TOOLS:50; ADR-0010), dispatcher drop (TROUBLESHOOTING:65-66) | none |
| audit-oct24-high | `done` (spec.md:2) | Full: dispatcher snapshots (README:87-90), devenv guard (TROUBLESHOOTING:85-87), run-to-cursor join (TROUBLESHOOTING:102-106; TOOLS:36), drill paths (TOOLS:48), code channel X1 (e2e.md:216) | minor: TOOLS never names generic-arity rejection (S12) or devenv-PID `invalid-argument` (S13) |
| audit-should-fix | `done` (spec.md:2) | Full: live-by-default/`--fake` (README:80-83; INSTALL:41), atomic attach (TOOLS:13), evaluate budget (e2e slice map F row, e2e.md:137), `unknown` mode (GLOSSARY:58-59), `vs-busy` retry (TOOLS:73; TROUBLESHOOTING:57-66), one-target rule (TOOLS:81; e2e.md:164-165), Sandbox+E2E (e2e.md:167-187) | none |
| conditional-breakpoints | `done` (spec.md:3), 3/3 resolved | Full: TOOLS:25 (`condition`/`hitCount`/`logMessage`); GLOSSARY:64-74 | none |
| launch-process | `done` (spec.md:3), 2/2 resolved | Full: TOOLS:19; ADR-0009; TROUBLESHOOTING stale-publish 40-53; e2e E1–E14 (e2e.md:335-361) | S9 (publish path docs inconsistent, shared cause) |
| list-instances | `done` (spec.md:1) | Full: TOOLS:16; e2e D1 (e2e.md:212) | S10 (spec tiebreak line superseded by code) |
| list-processes | `done` (spec.md:1) | Full: TOOLS:17; TROUBLESHOOTING:77-87; e2e D3–D4 (e2e.md:212-216) | none |
| module-symbol-status | `done` (spec.md:3), 2/2 resolved | Covered: TOOLS:27; TROUBLESHOOTING:89-101 | S8 (GLOSSARY `failed` vs live code/TOOLS) |
| run-to-cursor | `resolved` (spec.md:3), 2/2 resolved | Full: TOOLS:36; GLOSSARY:86-93; TROUBLESHOOTING:98-106; e2e E16–E18,E27 (e2e.md:342-345) | none (S12-adjacent wording only) |
| set-variable | `done` (spec.md:3), 2/2 resolved | Full: TOOLS:49; ADR-0012; TROUBLESHOOTING:146-157; GLOSSARY consent 50-55 | none |
| multi-attach | map only, 4/4 resolved | Full: ADR-0008 (adr/README:14); TOOLS:13-15; TROUBLESHOOTING:68-73; README:21 | none; lanes/labels/follow-mode correctly absent (deferred, tool-slices/issues/03-session-launch.md:38-40) |
| exception-config | `ready-for-agent` (spec.md:3), 2/2 issues resolved | Full: TOOLS:38-39,41; TROUBLESHOOTING:129-144; README:45-46; ADR-0011; GLOSSARY:103-109 | S4 (spec `Status:` stale, docs ahead) |
| tool-slices | no `Status:` line in spec.md; 5/5 issues `resolved` on disk | Covered: slice map e2e.md:125-143; GLOSSARY runner/slice/run-tag 116-137; `DocsContractTests` (tests/VsDebugMcp.Tests/DocsContractTests.cs:7) | S1 (index row stale) |
| workflow-hardening | no `Status:` in spec.md; 2/2 issues `ready-for-agent` | Landed: scripts/publish-fresh.ps1:1; definition-of-done.md:1-7; issue-tracker.md:11-15; triage-labels deleted (no hits in docs/); CI gate .github/workflows/ci.yml:37-43; build.md:16-17 | S5 (issue statuses stale); S9 (INSTALL/README still raw publish) |
| audit-oct24-medium | `ready-for-agent` (spec.md:2) | Correctly absent: E29–E31/T6/I19/W11/E25b not in e2e.md (grep: no hits); `Failed` still in code (ModuleModels.cs:8) and GLOSSARY | S8 related; no doc action until implemented |
| e2e-2026-10-23 | no spec (findings); 3 resolved + 1 needs-info — matches disk | Covered: side-effect refusal (TROUBLESHOOTING:110-117), stale-publish (TROUBLESHOOTING:40-53), two-VS (e2e P10) | none |
| e2e-2026-10-24 | no spec; disk has 12 issues, index lists 11 | Covered per-row (02→TOOLS:25,90; 06→TROUBLESHOOTING:85-87; 08→TOOLS:13; 11→TOOLS:73; 03→TROUBLESHOOTING:40-53; 10→e2e.md:265 canary; 12→TROUBLESHOOTING:102-106 + e2e.md:345 E27) | S2 (index missing #12, counts sum to 11); S3 (`open` vocabulary); S6 (#04 status stale, fix landed) |
| Sandbox/e2e markers | n/a | e2e.md:175-187 marker table covers all 12 `// E2E:` markers in Sandbox/Scenarios.cs:13-134; S1 asserts "12 markers found" (e2e.md:200) | none |

## Stale / conflict list (with sources)

- S1: `.scratch/README.md` tool-slices row says "1 resolved, 4 ready-for-agent";
  disk shows all five `Status: resolved`
  (`.scratch/tool-slices/issues/02-platform.md:3`, `03-session-launch.md:3`,
  `04-tool-slices.md:3`, `05-contracts.md:3`, plus `01-hygiene.md:3`).
- S2: `.scratch/README.md` e2e-2026-10-24 row counts "8 resolved, 1 ready-for-agent,
  1 needs-triage, 1 needs-info" (= 11) and lists issues 01–11, but disk has a 12th:
  `.scratch/e2e-2026-10-24/issues/12-run-to-cursor-concurrent-resume-race.md` (exists, unlisted).
- S3: Issue 12 line 19 `Status: open` — `open` is not in the vocabulary
  (`docs/agents/issue-tracker.md:11-15`: needs-triage/needs-info/ready-for-agent/
  ready-for-human/wontfix + claimed/resolved).
- S4: `.scratch/exception-config/spec.md:3` says `ready-for-agent` while both issues are
  resolved and the feature is fully documented (TOOLS:38-39, ADR-0011, TROUBLESHOOTING:137-144).
- S5: `.scratch/workflow-hardening/issues/01-publish-script-and-ci-gate.md:3` and
  `02-definition-of-done-and-tracker.md:3` say `ready-for-agent`, but every deliverable
  is landed (scripts/publish-fresh.ps1, docs/agents/definition-of-done.md,
  docs/agents/issue-tracker.md:11-15, .github/workflows/ci.yml:37-43 with fetch-depth 0 at ci.yml:13).
- S6: `.scratch/e2e-2026-10-24/issues/04-attach-default-binds-oldest-instance.md:25`
  says `ready-for-agent`, but the fix is in code
  (`src/VsDebugMcp/VsDiscovery.cs:17-20`, pid-descending = newest), docs promise newest
  (`docs/TOOLS.md:13`, `docs/agents/e2e.md:385`), and the issue's own checkboxes (lines 27-32)
  record code + rerun done.
- S7: `docs/README.md:18` says "the eleven ADRs"; `docs/adr/` holds 0001–0012 (12 files,
  all indexed in `docs/adr/README.md:7-18`).
- S8 (conflict): `GLOSSARY.md:95-100` documents `failed` as a debugger-reported
  `symbolState`, but the live bridge never emits it
  (`src/VsDebugMcp/DteDebugBridge.cs:634-640` emits only `Loaded`/`Missing`/`Unknown`)
  and `docs/TOOLS.md:27` says "Live VS reports only `loaded`/`missing`". `failed` survives
  only as `SymbolStates.Failed` (`src/VsDebugMcp/ModuleModels.cs:8`); removal is scoped to
  the open audit-oct24-medium spec (spec.md:78).
- S9 (inconsistency): `INSTALL.md:11-13,53` and `README.md:70-73` prescribe raw
  `dotnet publish ... -o standalone-vNext` / `-o src/VsDebugMcp/bin/Fixed` with no pointer
  to `scripts/publish-fresh.ps1`, contradicting the single-publish-path rule
  (`docs/agents/definition-of-done.md:3`, `docs/agents/build.md:16`).
- S10 (historical): `.scratch/list-instances/spec.md:36` says same-version tiebreak is
  "display-name order"; code is pid-descending (`src/VsDebugMcp/VsDiscovery.cs:19`) since
  the issue-04 fix. Spec is `done`/frozen; note only.
- S11: `run-to-cursor/spec.md:3` uses `Status: resolved` where sibling specs use `done`;
  harmless (tool-slices spec itself blesses `resolved` = done work, spec.md:63).
- S12 (minor gap): e2e-2026-10-24 issue 09 (generic-arity gate hole, resolved) has no
  explicit mention in `docs/TOOLS.md:50` (`evaluate` row lists other rejections).
- S13 (minor gap): e2e-2026-10-24 issue 06 (devenv PID refused) is covered in
  `docs/TROUBLESHOOTING.md:85-87` but the `attach_process` row (`docs/TOOLS.md:18`)
  never names the `invalid-argument` refusal.
- Unverified (not a gap claim): exception-config story 7 (running change names the mode)
  vs `docs/TOOLS.md:86` (`already-running` row) — row does not state the mode is named;
  needs a code spot-check before calling it a docs gap.

## Prioritized fix list

1. P0 — `.scratch/README.md`: tool-slices row → "5 resolved"; e2e-2026-10-24 row → add
   issue 12, recount to 12 (S1, S2).
2. P0 — stale `Status:` lines: exception-config spec → `done` (S4); workflow-hardening
   issues 01/02 → `resolved` (S5); e2e-2026-10-24 issue 04 → `resolved` (S6);
   issue 12 `open` → `needs-triage` or `resolved` after an E27 clean pass (S3;Flips gated on `docs/agents/e2e.md:345` E27).
3. P1 — `docs/README.md:18`: "eleven ADRs" → "twelve" (S7).
4. P1 — reconcile S8: either drop `failed` from `GLOSSARY.md:95-100` (matching TOOLS + live
   code) or mark it fake-only/legacy; pairs with audit-oct24-medium `ListModulesTests.Live_shape_only` (spec.md:77).
5. P1 — `INSTALL.md` + root `README.md` Develop: point publish steps at
   `scripts/publish-fresh.ps1` per `docs/agents/build.md:16` (S9).
6. P2 — `docs/TOOLS.md`: one clause each for generic-arity rejection in `evaluate` (S12)
   and devenv-PID `invalid-argument` in `attach_process` (S13).
7. P2 — spot-check S-unverified (mode named in `already-running` error), then close or file.
8. No action — audit-oct24-medium: docs correctly omit unimplemented behavior; implement
   spec first, then document per definition-of-done.md:4.

Skipped: re-reading full bodies of all 12 done specs (headers + `Status:` + prior doc
cross-checks sufficed); executing tests or live VS passes; source edits (report only).
Risk: S-unverified and S12/S13 rest on doc-text reads, not code-path traces — a
`DteDebugBridge`/`ArgumentValidator` grep would confirm before editing TOOLS.
