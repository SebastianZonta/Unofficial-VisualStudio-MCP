using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using VsDebugMcp;

// Seam: the registered tool surface vs the hand-written docs. Catches count and catalog drift.
public sealed class DocsContractTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VsDebugMcp.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root (VsDebugMcp.slnx) not found");
    }

    private static string Doc(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static IReadOnlyList<string> ToolNames() => ToolMethods().Select(t => t.Name).ToList();

    private static IReadOnlyList<(string Name, System.Reflection.MethodInfo Method)> ToolMethods() =>
        typeof(IDebugBridge).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (Name: m.GetCustomAttribute<McpServerToolAttribute>()?.Name, Method: m))
            .Where(p => p.Name is not null)
            .Select(p => (p.Name!, p.Method))
            .ToList();

    // Tool-call params only: the injected bridge and cancellation token are not documented.
    private static IReadOnlyList<System.Reflection.ParameterInfo> DocParams(System.Reflection.MethodInfo method) =>
        method.GetParameters()
            .Where(p => p.ParameterType != typeof(IDebugBridge) && p.ParameterType != typeof(CancellationToken))
            .ToList();

    private static string ToolRow(string toolsDoc, string tool)
    {
        var row = toolsDoc.Split('\n').FirstOrDefault(l => l.Contains($"| `{tool}` |", StringComparison.Ordinal));
        Assert.True(row is not null, $"no TOOLS.md table row for `{tool}`");
        return row!;
    }

    [Fact]
    public void Every_tool_has_a_row_in_TOOLS_and_README()
    {
        var tools = Doc("docs/TOOLS.md");
        var readme = Doc("README.md");

        Assert.All(ToolNames(), name =>
        {
            Assert.Contains($"| `{name}` |", tools);
            Assert.Contains($"| `{name}` |", readme);
        });
    }

    [Fact]
    public void Every_error_code_has_a_row_in_the_TOOLS_error_catalog()
    {
        var tools = Doc("docs/TOOLS.md");
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name != nameof(ErrorCodes.DataKey))
            .Select(f => (string)f.GetRawConstantValue()!);

        Assert.All(codes, code => Assert.Contains($"| `{code}` |", tools));
    }

    [Theory]
    [InlineData("docs/agents/e2e.md")]
    [InlineData("docs/TOOLS.md")]
    [InlineData("README.md")]
    public void Tool_counts_match_the_registered_tools(string doc)
    {
        var counts = Regex.Matches(Doc(doc), @"\b(\d+) tools\b|Tools \((\d+)\)")
            .Select(m => int.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value))
            .ToList();

        Assert.NotEmpty(counts);
        Assert.All(counts, n => Assert.Equal(ToolNames().Count, n));
    }

    [Fact]
    public void Every_tool_parameter_is_documented_in_TOOLS()
    {
        var tools = Doc("docs/TOOLS.md");
        foreach (var (name, method) in ToolMethods())
        {
            var row = ToolRow(tools, name);
            Assert.All(DocParams(method), p =>
                Assert.Contains($"`{p.Name}", row));
        }
    }

    [Fact]
    public void Optional_tool_parameters_show_a_default_marker_in_TOOLS()
    {
        // Convention: a parameter with a code default appears as `name?` or `name` = <default>.
        var tools = Doc("docs/TOOLS.md");
        foreach (var (name, method) in ToolMethods())
        {
            var row = ToolRow(tools, name);
            Assert.All(DocParams(method).Where(p => p.HasDefaultValue), p =>
                Assert.True(row.Contains($"`{p.Name}?`", StringComparison.Ordinal)
                    || row.Contains($"`{p.Name}` =", StringComparison.Ordinal),
                    $"`{name}` param `{p.Name}` has a code default but no `?`/`=` marker in TOOLS.md"));
        }
    }

    [Fact]
    public void Every_tool_and_parameter_a_skill_names_is_registered()
    {
        // Convention (docs/agents/skills.md): a tool is a backticked snake_case name or a
        // backticked call `tool(param, param: value)`; every call param must be the tool's.
        var paramsByTool = ToolMethods().ToDictionary(t => t.Name, t => DocParams(t.Method).Select(p => p.Name).ToHashSet());
        var docs = Directory.GetFiles(Path.Combine(Root, "skills"), "*.md", SearchOption.AllDirectories);
        Assert.NotEmpty(docs);

        foreach (var doc in docs)
        {
            var rel = Path.GetRelativePath(Root, doc);

            foreach (Match m in Regex.Matches(File.ReadAllText(doc), @"`([a-z]+(?:_[a-z]+)+|[a-z]+(?=\())(?:\(([^)`]*)\))?`"))
            {
                var tool = m.Groups[1].Value;
                Assert.True(paramsByTool.ContainsKey(tool), $"{rel} names unregistered tool `{tool}`");
                var args = m.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                Assert.All(args.Select(a => a.Split(':', '=')[0].Trim()), p =>
                    Assert.True(paramsByTool[tool].Contains(p), $"{rel} names unregistered parameter `{p}` of `{tool}`"));
            }
        }
    }

    [Fact]
    public void Result_envelope_is_mode_lastHit_and_exception_only()
    {
        // Session-less lists and the detach echo carry no mode; every other
        // result carries Mode + LastHit; execution-shaped results add Exception.
        var results = typeof(IDebugBridge).Assembly.GetTypes()
            .Where(t => t.IsSealed && t.Name.EndsWith("Result", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(results);
        var modeless = results.Where(t => t.GetProperty("Mode") is null)
            .Select(t => t.Name).Order().ToList();
        Assert.Equal(["DetachResult", "ListInstancesResult", "ListProcessesResult", "ListSessionsResult"], modeless);
        Assert.All(results.Where(t => t.GetProperty("Mode") is not null),
            t => Assert.NotNull(t.GetProperty("LastHit")));
        foreach (var name in new[] { "ExecutionResult", "LaunchProcessResult" })
            Assert.NotNull(results.Single(t => t.Name == name).GetProperty("Exception"));
        var envelope = Doc("docs/TOOLS.md");
        Assert.Contains("`mode`", envelope);
        Assert.Contains("`lastHit`", envelope);
        Assert.Contains("`exception`", envelope);
    }
}
