# 05: Probe: skill folders, duplicates and user-only flags per client (P1 + P2)

**What to build:** A recorded answer, per MCP client the installer wires, to two
questions the installer depends on:

- **P1.** Does a skill with the same name in both shared user-level folders (the
  `.claude` one and the `.agents` one) list twice in opencode, Cursor and Copilot? Does
  Windsurf load skills, and from where?
- **P2.** Which user-only flag, if any, do opencode, Cursor, Copilot and Gemini CLI honour?

The method is a dummy skill installed into the candidate folders with each candidate
flag, then listed and model-invocation tried in each client. Clients that only work
through their UI get a short checklist the developer runs. Results go into this
feature's research folder as a second note.
Spec: [Capabilities and probes](../spec.md#capabilities-and-probes) P1, P2;
research [01](../research/01-skill-distribution.md).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] For each of opencode, Claude Code, Cursor, VS Code Copilot, Windsurf, Gemini CLI and Codex: the folders it actually loads skills from, observed rather than quoted from docs.
- [ ] For opencode, Cursor and Copilot: whether a same-name skill in both shared folders lists once or twice.
- [ ] For every client: whether any flag stops the model from invoking a skill on its own, and which.
- [ ] A recommended installer target set that lists each skill exactly once per client, with the reason.
- [ ] The research note is written and linked from the spec's probe table, with P1 and P2 marked proven or with what stays unproven.
- [ ] Dummy skills are removed from every folder afterwards.
