using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VsDebugMcp;

// Builder-only harness (never a tool): attach + set + list against the real VS, print, exit.
if (args.Contains("--live-check-full", StringComparer.Ordinal))
    return await LiveCheck.RunFullAsync(args);

if (args.Contains("--live-check", StringComparer.Ordinal))
    return await LiveCheck.RunAsync(args);

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

if (args.Contains("--live", StringComparer.Ordinal))
    builder.Services.AddSingleton<IDebugBridge, DteDebugBridge>();
else
    builder.Services.AddSingleton<IDebugBridge, FakeDebugBridge>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SessionTools>()
    .WithTools<BreakpointTools>()
    .WithTools<ExecutionTools>()
    .WithTools<InspectionTools>();

await builder.Build().RunAsync();

return 0;
