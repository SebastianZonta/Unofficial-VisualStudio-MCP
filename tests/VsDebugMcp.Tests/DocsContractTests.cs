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

    private static IReadOnlyList<string> ToolNames() => typeof(IDebugBridge).Assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
        .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
        .OfType<string>()
        .ToList();

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

    [Fact]
    public void E2E_tool_counts_match_the_registered_tools()
    {
        var counts = Regex.Matches(Doc("docs/agents/e2e.md"), @"(?:all|every one of the) (\d+) tools")
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();

        Assert.NotEmpty(counts);
        Assert.All(counts, n => Assert.Equal(ToolNames().Count, n));
    }
}
