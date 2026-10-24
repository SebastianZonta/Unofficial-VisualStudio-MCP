# ADR index

Architecture decisions, oldest first. Bodies are historical: a superseded ADR stays as written. Add new ones per the `domain-modeling` skill and link them here; cite them when work conflicts (see [domain.md](../agents/domain.md)).

| # | Decision | Status | In one line |
|---|---|---|---|
| 0001 | [Out-of-proc COM automation, not a VSIX](0001-com-not-vsix.md) | accepted | Drive EnvDTE out-of-proc through the instance's DTE; VSIX only as fallback. |
| 0002 | [Single `IDebugBridge` seam](0002-debug-bridge-seam.md) | accepted | All automation crosses one interface; live COM is snapshotted to DTOs. |
| 0003 | [Single active session](0003-single-session.md) | superseded by [0008](0008-multi-session.md) | One session at a time; a new `attach` replaces it and the old id goes `stale-session`. |
| 0004 | [Consent plus same-user gate on attach](0004-consent-gate.md) | accepted | `attach_process` needs `consent: true` and same-user; unknown owners fail closed. |
| 0005 | [Read-only evaluator](0005-read-only-eval.md) | superseded by [0010](0010-no-side-effects-eval.md) | `evaluate` is read-only by construction: no assignment or statements. |
| 0006 | [Uniform result envelope](0006-envelope.md) | accepted | Every result carries `mode`, `lastHit`, truncation and coded errors. |
| 0007 | [Session-less discovery](0007-sessionless-discovery.md) | accepted | `list_instances`/`list_processes` need no session; matching stays agent-side. |
| 0008 | [Multi-session per VS instance](0008-multi-session.md) | accepted (supersedes 0003) | One debug session per VS instance, many alive; `list_sessions` and `detach` added. |
| 0009 | [Launch needs no consent flag](0009-launch-without-consent.md) | accepted | `launch_process` creates its target, so consent (0004) applies only to attach. |
| 0010 | [No-side-effects evaluation](0010-no-side-effects-eval.md) | accepted (supersedes 0005) | One read-only rule for every expression: text rule first, then live `, nse`. |
| 0011 | [Exception settings in the VS instance](0011-exception-settings-in-vs-instance.md) | accepted | Exception-break settings live in the VS instance's table; breaks carry an `exception` payload. |
