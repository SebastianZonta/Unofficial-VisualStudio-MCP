using ModelContextProtocol;
using VsDebugMcp;

// Seam: exception-config tools driven against the fake bridge (no VS).
public sealed class ExceptionConfigTests
{
    private const string Nre = "System.NullReferenceException";

    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    private static async Task<string?> ModeOfAsync(FakeDebugBridge bridge, string sessionId, string type)
    {
        var list = await ExecutionTools.ListExceptionSettingsAsync(bridge, sessionId, type, CancellationToken.None);
        return list.Settings.SingleOrDefault(s => s.Name == type)?.Mode;
    }

    [Fact]
    public async Task List_returns_seeded_settings_in_design_mode()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var list = await ExecutionTools.ListExceptionSettingsAsync(bridge, sessionId, null, CancellationToken.None);

        Assert.Equal("design", list.Mode);
        Assert.Contains(list.Settings, s => s.Name == Nre && s.Mode == "unhandled-only");
    }

    [Fact]
    public async Task List_works_while_running()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        var list = await ExecutionTools.ListExceptionSettingsAsync(bridge, sessionId, "NullReference", CancellationToken.None);

        Assert.Equal("run", list.Mode);
        Assert.Equal([Nre], list.Settings.Select(s => s.Name));
    }

    [Fact]
    public async Task First_chance_and_unhandled_only_round_trip_through_list()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var set = await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None);
        Assert.Equal("first-chance", set.Setting.Mode);
        Assert.Equal("first-chance", await ModeOfAsync(bridge, sessionId, Nre));

        await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "unhandled-only", CancellationToken.None);
        Assert.Equal("unhandled-only", await ModeOfAsync(bridge, sessionId, Nre));
    }

    [Fact]
    public async Task Default_restores_the_seeded_mode()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None);

        var reset = await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "default", CancellationToken.None);

        Assert.Equal("unhandled-only", reset.Setting.Mode);
        Assert.Equal("unhandled-only", await ModeOfAsync(bridge, sessionId, Nre));
    }

    [Fact]
    public async Task Change_while_running_is_rejected_naming_the_mode()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None));

        Assert.Equal("already-running", ex.Data["code"]);
        Assert.Contains("debuggee is run", ex.Message);
        Assert.Equal("unhandled-only", await ModeOfAsync(bridge, sessionId, Nre));
    }

    [Fact]
    public async Task Change_in_break_mode_is_allowed()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        var set = await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None);

        Assert.Equal("break", set.Mode);
        Assert.Equal("first-chance", set.Setting.Mode);
    }

    [Theory]
    [InlineData("System.NoSuchException")]
    [InlineData("NullReferenceException")] // short names never resolve to a lookalike
    public async Task Unknown_type_name_is_rejected(string type)
    {
        var (bridge, sessionId) = await AttachedAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, type, "first-chance", CancellationToken.None));

        Assert.Equal("exception-not-found", ex.Data["code"]);
    }

    [Fact]
    public async Task Unknown_mode_is_rejected_at_the_tool_boundary()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "always", CancellationToken.None));

        Assert.Contains("invalid-argument", ex.Message);
    }

    [Fact]
    public async Task Settings_never_leak_into_a_sibling_session()
    {
        var bridge = new FakeDebugBridge();
        var a = (await SessionTools.AttachAsync(bridge, "vs-a", CancellationToken.None)).SessionId;
        var b = (await SessionTools.AttachAsync(bridge, "vs-b", CancellationToken.None)).SessionId;

        await ExecutionTools.SetExceptionSettingAsync(bridge, a, Nre, "first-chance", CancellationToken.None);

        Assert.Equal("first-chance", await ModeOfAsync(bridge, a, Nre));
        Assert.Equal("unhandled-only", await ModeOfAsync(bridge, b, Nre));
    }

    [Fact]
    public async Task First_chance_throw_breaks_with_type_path_and_message()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None);
        bridge.ThrowOnNextRun(sessionId, Nre, "Object reference not set to an instance of an object.");

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("break", resumed.Mode);
        Assert.Null(resumed.LastHit); // lastHit stays reserved for breakpoint hits
        Assert.Equal(new ExceptionBreakInfo(Nre, "$exception", "Object reference not set to an instance of an object."), resumed.Exception);
        Assert.Contains(Nre, resumed.Note);
    }

    [Fact]
    public async Task Exception_path_resolves_in_read_variables_and_evaluate()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None);
        bridge.ThrowOnNextRun(sessionId, Nre, "boom");
        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        var vars = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", resumed.Exception!.Path, 50, CancellationToken.None);
        var probe = await InspectionTools.EvaluateAsync(bridge, sessionId, "$exception.Message", 5000, CancellationToken.None);

        Assert.Contains(vars.Variables, v => v.Name == "Message" && v.Value == "boom");
        Assert.Equal("boom", probe.Value);
    }

    [Fact]
    public async Task Handled_throw_under_unhandled_only_does_not_break()
    {
        var (bridge, sessionId) = await AttachedAsync();
        bridge.ThrowOnNextRun(sessionId, Nre, "boom");

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("run", resumed.Mode);
        Assert.Null(resumed.Exception);
    }

    [Fact]
    public async Task Unhandled_throw_breaks_whatever_the_setting()
    {
        var (bridge, sessionId) = await AttachedAsync();
        bridge.ThrowOnNextRun(sessionId, Nre, "boom", handled: false);

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("break", resumed.Mode);
        Assert.Equal(Nre, resumed.Exception?.Type);
        Assert.Contains("unhandled", resumed.Note);
    }

    [Fact]
    public async Task Long_message_is_truncated_at_the_value_limit()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None);
        bridge.ThrowOnNextRun(sessionId, Nre, new string('x', 600));

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal(new string('x', 500) + " [truncated]", resumed.Exception?.Message);
    }

    [Fact]
    public async Task Unreadable_message_is_absent_with_a_note_and_the_break_still_reported()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None);
        bridge.ThrowOnNextRun(sessionId, Nre, message: null); // read refused under nse

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("break", resumed.Mode);
        Assert.Equal("ok", resumed.Status);
        Assert.Equal(Nre, resumed.Exception?.Type);
        Assert.Null(resumed.Exception?.Message);
        Assert.Contains("$exception", resumed.Note);
    }

    [Fact]
    public async Task Launch_stopping_on_an_exception_carries_the_payload()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.SetExceptionSettingAsync(bridge, sessionId, Nre, "first-chance", CancellationToken.None);
        bridge.ThrowOnNextRun(sessionId, Nre, "boom");
        var exe = Path.Combine(Path.GetTempPath(), $"launch-{Guid.NewGuid():N}.exe");
        File.WriteAllText(exe, "");
        File.WriteAllText(Path.ChangeExtension(exe, ".runtimeconfig.json"), "{}");
        try
        {
            var launched = await SessionTools.LaunchProcessAsync(bridge, sessionId, exe);

            Assert.Equal(new ExceptionBreakInfo(Nre, "$exception", "boom"), launched.Exception);
        }
        finally
        {
            File.Delete(exe);
            File.Delete(Path.ChangeExtension(exe, ".runtimeconfig.json"));
        }
    }

    [Fact]
    public async Task Breakpoint_hit_carries_no_exception_payload()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("break", resumed.Mode);
        Assert.Null(resumed.Exception);
    }
}
