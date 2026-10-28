# ADR-0014: Skills ship inside the release and the installer installs them

Status: accepted

## Context

The flow-testing skills name this server's tools and parameters, so a skill
written for one server version can break against another. The server already
reaches developers through one channel: a release zip fetched by a one-line
installer that registers it in every detected MCP client. Three other channels
were available (research: `.scratch/flow-testing-skills/research/01-skill-distribution.md`):

- **Per-client plugins plus `npx skills`**, the route Matt Pocock's skills take.
  It reaches 75+ agents, but needs Node 22+, is a second install step, and lets
  the skills drift from the installed exe.
- **MCP prompts served by the exe.** They stay version-locked with no install
  step, but every client that documents them invokes them only from the user,
  Codex lacks them, and a skill could not invoke another.
- **A Claude Code plugin marketplace.** It reaches one client only.

## Decision

The skills live in this repo, ship inside the release zip, and the installer
installs them into the user-level skill folder of each client it wires. It
replaces only the skill folders it owns. Which folders it writes, and how it avoids
a client that reads two shared folders listing a skill twice, is settled by the
spec's probe P1. A docs contract test pins every tool and parameter a skill names
to the registered tool surface. `npx skills add` against the same folder stays
available as a convenience, and the plugin marketplace is deferred.

## Consequences

Installing or updating the server installs or updates the matching skills, with
no Node dependency and no second step. The installer now writes outside its
install folder, which INSTALL must state. Clients with no local skill folder
(Claude Desktop) get no skills. A developer who also installs through
`npx skills` sees each skill twice: the docs warn against mixing channels.
