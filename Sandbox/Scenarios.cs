namespace Sandbox;

/// <summary>One method per scenario. The locals in scope at each marker are listed in
/// docs/agents/e2e.md; change them together.</summary>
internal static class Scenarios
{
    // loop: conditions, hit counts, logpoints. Tick runs once per i = 1..count.
    public static int Loop(int count)
    {
        var total = 0;
        for (var i = 1; i <= count; i++)
            total = Tick(i, total);
        Console.WriteLine($"loop done total={total}"); // E2E:AFTER_LOOP
        return 0;
    }

    private static int Tick(int i, int total)
    {
        var next = total + i; // E2E:TICK
        return next;
    }

    // inspect: nested objects, a 1500-item list, a 2000-char string (truncation),
    // nulls, enums, structs, and members with side effects (no-side-effects mode).
    public static int Inspect()
    {
        var order = Order.Sample(itemCount: 1500);
        var counter = new Counter();
        var note = order.Note;
        int? missing = null;
        var subtotal = order.ComputeSubtotal();
        Console.WriteLine($"inspect {order.Id} {counter.Value} {note.Length} {missing} {subtotal}"); // E2E:INSPECT
        return 0;
    }

    // stack: depth frames of Recurse; each frame has its own level (depth * 10) and node.
    public static int Stack(int depth)
    {
        Recurse(0, depth);
        return 0;
    }

    private static void Recurse(int depth, int max)
    {
        var level = depth * 10;
        var node = new Node(depth, $"frame{depth}");
        if (depth + 1 < max)
            Recurse(depth + 1, max);
        else
            Console.WriteLine($"bottom level={level} node={node.Label}"); // E2E:STACK_BOTTOM
    }

    // step: over / into / out around a call.
    public static int Step()
    {
        var a = 1; // E2E:STEP_START
        var b = Add(a, 2); // E2E:STEP_CALL
        var c = a + b;
        Console.WriteLine($"step c={c}");
        return 0;
    }

    private static int Add(int x, int y)
    {
        var sum = x + y; // E2E:STEP_INTO
        return sum;
    }

    // threads: workers hit the same line concurrently (5 rounds each).
    public static int Threads(int workers)
    {
        var threads = Enumerable.Range(1, workers).Select(id => new Thread(() => Worker(id, 5))).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());
        Console.WriteLine("threads done");
        return 0;
    }

    private static void Worker(int id, int rounds)
    {
        for (var n = 1; n <= rounds; n++)
        {
            var stamp = id * 100 + n; // E2E:WORKER
            Thread.Sleep(10 + stamp % 7);
        }
    }

    // async: a break after an await (state-machine frames).
    public static async Task<int> AsyncFlow()
    {
        var before = Environment.CurrentManagedThreadId;
        await Task.Delay(50);
        var after = Environment.CurrentManagedThreadId;
        Console.WriteLine($"async before={before} after={after}"); // E2E:ASYNC
        return 0;
    }

    // throw: an unhandled exception (VS breaks; inspect $exception).
    public static int Throw()
    {
        var reason = "sandbox boom";
        Fail(reason);
        return 0;
    }

    private static void Fail(string reason) => throw new InvalidOperationException(reason); // E2E:THROW

    // echo: proves launch_process arguments, working directory and environment.
    public static int Echo(string[] args)
    {
        var argsJoined = string.Join("|", args.Skip(1));
        var cwd = Environment.CurrentDirectory;
        var envValue = Environment.GetEnvironmentVariable("SANDBOX_E2E") ?? "(unset)";
        Console.WriteLine($"echo args={argsJoined} cwd={cwd} env={envValue}"); // E2E:ECHO
        return 0;
    }

    // wait: a long-running target for pause/resume and attach_process. Exits by itself
    // after `seconds` so a forgotten run never lingers.
    public static int Wait(int seconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        var beat = 0;
        while (DateTime.UtcNow < deadline)
        {
            beat = Heartbeat(beat);
            Thread.Sleep(250);
        }
        return 0;
    }

    private static int Heartbeat(int beat)
    {
        var next = beat + 1; // E2E:HEARTBEAT
        return next;
    }
}
