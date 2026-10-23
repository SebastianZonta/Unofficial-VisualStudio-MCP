# 01: Labels, resolution, and rename in bridge plus fake

**What to build:** MCP-side labels with attach-time set, rename, and label-or-id resolution (exact id, exact label, unambiguous prefix) in the bridge and fake, so naming is testable with no VS.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Attach-with-label, rename, and list-shows-labels round-trip; re-attach preserves the label.
- [ ] Ambiguous prefixes fail listing candidates; duplicates, empty labels, and unknown values fail with `invalid-argument`/`no-session` as specified.
- [ ] Labels never reach VS/COM: no debugger-state mutation observable through any tool.
