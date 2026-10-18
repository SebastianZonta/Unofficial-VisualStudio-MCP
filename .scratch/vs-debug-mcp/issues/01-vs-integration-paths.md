Type: research
Status: resolved
Labels: wayfinder:research

## Question

Which integration path lets a same-machine MCP server talk to Visual Studio 2026 for C# debugging with the least friction and most reliability: a tiny VS extension exposing a local bridge (stdio/HTTP/named pipe) vs out-of-proc COM/DTE automation with no extension vs Concord/MIEngine APIs? Cover VS 2026 API changes, install/permission friction, lifetime/discovery of the VS instance, and failure modes. Recommend one path for the spec with reasons and fallback.

## Answer

Resolved via background research. Recommended: out-of-proc COM/DTE automation, no extension — stable EnvDTE covers the full minimal core with lowest friction; live VsBridge precedent (.NET 8 exe, COM/EnvDTE, ~11 tools) claims VS 2026 support. VS 2026 adds no debugger-automation API in the new extension model; EnvDTE not obsoleted. Fallback: tiny in-proc VSIX bridge only if ROT targeting/COM hangs/elevation block. Concord/MIEngine rejected (engine-authorship layer, wrong for driving). Full detail in `.scratch/vs-debug-mcp/research/01-vs-integration-paths.md`.
