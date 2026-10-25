using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VsDebugMcp;

// Builder-only harness (never a tool): attach + set + list against the real VS, print, exit.
var liveChecks = new (string Flag, Func<string[], Task<int>> Run)[]
{
    ("--live-check-full", LiveCheck.RunFullAsync),
    ("--live-check-launch", LiveCheck.RunLaunchAsync),
    ("--live-check-occupied", LiveCheck.RunOccupiedAsync),
    ("--live-check-run-to", LiveCheck.RunToCursorAsync),
    ("--live-check-audit", LiveCheck.RunAuditAsync),
    ("--live-check-exception", LiveCheck.RunExceptionAsync),
    ("--live-check", LiveCheck.RunAsync),
};
foreach (var (flag, run) in liveChecks)
    if (args.Contains(flag, StringComparer.Ordinal))
        return await run(args);

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var fake = args.Contains("--fake", StringComparer.Ordinal);
builder.Services.AddSingleton(typeof(IDebugBridge), fake ? typeof(FakeDebugBridge) : typeof(DteDebugBridge));
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
