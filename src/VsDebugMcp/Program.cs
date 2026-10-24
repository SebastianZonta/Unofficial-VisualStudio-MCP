using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VsDebugMcp;

// Builder-only harness (never a tool): attach + set + list against the real VS, print, exit.
if (args.Contains("--live-check-full", StringComparer.Ordinal))
    return await LiveCheck.RunFullAsync(args);

if (args.Contains("--live-check-launch", StringComparer.Ordinal))
    return await LiveCheck.RunLaunchAsync(args);

if (args.Contains("--live-check-run-to", StringComparer.Ordinal))
    return await LiveCheck.RunToCursorAsync(args);

if (args.Contains("--live-check-audit", StringComparer.Ordinal))
    return await LiveCheck.RunAuditAsync(args);

if (args.Contains("--live-check", StringComparer.Ordinal))
    return await LiveCheck.RunAsync(args);

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var fake = args.Contains("--fake", StringComparer.Ordinal);
if (fake)
    builder.Services.AddSingleton<IDebugBridge, FakeDebugBridge>();
else
    builder.Services.AddSingleton<IDebugBridge, DteDebugBridge>();
// --live is a no-op (live is the default); --fake opts into the fake bridge.
if (fake)
    Console.Error.WriteLine("warning: running with --fake: values are canned and must not be treated as a real debuggee");

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SessionTools>()
    .WithTools<BreakpointTools>()
    .WithTools<ExecutionTools>()
    .WithTools<InspectionTools>();

await builder.Build().RunAsync();

return 0;
