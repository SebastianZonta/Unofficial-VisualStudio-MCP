// Opt-in live E2E: drives the real server binary in live mode over MCP stdio.
// Enabled only with VSDEBUG_E2E=1 (plus an interactive VS 2026); otherwise every
// test skips, so CI and plain `dotnet test` stay VS-free. Optional configuration:
//   VSDEBUG_E2E_SERVER  absolute path to VsDebugMcp.dll (default: Debug build output)
//   VSDEBUG_E2E_VS_A    instanceOverride display name for VS instance A
//   VSDEBUG_E2E_VS_B    instanceOverride display name for VS instance B (two-VS cases skip when unset)
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace VsDebugMcp.E2E;

/// <summary>Opt-in gate evaluated at discovery time: without VSDEBUG_E2E=1 the test
/// is statically skipped (dynamic SkipException is reported as Failed by this
/// toolchain, so the check must happen in the attribute constructor).</summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VSDEBUG_E2E") != "1")
            Skip = "live E2E is opt-in: set VSDEBUG_E2E=1 with VS 2026 running.";
    }
}

/// <summary>One shared live server for the whole E2E run, plus Sandbox build/locate helpers.</summary>
public sealed class E2EFixture : IAsyncLifetime
{
    public static string RepoRoot { get; } =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static void Gate()
    {
        if (Environment.GetEnvironmentVariable("VSDEBUG_E2E") != "1")
            throw new InvalidOperationException("live E2E is opt-in: set VSDEBUG_E2E=1 with VS 2026 running.");
    }

    public McpClient Client { get; private set; } = null!;
    public string SandboxExe { get; private set; } = "";
    public string SandboxScenarios { get; private set; } = "";
    public string? VsA { get; private set; }
    public string? VsB { get; private set; }

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("VSDEBUG_E2E") != "1")
            return;
        var serverDll = Environment.GetEnvironmentVariable("VSDEBUG_E2E_SERVER")
            ?? Path.Combine(RepoRoot, "src", "VsDebugMcp", "bin", "Debug", "net10.0-windows", "VsDebugMcp.dll");
        if (!File.Exists(serverDll))
            throw new InvalidOperationException($"live E2E needs a built server: run `dotnet build VsDebugMcp.slnx` first (looked for {serverDll}).");
       VsA = Environment.GetEnvironmentVariable("VSDEBUG_E2E_VS_A");
        VsB = Environment.GetEnvironmentVariable("VSDEBUG_E2E_VS_B");
        var sandboxDir = Path.Combine(RepoRoot, "Sandbox");
        SandboxScenarios = Path.Combine(sandboxDir, "Scenarios.cs");
        SandboxExe = Path.Combine(sandboxDir, "bin", "Debug", "net10.0", "Sandbox.exe");
        // Self-contained setup: build the Sandbox target if it is missing or stale.
        using var buildCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var build = Process.Start(new ProcessStartInfo(
            "dotnet", $"build \"{Path.Combine(sandboxDir, "Sandbox.csproj")}\" -c Debug")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        });
        if (build is not null)
        {
            await build.WaitForExitAsync(buildCts.Token);
            if (build.ExitCode != 0)
                throw new InvalidOperationException("live E2E could not build Sandbox: " + await build.StandardError.ReadToEndAsync());
        }
        if (!File.Exists(SandboxExe))
            throw new InvalidOperationException($"live E2E could not find {SandboxExe} after building Sandbox.");
        Client = await McpClient.CreateAsync(
            new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "vs-debug-e2e",
                Command = "dotnet",
                Arguments = [serverDll],
            }),
            cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
    }

    public async Task DisposeAsync()
    {
        if (Client is not null)
            await Client.DisposeAsync();
    }

    /// <summary>Marker line for `// E2E:&lt;NAME&gt;` in Sandbox/Scenarios.cs (never a hardcoded line number).</summary>
    public int MarkerLine(string name)
    {
        foreach (var line in File.ReadLines(SandboxScenarios).Select((text, i) => (text, line: i + 1)))
        {
            if (line.text.Contains($"// E2E:{name}", StringComparison.Ordinal))
                return line.line;
        }
        throw new InvalidOperationException($"Sandbox marker E2E:{name} not found in Scenarios.cs.");
    }

    /// <summary>Call a tool; throws <see cref="E2EToolException"/> carrying the wire code on tool errors.</summary>
    public static async Task<JsonElement> CallAsync(McpClient client, string tool, Dictionary<string, object?>? args, CancellationToken ct)
    {
        var result = await client.CallToolAsync(tool, args, cancellationToken: ct);
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        if (result.IsError == true)
            throw new E2EToolException(text);
        if (result.StructuredContent is not null)
            return JsonSerializer.SerializeToElement(result.StructuredContent);
        return JsonDocument.Parse(text).RootElement;
    }

    public static string ModeOf(JsonElement result)
    {
        if (result.TryGetProperty("mode", out var lower))
            return lower.GetString() ?? "";
        foreach (var prop in result.EnumerateObject())
        {
            if (prop.Name.Equals("mode", StringComparison.OrdinalIgnoreCase))
                return prop.Value.GetString() ?? "";
        }
        return "";
    }

    /// <summary>Run every cleanup LIFO, even after a failure; surfaces the first cleanup error only when the body passed.</summary>
    public static async Task<T> WithCleanupAsync<T>(Func<List<Func<Task>>, Task<T>> body)
    {
        var cleanups = new List<Func<Task>>();
        T value;
        try
        {
            value = await body(cleanups);
        }
        finally
        {
            for (var i = cleanups.Count - 1; i >= 0; i--)
            {
                try { await cleanups[i](); }
                catch { /* best effort: reruns must start clean */ }
            }
        }
        return value;
    }
}

public sealed class E2EToolException(string text) : Exception(text)
{
    public string Code => Text.Split(':')[0].Trim();
    public string Text { get; } = text;
}

[CollectionDefinition("e2e")]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>;
