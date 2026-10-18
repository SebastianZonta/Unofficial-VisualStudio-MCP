Type: research
Status: resolved
Labels: wayfinder:research

## Question

What is the current best-practice shape for a local same-machine MCP server in C# (stdio transport, .NET MCP SDK): project layout, tool-registration pattern, parameter/error conventions, and how to keep the surface to < ~10 plain-named tools with safe defaults? List the concrete SDK and patterns the spec should mandate, plus what to avoid.

## Answer

Resolved via background research. Mandate `ModelContextProtocol` v2.x + Hosting, stdio only (`WithStdioServerTransport()`), explicit `.WithTools<A,B,C>()` with `[McpServerToolType]`/`[McpServerTool]`/`[Description]` everywhere; snake_case verbs, C# optional defaults (timeouts, caps), `CancellationToken` plumbed, `sessionId` scoping, `McpException`/`IsError` errors, all logs to stderr, hard cap ~10 tools. Avoid stdout logging, assembly-scan creep, mega-tool, raw exceptions, HTTP transport, sync-over-async, unbounded payloads. Full detail in `.scratch/vs-debug-mcp/research/03-mcp-server-shape.md`.
