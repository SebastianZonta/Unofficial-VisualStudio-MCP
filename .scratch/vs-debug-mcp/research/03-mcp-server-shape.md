# Ticket 03 — MCP server shape (C#, same-machine stdio)

## SDK + version

- Official SDK: `modelcontextprotocol/csharp-sdk` (maintained with Microsoft).
- Packages: `ModelContextProtocol` (hosting + DI + attribute discovery — use this),
  `ModelContextProtocol.Core` (client/low-level only), `ModelContextProtocol.AspNetCore` (HTTP only — NOT needed for v1).
- Latest release observed: **v2.2.0** (Aug 2026; v2.0.0 aligned to MCP spec 2026-07-28). Spec should mandate
  `ModelContextProtocol` **v2.x** and `Microsoft.Extensions.Hosting`, target **net8.0+** (net10 where VS 2026 requires it).
- Transport: **stdio only** for v1 (`WithStdioServerTransport()`). No HTTP listener, no port, no auth surface.

## Minimal code shape (mandate this)

Project: single console project (`dotnet new console`), e.g. `VsDebugMcp/`, one `Tools/` folder with one static class
per group (e.g. `SessionTools`, `BreakpointTools`, `SteppingTools`, `InspectionTools`), each:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace); // NEVER stdout
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // or explicit .WithTools<T>() per class — preferred for <10-tool audit
await builder.Build().RunAsync();

[McpServerToolType]
public static class SteppingTools
{
    [McpServerTool, Description("Step over the current line in the active session.")]
    public static async Task<string> StepOver(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        CancellationToken ct) => await bridge.StepOverAsync(sessionId, ct);
}
```

Registration: attribute-based is the blessed path — `[McpServerToolType]` on class,
`[McpServerTool]` + `[Description]` on each method. Prefer explicit `.WithTools<A,B,C>()` over
`WithToolsFromAssembly()` so the tool list stays reviewable (grep = the surface).
`McpServerTool.Create` factory is for dynamic/generated tools only — avoid in v1.

## Conventions to mandate

1. **Tool naming:** `snake_case`, plain verbs (`attach`, `set_breakpoint`, `step_over`, `read_stack`, `read_locals`,
   `evaluate`). No overloads; one method = one tool name (`[McpServerTool(Name = "...")]` when C# name differs).
2. **Descriptions everywhere:** every tool AND every parameter gets `[Description]` — it is the LLM's UI.
3. **Parameters:** JSON-bound args only for domain data (sessionId, file/line, expression). Framework-injected
   params (`CancellationToken`, `McpServer`/`IMcpServer`, `IServiceProvider`, DI services like `IDebugBridge`,
   `HttpClient`) are auto-bound and MUST NOT appear in the JSON schema. Validate explicitly in-method —
   `[Required]`/`[MaxLength]` only shape the schema, they are not enforced at runtime.
4. **Defaults for safety:** optional parameters with C# default values (e.g. `timeoutMs = 5000`,
   `maxFrames = 20`, `maxVariables = 50`) keep calls small and safe; debugger side-effects default OFF
   (evaluate = no side effects unless explicit flag; attach requires consent/path allowlist).
5. **Async + cancellation:** all tools that touch the debugger are `async Task<T>` with `CancellationToken`
   plumbed through; honor `notifications/cancelled` (client abort must stop the step/eval).
6. **Single-session model:** tools take an opaque `sessionId` (string) scoping all state server-side; stdio is
   inherently single-session so no session headers needed.
7. **Errors as tool results, not crashes:** throw `McpException` (message IS forwarded to the client/LLM) for
   expected failures (bad session, no symbols, timeout); return `CallToolResult` with `IsError=true` for full
   control. Any other exception becomes a *generic* message (no leak) — so never rely on raw exception text.
   Protocol errors (`McpProtocolException`) and `OperationCanceledException` are reserved — do not throw them for domain errors.
8. **Logging:** ALL logs to **stderr** (`LogToStandardErrorThreshold`); stdout is the protocol pipe — anything
   written there corrupts JSON-RPC.
9. **Surface budget:** hard cap ~10 tools; adding #11 requires removing/merging (e.g. `read_stack` covers frames,
   not `read_threads`+`read_tasks`). No prompts/resources in v1 — tools only.

## Anti-patterns to avoid

- Logging/Console.Write to **stdout**; `Console.WriteLine` debugging in the server process.
- `WithToolsFromAssembly()` in a large assembly (silent tool creep) — use explicit `.WithTools<>()`.
- Exposing raw VS/DTE/COM handles, PIDs-as-authority, or free-form process-kill/eval-with-side-effects tools.
- One mega-tool (`debug(action: string, ...)`) — destroys schema validation and per-tool descriptions.
- Throwing raw `Exception`/`InvalidOperationException` for domain errors (client sees only "error", LLM can't recover).
- Adding HTTP transport "for convenience" — doubles the threat model (ports, auth) for zero v1 benefit.
- Sync-over-async (`.Result`/`.Wait()`) in tools — deadlocks the single stdio loop; always await with `ct`.
- Returning unbounded payloads (full heap, 10k frames) — always paginate/truncate with explicit limits.

## Sources

- Getting started (packages, stdio setup, `[McpServerToolType]`/`[McpServerTool]`/`[Description]`, stderr logging):
  https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html
- Tools (registration options, DI/special params, `CallToolResult IsError` handling):
  https://csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html
- Transports (stdio = single-session; HTTP not needed): https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html
- Cancellation (`CancellationToken` wiring): https://csharp.sdk.modelcontextprotocol.io/v2/concepts/cancellation/cancellation.html
- `McpServerToolAttribute` API (param binding, `McpException` vs generic, `CallToolResult`, annotation defaults):
  https://csharp.sdk.modelcontextprotocol.io/v2/api/ModelContextProtocol.Server.McpServerToolAttribute.html
- Repo + releases (v2.2.0 latest, v2.0.0 spec 2026-07-28): https://github.com/modelcontextprotocol/csharp-sdk
- MS Learn quickstart (template `Microsoft.McpServer.ProjectTemplates`, stdio): https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server
