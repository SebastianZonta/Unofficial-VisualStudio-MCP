using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VsDebugMcp;

// Required UTs for .scratch/audit-oct24-medium/spec.md (fake bridge, CI-safe).
// One test per spec bullet; live-only steps (engine bind, kill-race) stay in e2e.md.
public sealed class AuditMediumTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync(FakeDebugBridge? bridge = null)
    {
        bridge ??= new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    private static async Task<(FakeDebugBridge Bridge, string SessionId)> BrokenAsync()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await bridge.SetBreakpointAsync(sessionId, "Program.cs", 42, CancellationToken.None);
        var run = await bridge.ResumeAsync(sessionId, 5000, CancellationToken.None);
        Assert.Equal("break", run.Mode);
        return (bridge, sessionId);
    }

    [Fact]
    public async Task Bridge_rejects_nonpositive_line()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var ex = await Assert.ThrowsAsync<DebugDomainException>(() =>
            bridge.SetBreakpointAsync(sessionId, "Program.cs", 0, CancellationToken.None));
        Assert.Contains("invalid-argument", ex.Message);
        Assert.Empty((await bridge.ListBreakpointsAsync(sessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task Step_reports_lastHit_both_paths()
    {
        var (bridge, sessionId) = await BrokenAsync();
        var fast = await bridge.StepAsync(sessionId, "over", 5000, CancellationToken.None);
        bridge.ForceTimeout = true;
        var slow = await bridge.StepAsync(sessionId, "over", 5000, CancellationToken.None);
        Assert.Equal(fast.LastHit, slow.LastHit);
        Assert.Matches(@"^.+:\d+$", fast.LastHit ?? "");
        Assert.DoesNotContain("bp", fast.LastHit ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Live_shape_only()
    {
        Assert.Null(typeof(SymbolStates).GetField("Failed"));
        var dte = ReadRepoFile("src", "VsDebugMcp", "DteDebugBridge.cs");
        Assert.DoesNotContain("SymbolStates.Failed", dte);
        Assert.DoesNotContain("Loaded: false", dte);
    }

    [Fact]
    public async Task Frames_carry_no_file_line()
    {
        var (bridge, sessionId) = await BrokenAsync();
        var stack = await bridge.ReadStackAsync(sessionId, 10, CancellationToken.None);
        Assert.NotEmpty(stack.Frames);
        Assert.All(stack.Frames, f => Assert.Null(f.File));
        Assert.All(stack.Frames, f => Assert.Null(f.Line));
    }

    [Fact]
    public void Char_space_glyph_roundtrips()
    {
        Assert.Equal("' '", VariableWrite.Coerce("char", "32 ' '"));
        Assert.True(VariableWrite.IsSameShownValue("32 ' '", "' '"));
    }

    [Fact]
    public void Refused_when_before_unknown_and_disconnect_propagates()
    {
        // Unknown-before re-read that differs from the literal is a refusal, never false success.
        Assert.False(VariableWrite.IsSameShownValue("0", "'a'"));
        var disconnected = new COMException("gone", unchecked((int)0x80010108));
        Assert.True(ComErrorMapper.IsDisconnected(disconnected));
        Assert.False(ComErrorMapper.IsRefusal(disconnected));
    }

    [Fact]
    public void Unicode_and_comment_calls_rejected()
    {
        foreach (var s in new[] { "a\u2028b", "a\u2029b", "a\vb", "a\fc", "a\u0085b", "foo /*c*/ (1)", "a // b" })
            Assert.NotNull(BreakpointOptions.CheckExpression(s));
        Assert.Null(BreakpointOptions.CheckExpression("x => x.Id>0"));
        Assert.Null(BreakpointOptions.CheckExpression("(i==3)"));
        var stripped = BreakpointOptions.StripBlockComments("x /*c*/ => x.Id>0");
        Assert.DoesNotContain("/*", stripped, StringComparison.Ordinal);
        Assert.Contains("=>", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Indexer_drill_path_roundtrips()
    {
        var (bridge, sessionId) = await BrokenAsync();
        var drilled = await bridge.ReadVariablesAsync(sessionId, "f0", "items[0]", 10, CancellationToken.None);
        Assert.NotEmpty(drilled.Variables);
        Assert.DoesNotContain(".[", drilled.Path ?? "", StringComparison.Ordinal);
        var eval = await bridge.EvaluateAsync(sessionId, "items[0]", 5000, CancellationToken.None);
        Assert.Equal("items[0]", eval.Expression);
    }

    [Fact]
    public void Single_prefix_and_mapped_timeout()
    {
        var busy = ComErrorMapper.ToDomain(new COMException("busy", unchecked((int)0x80010001)));
        Assert.StartsWith("vs-busy", busy.Message);
        Assert.Equal(1, Regex.Matches(busy.Message, "vs-busy").Count);
        var dte = ReadRepoFile("src", "VsDebugMcp", "DteDebugBridge.cs");
        Assert.Contains("attaching to the VS instance exceeded", dte);
    }

    [Fact]
    public void Only_genuine_refusal_is_readonly()
    {
        Assert.True(ComErrorMapper.IsRefusal(new COMException("refused", unchecked((int)0x80004005))));
        Assert.False(ComErrorMapper.IsRefusal(new COMException("busy", unchecked((int)0x80010001))));
        Assert.True(ComErrorMapper.IsCallRejected(new COMException("busy", unchecked((int)0x80010001))));
    }

    [Fact]
    public void Launch_helpers_fail_closed()
    {
        Assert.Throws<DebugDomainException>(() => ProcessLaunch.CheckStart("foo.exe", null));
        var self = Environment.ProcessPath;
        Assert.False(string.IsNullOrEmpty(self));
        var cwdEx = Assert.Throws<DebugDomainException>(() => ProcessLaunch.CheckStart(self!, ""));
        Assert.Contains("invalid-argument", cwdEx.Message);
        var inherited = new Dictionary<string, string> { ["A"] = "1" };
        Assert.Throws<DebugDomainException>(() => ProcessLaunch.BuildEnvironmentBlock(
            inherited, new Dictionary<string, string> { ["B=C"] = "x" }));
        Assert.Throws<DebugDomainException>(() => ProcessLaunch.BuildEnvironmentBlock(
            inherited, new Dictionary<string, string> { ["B\0"] = "x" }));
        Assert.Throws<DebugDomainException>(() => ProcessLaunch.BuildEnvironmentBlock(
            inherited, new Dictionary<string, string> { ["B"] = null! }));
        // Unknown owner fails closed (WMI miss or hosting failure both read null).
        Assert.Null(ProcessOwner.OwnerOf(-1));
    }

    [Fact]
    public async Task Bridge_validates_mode_and_truncates()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var modeEx = await Assert.ThrowsAsync<DebugDomainException>(() =>
            bridge.SetExceptionSettingAsync(sessionId, "System.InvalidOperationException", "always", CancellationToken.None));
        Assert.Contains("invalid-argument", modeEx.Message);
        bridge.ThrowOnNextRun(sessionId, "System.InvalidOperationException", new string('x', 600), handled: false);
        var run = await bridge.ResumeAsync(sessionId, 5000, CancellationToken.None);
        Assert.Equal("break", run.Mode);
        Assert.NotNull(run.Exception?.Message);
        Assert.EndsWith("[truncated]", run.Exception!.Message);
        Assert.True(run.Exception.Message.Length <= 500 + " [truncated]".Length);
        await bridge.SetExceptionSettingAsync(sessionId, "System.InvalidOperationException",
            ExceptionBreakModes.FirstChance, CancellationToken.None);
        await bridge.SetExceptionSettingAsync(sessionId, "System.InvalidOperationException",
            ExceptionBreakModes.Default, CancellationToken.None);
        var list = await bridge.ListExceptionSettingsAsync(sessionId, "InvalidOperation", CancellationToken.None);
        Assert.Equal(ExceptionBreakModes.UnhandledOnly, Assert.Single(list.Settings).Mode);
    }

    [Fact]
    public async Task Attach_fails_closed_on_recycled_pid()
    {
        var bridge = new FakeDebugBridge();
        bridge.ProcessOwners[4321] = "FAKE\\dev";
        bridge.Processes.Add(new TargetProcessInfo(4321, "Sandbox", "FAKE\\dev", true, null));
        var (_, sessionId) = await AttachedAsync(bridge);
        var ok = await SessionTools.AttachProcessAsync(bridge, sessionId, 4321,
            consent: true, 30_000, expectedProcessName: "Sandbox", cancellationToken: CancellationToken.None);
        Assert.Equal("ok", ok.Status);
        var bridge2 = new FakeDebugBridge();
        bridge2.ProcessOwners[4321] = "FAKE\\dev";
        bridge2.Processes.Add(new TargetProcessInfo(4321, "Sandbox", "FAKE\\dev", true, null));
        var (_, session2) = await AttachedAsync(bridge2);
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() =>
            SessionTools.AttachProcessAsync(bridge2, session2, 4321,
                consent: true, 30_000, expectedProcessName: "Other", cancellationToken: CancellationToken.None));
        Assert.Contains("process-not-found", ex.Message);
    }

    private static string ReadRepoFile(params string[] relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var parts = new List<string> { dir.FullName };
            parts.AddRange(relative);
            var candidate = Path.Combine(parts.ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            if (File.Exists(Path.Combine(dir.FullName, "VsDebugMcp.slnx")))
            {
                var inner = new List<string> { dir.FullName, "Unofficial-VisualStudio-MCP" };
                inner.AddRange(relative);
                candidate = Path.Combine(inner.ToArray());
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"repo file not found: {string.Join('/', relative)}");
    }
}
