using ModelContextProtocol;
using VsDebugMcp;

// Seam 1 (tool-call boundary): launch_process session binding and start rules against the fake bridge.
public sealed class LaunchProcessTests : IDisposable
{
    private readonly string _exe = Path.Combine(Path.GetTempPath(), $"launch-{Guid.NewGuid():N}.exe");

    public LaunchProcessTests()
    {
        File.WriteAllText(_exe, "");
        File.WriteAllText(Path.ChangeExtension(_exe, ".runtimeconfig.json"), "{}");
    }

    public void Dispose()
    {
        File.Delete(_exe);
        File.Delete(Path.ChangeExtension(_exe, ".runtimeconfig.json"));
    }

    [Fact]
    public async Task Non_dotnet_executable_is_refused_before_start()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var native = Path.Combine(Path.GetTempPath(), $"native-{Guid.NewGuid():N}.exe");
        File.WriteAllText(native, "");
        try
        {
            var ex = await Assert.ThrowsAsync<McpException>(() => SessionTools.LaunchProcessAsync(bridge, sessionId, native));
            Assert.Contains("invalid-argument", ex.Message);
            Assert.Contains(".NET", ex.Message);

            Assert.Equal("ok", (await SessionTools.LaunchProcessAsync(bridge, sessionId, _exe)).Status);
        }
        finally
        {
            File.Delete(native);
        }
    }

    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    [Fact]
    public async Task Launch_returns_a_target_pid_bound_to_the_session()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var result = await SessionTools.LaunchProcessAsync(bridge, sessionId, _exe);

        Assert.True(result.ProcessId > 0);
        Assert.Equal("ok", result.Status);
        Assert.Equal("run", result.Mode);
    }

    [Fact]
    public async Task Breakpoint_set_before_launch_hits_on_startup()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var bp = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 1, CancellationToken.None);

        var result = await SessionTools.LaunchProcessAsync(bridge, sessionId, _exe);

        Assert.Equal("break", result.Mode);
        Assert.Contains("Program.cs:1", result.LastHit);
        Assert.Contains(bp.Breakpoint.Id, result.LastHit);
    }

    [Fact]
    public async Task Missing_working_directory_fails_named_and_leaves_the_session_unbound()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var missingDir = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            SessionTools.LaunchProcessAsync(bridge, sessionId, _exe, workingDirectory: missingDir));
        Assert.Contains("invalid-argument", ex.Message);
        Assert.Contains("working directory", ex.Message);

        Assert.Equal("ok", (await SessionTools.LaunchProcessAsync(bridge, sessionId, _exe)).Status);
    }

    [Fact]
    public async Task Missing_executable_fails_named_and_leaves_the_session_unbound()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe");

        var ex = await Assert.ThrowsAsync<McpException>(() => SessionTools.LaunchProcessAsync(bridge, sessionId, missing));
        Assert.Contains("executable-not-found", ex.Message);

        var retry = await SessionTools.LaunchProcessAsync(bridge, sessionId, _exe);
        Assert.Equal("ok", retry.Status);
    }

    [Fact]
    public async Task Launching_devenv_is_refused_and_leaves_the_session_unbound()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var dir = Directory.CreateTempSubdirectory().FullName;
        var devenv = Path.Combine(dir, "DevEnv.exe");
        File.WriteAllText(devenv, "");
        try
        {
            var ex = await Assert.ThrowsAsync<McpException>(() => SessionTools.LaunchProcessAsync(bridge, sessionId, devenv));
            Assert.Contains("invalid-argument", ex.Message);
            Assert.Contains("VS instance", ex.Message);

            Assert.Equal("ok", (await SessionTools.LaunchProcessAsync(bridge, sessionId, _exe)).Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Launch_while_a_target_process_is_bound_fails_named()
    {
        var (bridge, sessionId) = await AttachedAsync();
        _ = await SessionTools.LaunchProcessAsync(bridge, sessionId, _exe);

        var ex = await Assert.ThrowsAsync<McpException>(() => SessionTools.LaunchProcessAsync(bridge, sessionId, _exe));
        Assert.Contains("already-bound", ex.Message);
    }

    [Fact]
    public async Task Relative_executable_path_is_rejected_at_the_tool_boundary()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() => SessionTools.LaunchProcessAsync(bridge, sessionId, "app.exe"));
        Assert.Contains("invalid-argument", ex.Message);
    }

    [Fact]
    public async Task Unknown_session_fails_named_no_session()
    {
        var bridge = new FakeDebugBridge();

        var ex = await Assert.ThrowsAsync<McpException>(() => SessionTools.LaunchProcessAsync(bridge, "bogus-session-id", _exe));
        Assert.Contains("no-session", ex.Message);
    }

    [Fact]
    public async Task Launch_after_attach_process_fails_named()
    {
        var (bridge, sessionId) = await AttachedAsync();
        bridge.ProcessOwners[4321] = "FAKE\\dev";
        _ = await SessionTools.AttachProcessAsync(bridge, sessionId, 4321, consent: true);

        var ex = await Assert.ThrowsAsync<McpException>(() => SessionTools.LaunchProcessAsync(bridge, sessionId, _exe));
        Assert.Contains("already-bound", ex.Message);
    }
}
