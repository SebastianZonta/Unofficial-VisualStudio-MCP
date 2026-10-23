// E2E debug target (docs/agents/e2e.md). One scenario per launch: `Sandbox.exe <scenario> [n]`.
// Breakpoint lines are found by their `// E2E:<NAME>` marker, never by number; keep every
// marker on an executable statement and keep marker names unique.
using Sandbox;

var scenario = args.Length > 0 ? args[0] : "loop";
var n = args.Length > 1 && int.TryParse(args[1], out var parsed) ? parsed : 0;
Console.WriteLine($"sandbox pid={Environment.ProcessId} scenario={scenario}");

return scenario switch
{
    "loop" => Scenarios.Loop(n > 0 ? n : 20),
    "inspect" => Scenarios.Inspect(),
    "stack" => Scenarios.Stack(n > 0 ? n : 5),
    "step" => Scenarios.Step(),
    "threads" => Scenarios.Threads(n > 0 ? n : 4),
    "async" => await Scenarios.AsyncFlow(),
    "throw" => Scenarios.Throw(),
    "echo" => Scenarios.Echo(args),
    "exit" => 3,
    "wait" => Scenarios.Wait(n > 0 ? n : 600),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: Sandbox <loop|inspect|stack|step|threads|async|throw|echo|exit|wait> [n]");
    return 2;
}
