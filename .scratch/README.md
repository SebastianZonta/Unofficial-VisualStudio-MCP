# Work index

Specs and issues, one directory per feature. Status is copied from each file's `Status:` line; edit the file, not this table. Conventions and valid `Status:` values: [issue-tracker.md](../docs/agents/issue-tracker.md).

Add a feature: create `.scratch/<feature-slug>/spec.md` plus `issues/NN-<slug>.md` as described in [issue-tracker.md](../docs/agents/issue-tracker.md), then add a row here.

## Open

| Feature | Spec | Status | Issues |
|---|---|---|---|
| workflow-hardening | [spec](workflow-hardening/spec.md) | ready-for-agent | [01](workflow-hardening/issues/01-publish-script-and-ci-gate.md) ready-for-agent · [02](workflow-hardening/issues/02-definition-of-done-and-tracker.md) ready-for-agent |
| audit-oct24-medium | [spec](audit-oct24-medium/spec.md) | ready-for-agent | none |
| exception-config | [spec](exception-config/spec.md) | ready-for-agent | [01](exception-config/issues/01-seam-and-fake.md) resolved · [02](exception-config/issues/02-live-tools-and-docs.md) resolved |
| tool-slices | [spec](tool-slices/spec.md) | 1 resolved, 4 ready-for-agent | [01](tool-slices/issues/01-hygiene.md) resolved · [02](tool-slices/issues/02-platform.md) ready-for-agent · [03](tool-slices/issues/03-session-launch.md) ready-for-agent · [04](tool-slices/issues/04-tool-slices.md) ready-for-agent · [05](tool-slices/issues/05-contracts.md) ready-for-agent |
| e2e-2026-10-23 | none (E2E findings) | 3 resolved, 1 needs-info | [01](e2e-2026-10-23/issues/01-process-tools-host-error.md) resolved · [02](e2e-2026-10-23/issues/02-logpoint-spurious-break.md) resolved · [03](e2e-2026-10-23/issues/03-evaluate-allows-method-calls.md) resolved · [04](e2e-2026-10-23/issues/04-two-vs-environment-blocked.md) needs-info |
| e2e-2026-10-24 | none (E2E findings) | 8 resolved, 1 ready-for-agent, 1 needs-triage, 1 needs-info | [01](e2e-2026-10-24/issues/01-busy-vs-orphans-breakpoints.md) resolved · [02](e2e-2026-10-24/issues/02-set-breakpoint-adopts-user-breakpoint.md) resolved · [03](e2e-2026-10-24/issues/03-stale-publish-poisons-system-management.md) needs-triage · [04](e2e-2026-10-24/issues/04-attach-default-binds-oldest-instance.md) ready-for-agent · [05](e2e-2026-10-24/issues/05-stale-suo-breakpoints-block-rerun.md) resolved · [06](e2e-2026-10-24/issues/06-attach-devenv-returns-process-not-found.md) resolved · [07](e2e-2026-10-24/issues/07-read-variables-path-carriage.md) resolved · [08](e2e-2026-10-24/issues/08-attach-default-parallel-contention.md) resolved · [09](e2e-2026-10-24/issues/09-evaluate-generic-arity-gate-hole.md) resolved · [10](e2e-2026-10-24/issues/10-logpoint-hit-surfaces-as-stop.md) needs-info · [11](e2e-2026-10-24/issues/11-modal-dialog-not-vs-busy.md) resolved |
| e2e-2026-10-26-2211 | none (E2E findings) | 2 resolved, 1 ready-for-human | [01](e2e-2026-10-26-2211/issues/01-c10-logpoint-first-hit-missing.md) resolved · [02](e2e-2026-10-26-2211/issues/02-step-vs-busy-executed-anyway.md) resolved · [03](e2e-2026-10-26-2211/issues/03-e28-manual-run-to-cursor-window.md) ready-for-human |

## Done

| Feature | Spec | Status | Issues |
|---|---|---|---|
| vs-debug-mcp | [spec](vs-debug-mcp/spec.md), [map](vs-debug-mcp/map.md) | done | [01](vs-debug-mcp/issues/01-vs-integration-paths.md) · [02](vs-debug-mcp/issues/02-managed-debug-apis.md) · [03](vs-debug-mcp/issues/03-mcp-server-shape.md) · [04](vs-debug-mcp/issues/04-minimal-tool-surface.md) · [05](vs-debug-mcp/issues/05-session-safety-model.md) · [06](vs-debug-mcp/issues/06-tool-shape-stub.md) · [07](vs-debug-mcp/issues/07-host-skeleton-attach.md) · [08](vs-debug-mcp/issues/08-breakpoints-end-to-end.md) · [09](vs-debug-mcp/issues/09-execution-control-end-to-end.md) · [10](vs-debug-mcp/issues/10-inspection-end-to-end.md) · [11](vs-debug-mcp/issues/11-real-com-dte-bridge.md) · [12](vs-debug-mcp/issues/12-safety-acceptance-walkthrough.md) all resolved; research: [01](vs-debug-mcp/research/01-vs-integration-paths.md), [02](vs-debug-mcp/research/02-managed-debug-apis.md), [03](vs-debug-mcp/research/03-mcp-server-shape.md) |
| audit-must-fix | [spec](audit-must-fix/spec.md) | done | none |
| audit-oct24-high | [spec](audit-oct24-high/spec.md) | done | none |
| audit-should-fix | [spec](audit-should-fix/spec.md) | done | none |
| conditional-breakpoints | [spec](conditional-breakpoints/spec.md) | done | [01](conditional-breakpoints/issues/01-seam-and-fake.md) · [02](conditional-breakpoints/issues/02-tool-surface-and-docs.md) · [03](conditional-breakpoints/issues/03-live-bridge-and-verify.md) all resolved |
| launch-process | [spec](launch-process/spec.md) | done | [01](launch-process/issues/01-seam-and-fake.md) · [02](launch-process/issues/02-live-tool-and-docs.md) both resolved |
| list-instances | [spec](list-instances/spec.md) | done | none |
| list-processes | [spec](list-processes/spec.md) | done | none |
| module-symbol-status | [spec](module-symbol-status/spec.md) | done | [01](module-symbol-status/issues/01-seam-and-fake.md) · [02](module-symbol-status/issues/02-live-tool-and-docs.md) both resolved |
| run-to-cursor | [spec](run-to-cursor/spec.md) | resolved | [01](run-to-cursor/issues/01-seam-and-fake.md) · [02](run-to-cursor/issues/02-live-tool-and-docs.md) both resolved |
| set-variable | [spec](set-variable/spec.md) | done | [01](set-variable/issues/01-seam-and-fake.md) · [02](set-variable/issues/02-live-tool-and-docs.md) both resolved |
| multi-attach | map only: [map](multi-attach/map.md) | resolved | [01](multi-attach/issues/01-session-store-and-tool-surface.md) · [02](multi-attach/issues/02-multi-dte-com-lifetime.md) · [03](multi-attach/issues/03-bridge-and-fake-build.md) · [04](multi-attach/issues/04-docs-and-branch-hygiene.md) all resolved |

## E2E run reports

Output of the pass in [e2e.md](../docs/agents/e2e.md) lands in `.scratch/e2e-runs/<yyyy-MM-dd-HHmm>.md`: gitignored, local to the machine that ran it, no status.
