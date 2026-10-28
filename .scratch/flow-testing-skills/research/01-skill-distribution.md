# Skill distribution across MCP clients, 2026-10-27

Question: which clients this installer wires load Agent Skills (`SKILL.md`), from
where, and what the alternatives (`npx skills`, MCP prompts, a Claude Code plugin)
offer. Fetched from primary sources on 2026-10-27; WebSearch was unavailable, so
everything comes from direct fetches. UNCONFIRMED marks claims no primary source
backed.

## Skill folders per client (Windows)

| Client | Loads skills | Project dirs | User dirs | Reads other clients' dirs |
|---|---|---|---|---|
| opencode | yes | `.opencode/skills`, `.claude/skills`, `.agents/skills` | `~/.config/opencode/skills`, `~/.claude/skills`, `~/.agents/skills` | yes ([docs](https://raw.githubusercontent.com/sst/opencode/dev/packages/web/src/content/docs/skills.mdx)) |
| Claude Code | yes | `.claude/skills` | `~/.claude/skills` | `.agents/skills` UNCONFIRMED ([docs](https://code.claude.com/docs/en/skills)) |
| Claude Desktop | account-level upload only | n/a | ZIP upload in Customize > Skills | no ([help](https://support.claude.com/en/articles/12512180-using-skills-in-claude)) |
| Cursor | yes | `.agents/skills`, `.cursor/skills` | `~/.agents/skills`, `~/.cursor/skills` | also `.claude/skills`, `.codex/skills` ([docs](https://cursor.com/docs/context/skills)) |
| VS Code (Copilot) | yes | `.github/skills`, `.claude/skills`, `.agents/skills` | `~/.copilot/skills`, `~/.claude/skills`, `~/.agents/skills` | yes ([docs](https://code.visualstudio.com/docs/copilot/customization/agent-skills)) |
| Windsurf | UNCONFIRMED (docs 503) | `.windsurf/skills` (third-party list) | `~/.codeium/windsurf/skills` (third-party list) | unknown ([vercel-labs/skills agents.md](https://raw.githubusercontent.com/vercel-labs/skills/main/docs/agents.md)) |
| Gemini CLI | yes | `.gemini/skills`, `.agents/skills` | `~/.gemini/skills`, `~/.agents/skills` | `.claude/skills` not mentioned ([docs](https://geminicli.com/docs/cli/skills/)) |
| Codex CLI | yes (docs 503; from source) | `.agents/skills` | `~/.agents/skills` | not confirmed ([capability_locations.rs](https://github.com/openai/codex/blob/main/codex-rs/exec-server/src/discoverV2/capability_locations.rs)) |

`~/.claude/skills` reaches Claude Code, opencode, Cursor and Copilot. `~/.agents/skills`
reaches Cursor, Copilot, Gemini CLI, opencode and Codex. opencode, Cursor and Copilot
read both, so writing one skill to both folders may list it twice (open: probe P1 in the spec).

## `npx skills` (vercel-labs/skills)

- MIT, Vercel Labs, needs Node 22.20+, 75+ agents ([repo](https://github.com/vercel-labs/skills)).
- Project install by default, user-level with `-g`; one canonical copy symlinked per
  agent, `--copy` to copy.
- Installs from any GitHub repo or subfolder (`npx skills add owner/repo`, or a
  `.../tree/main/<path>` URL), GitLab, any git URL, a local path, or a SKILL.md/zip URL
  ([commands](https://raw.githubusercontent.com/vercel-labs/skills/main/docs/commands.md)).
- Matt Pocock's skills use per-client channels (Claude Code plugin, Codex and Copilot
  plugin marketplaces, `gemini skills install`) and `npx skills` for the rest; his
  README warns that two install methods duplicate every skill ([README](https://github.com/mattpocock/skills)).

## MCP prompts

- The C# SDK supports server prompts (`[McpServerPromptType]`, `[McpServerPrompt]`,
  `WithPrompts<T>()`), present in v2.2.0 ([docs](https://csharp.sdk.modelcontextprotocol.io/v1/concepts/prompts/prompts.html),
  [source](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/src/ModelContextProtocol/McpServerBuilderExtensions.cs)).
- Claude Code (`/server:prompt`), VS Code (`/<server>.<prompt>`), Gemini CLI and Cursor
  surface them as user commands; opencode lists them in client code; Codex does not
  support them (open [#5059](https://github.com/openai/codex/issues/5059)).
- Every documented surface is user-invoked; no source confirms model invocation.
  Prompts therefore cannot let the plan or run skill invoke another skill, and miss Codex.

## Claude Code plugin marketplace

- Any repo with `.claude-plugin/marketplace.json` (`name`, `owner`, `plugins`) is a
  marketplace; an entry may use the repo root as its source ([docs](https://code.claude.com/docs/en/plugin-marketplaces)).
- Users run `/plugin marketplace add owner/repo`, then `/plugin install name@marketplace`;
  skills appear as `/plugin-name:skill-name`. Nothing beyond pushed files is needed
  ([hosting](https://code.claude.com/docs/en/plugins/host-marketplace.md)).
- Auto-update is off by default; new versions arrive when `version` changes.

## User-only invocation flags seen in prior art

- Claude Code: `disable-model-invocation: true` in SKILL.md frontmatter (Matt's
  `wayfinder`, `to-spec`, `setup-matt-pocock-skills`).
- Codex: `policy.allow_implicit_invocation: false` in the skill's `agents/openai.yaml`
  (same skills).
- opencode, Cursor, Copilot, Gemini CLI: no equivalent found (open: probe P2 in the spec).
