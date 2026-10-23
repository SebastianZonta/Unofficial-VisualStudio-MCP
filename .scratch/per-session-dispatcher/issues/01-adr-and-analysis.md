# 01: ADR with deadlock and starvation analysis

**What to build:** a new ADR file recording the dispatcher-lane design: lanes versus fair-queue choice with reasons, lane lifecycle, lock ordering, COM apartment assumptions, and explicit deadlock plus starvation/fairness analysis.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] ADR states which design shipped and why the other was rejected, with the fallback selection and its removal/retention condition.
- [ ] Lock ordering, COM apartment assumptions, and what a blocked call holds at teardown are explicit.
- [ ] Safety gates (mine/consent, same-user) and tool contracts are recorded as unchanged.
