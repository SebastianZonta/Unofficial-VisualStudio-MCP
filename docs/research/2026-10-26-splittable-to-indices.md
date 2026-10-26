# Splittable-to-index candidates (in-scope docs/ADRs only)

Date: 2026-10-26. Scope excludes `docs/research/*` and `.scratch/**/*`.
Method: read every in-scope file; line counts measured directly.

## Inventory

| File | Lines | Topics (by `##` headers) | Splittable? |
|---|---|---|---|
| `docs/agents/e2e.md` | 460 | 12: Automated suite (L21), Roles (L40), Parallel run (L88), Runner rules (L145), Sandbox reference (L167), P0–P11 phases (L196–403), H human-assisted (L405), Audit medium (L421), Report (L436) + intro/contents (L1–19) | **YES — #1** |
| `docs/publishing-options.md` | 327 | 7: Options glance (L15), a. GitHub Releases (L32), b. .NET tool (L58), c. Registry+MCPB (L90), d. Winget/share (L129), Ruled out (L146), Recommended implementation 5 subsections (L159–320) | **YES — #2** |
| `docs/TROUBLESHOOTING.md` | 161 | 4 `##` areas + 14 `###` entries: Setup (L11, 4 entries), VS state (L59, 2), Targets/breakpoints (L79, 3), Evaluation (L112, 5); symptom index L6–9 | **Marginal — #3** |
| `GLOSSARY.md` | 145 | 18 flat terms; last 4 are E2E-scoped: Runner (L116), Slice (L124), Run tag (L134), VS PID vs target PID (L140) | No |
| `INSTALL.md` | 115 | 3 paths: one-liner (L16), Option A (L63), Option B (L102), Debug notes (L109); situation table L10 | No (optional micro-move) |
| `README.md` | 109 | What/tools/requirements/develop/architecture/limits; 21-row tool table L26–51 | No (delink, don't split) |
| `docs/TOOLS.md` | 100 | 6: Session (L9), Breakpoints (L21), Execution (L30), Inspection (L43), Envelope (L52), Error catalog (L65, ~16 codes); Contents index L7 | No |
| `docs/agents/domain.md` | 51 | Generic skill guidance (single/multi-context trees L13–39); mostly upstream template, not repo-specific | No |
| `docs/agents/issue-tracker.md` | 35 | Conventions (L5) + wayfinding ops (L26); 2 audiences (normal vs `/wayfinder`) | No (too short) |
| `CODING_STANDARDS.md` | 32 | 3: Bridge parity (L6), Shared rules (L23), Spec deviations (L29) | No |
| `docs/README.md` | 31 | Already an index (Using/Reference/Decisions/Agent workflow/Work tracking) | No (is the pattern to copy) |
| `docs/agents/build.md` | 23 | Build/test + Publish (L16) + Release (L21) | No |
| `docs/adr/README.md` | 18 | Already an index (12 rows; body L3 says historical, L5–18 table) | No (extend pattern, don't split) |
| `AGENTS.md` | 15 | Already an index (L5–14 link list) | No |
| `docs/adr/0010-*.md` | 59 | Single decision: Context (L5) / Decision (L22) / Consequences (L45) | No |
| `docs/adr/0011-*.md` | 54 | Single decision: Context (L5) / Decision (L18) / Consequences (L42) | No |
| `docs/adr/0012-*.md` | 46 | Single decision: Context (L5) / Decision (L18) / Consequences (L39) | No |
| `docs/adr/0009-*.md` | 42 | Single decision: Context (L5) / Decision (L14) / Consequences (L35) | No |
| `docs/adr/0008-*.md` | 29 | Single decision: Context (L5) / Decision (L12) / Consequences (L24) | No |
| `docs/agents/live-verify.md` | 13 | 8 bullets, one per harness/area (L5–13) | No |
| `docs/agents/architecture.md` | 7 | 5 bullets, one seam | No |
| `docs/agents/definition-of-done.md` | 8 | 6 numbered steps | No |
| `CLAUDE.md` | 7 | Pointer to `AGENTS.md` (L5–7) | No |
| `docs/adr/0001–0007` | 19–24 each | Single decision each (same Context/Decision/Consequences shape as 0008–0012 above) | No |

## Proposal 1 (rank #1): `docs/agents/e2e.md` → index + 4 phase files

Why: 460 lines (largest in scope), 3 audiences in one file (orchestrator Roles L40–86, runners Parallel/Rules L88–165, case tables P0–P11 L196–403 plus reference tables L167–194, H L405–419, audit L421–434, report L436–460). Runners load the whole file (`e2e.md` L73: "Read docs/agents/e2e.md fully") but each runner needs only its slice (slice map `e2e.md` L132–138).

Concrete split (keeps `docs/agents/*.md` naming):

- `docs/agents/e2e.md` → index: keep L1–19 (intro + Contents), Roles summary, slice map table (L132–138), links to the four files, Report format (L436–460).
- `docs/agents/e2e-runner.md` ← L40–165 (Roles prompt L40–86, Parallel run L88–124 + slice map, Runner rules L145–165).
- `docs/agents/e2e-phases-setup.md` ← P0 (L196), P1 (L208), P2 (L218), P3 (L229), P4 (L248).
- `docs/agents/e2e-phases-debug.md` ← P5 (L267), P6 (L303), P7 (L317), P8 (L331), P9 (L363).
- `docs/agents/e2e-phases-closeout.md` ← P10 (L380), P11 (L397), H (L405), Audit medium (L421); Sandbox reference table (L167–194) moves here or to the index (single table, keep whole).

## Proposal 2 (rank #2): `docs/publishing-options.md` → index + 1 howto

Why: 327 lines mixing decision record (options a–d L32–144, ruled-out L146–157) with a 169-line implementation section (L159–327) that duplicates the landed workflow file and says so itself ("Reproduced here verbatim — edit the workflow file, not this copy", L204; "The sketch below was the starting point", L181, L278).

Concrete split:

- `docs/publishing-options.md` → index: keep recommendation (L3–13), glance table (L15–30), per-option verdict paragraphs only, ruled-out list.
- Move L159–327 → `docs/publishing-release-howto.md`, or (preferred, smaller): delete the verbatim workflow YAML (L206–268) and sketch scripts (L287–318) and link the landed files instead — the doc already admits they are copies.

## Proposal 3 (rank #3, optional): `docs/TROUBLESHOOTING.md`

Already index-shaped (symptom links L6–9 → 4 `##` areas → 14 `###` entries). Split only if entries keep growing: one file per `##` area (`troubleshooting-setup.md`, `troubleshooting-vs-state.md`, `troubleshooting-targets.md`, `troubleshooting-eval.md`), with `TROUBLESHOOTING.md` keeping L1–9 as the index. Do not split pre-emptively — 161 lines with short entries is fine as-is.

## Non-candidates and reasons

- `docs/TOOLS.md` (100 lines, densest file at 15.7 KB): already indexed via Contents line (L7: Session/Breakpoints/Execution/Inspection/Envelope/Error catalog). Splitting the error catalog (L65–100) would break inbound anchors (`TROUBLESHOOTING.md` L3–4 `TOOLS.md#error-catalog`, `TROUBLESHOOTING.md` L129 `TOOLS.md#execution-in-executiontools`, `README.md` L52–54). Cost exceeds payoff.
- `GLOSSARY.md` (145 lines): flat term list is the point; `AGENTS.md` L9 mandates its terms. Only observation: 4 E2E-scoped terms (L116–145) could move to e2e docs if Proposal 1 lands — not a split, a move.
- `README.md` (109 lines): single-audience overview. The 21-row tool table (L26–51) duplicates `TOOLS.md`; shrink to a pointer, don't split.
- `INSTALL.md` (115 lines): situation table (L10) already routes 3 paths. At most move Option B (L102–107) into `docs/agents/build.md` as a pointer — one-line change, not a split.
- `docs/agents/domain.md` (51 lines): generic upstream skill text (`GLOSSARY-MAP.md`, `src/<context>/` trees L26–39 don't match this repo); splitting would enshrine template content — trim, don't split.
- `docs/adr/0001–0012` + `adr/README.md`: every ADR is one decision in Context/Decision/Consequences shape (verified 0008 L5–29, 0009 L5–42, 0010 L5–59, 0011 L5–54, 0012 L5–46; 0001–0007 are 19–24 lines each, same shape). No ADR mixes decisions. `adr/README.md` is already the index pattern (`docs/README.md` L18 cites it) — extend it, don't split it.
- Everything ≤ 35 lines (`AGENTS.md`, `CLAUDE.md`, `CODING_STANDARDS.md`, `architecture.md`, `build.md`, `definition-of-done.md`, `issue-tracker.md`, `live-verify.md`, `docs/README.md`): single topic, already index or checklist; splitting adds files with no payoff.

Stale count noticed (not a split): `docs/README.md` L18 says "eleven ADRs" but `docs/adr/` holds 12 (0001–0012 + README listing L5–18 of `docs/adr/README.md`).

## Recommended order

1. `docs/agents/e2e.md` (460 lines × 3 audiences × runner token cost — biggest payoff).
2. `docs/publishing-options.md` (mostly deletion of duplicated verbatim copies, not a true split — cheapest).
3. `docs/TROUBLESHOOTING.md` only when it passes ~250 lines or gains a 5th `##` area.
4. No action on ADRs, `TOOLS.md`, `GLOSSARY.md`, `README.md`, `INSTALL.md`, `docs/README.md`, `AGENTS.md`, small `agents/*.md` files.
