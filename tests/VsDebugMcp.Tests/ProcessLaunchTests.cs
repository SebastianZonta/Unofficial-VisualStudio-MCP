using VsDebugMcp;

// Seam 2 (pure helper): the environment block the live bridge hands to CreateProcess.
public sealed class ProcessLaunchTests
{
    [Fact]
    public void Extra_variables_merge_over_inherited_case_insensitively_in_sorted_order()
    {
        var inherited = new Dictionary<string, string> { ["Path"] = @"C:\bin", ["FOO"] = "1" };
        var extra = new Dictionary<string, string> { ["foo"] = "2", ["BAR"] = "x" };

        var block = ProcessLaunch.BuildEnvironmentBlock(inherited, extra);

        Assert.Equal("BAR=x\0foo=2\0Path=C:\\bin\0\0", block);
    }

    [Fact]
    public void No_extra_variables_passes_the_inherited_environment_through()
    {
        var inherited = new Dictionary<string, string> { ["A"] = "1" };

        Assert.Equal("A=1\0\0", ProcessLaunch.BuildEnvironmentBlock(inherited, null));
    }
}
