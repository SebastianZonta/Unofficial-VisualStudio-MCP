using ModelContextProtocol;
using VsDebugMcp;

// Seam: set_variable at the tool boundary against the fake bridge (no VS).
public sealed class SetVariableTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> BrokenAsync(string? instance = null)
    {
        var bridge = new FakeDebugBridge();
        return (bridge, await BreakIn(bridge, instance));
    }

    private static async Task<string> BreakIn(FakeDebugBridge bridge, string? instance)
    {
        var attach = await SessionTools.AttachAsync(bridge, instance, CancellationToken.None);
        await BreakpointTools.SetBreakpointAsync(bridge, attach.SessionId, "Program.cs", 42, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, attach.SessionId, 5000, CancellationToken.None);
        return attach.SessionId;
    }

    private static async Task<string> TotalAsync(FakeDebugBridge bridge, string sessionId, string frameId = "f0")
    {
        var vars = await InspectionTools.ReadVariablesAsync(bridge, sessionId, frameId, null, 50, CancellationToken.None);
        return Assert.Single(vars.Variables, v => v.Name == "total").Value;
    }

    [Fact]
    public async Task Consented_write_round_trips_through_read_variables()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var set = await InspectionTools.SetVariableAsync(bridge, sessionId, "f0", "total", "0.01", consent: true, CancellationToken.None);

        Assert.Equal(("f0", "total", "0.01", "decimal", "break"), (set.FrameId, set.Path, set.Value, set.Type, set.Mode));
        Assert.Equal("0.01", await TotalAsync(bridge, sessionId));
        Assert.Equal("99.00", await TotalAsync(bridge, sessionId, "f1"));
    }

    [Fact]
    public async Task Read_only_target_is_named_and_writes_nothing()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, sessionId, "f1", "items.Count", "4", consent: true, CancellationToken.None));

        Assert.Contains("read-only-target", ex.Message);
        var drilled = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f1", "items", 50, CancellationToken.None);
        Assert.Equal("3", Assert.Single(drilled.Variables, v => v.Name == "items.Count").Value);
    }

    [Fact]
    public async Task Unsupported_target_type_fails_closed_as_type_mismatch()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, sessionId, "f1", "items[0]", "null", consent: true, CancellationToken.None));

        Assert.Contains("type-mismatch", ex.Message);
        Assert.Contains("Item", ex.Message);
    }

    [Fact]
    public async Task Missing_consent_fails_closed_with_state_unchanged()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, sessionId, "f0", "total", "0.01", consent: false, CancellationToken.None));

        Assert.Contains("consent-required", ex.Message);
        Assert.Equal("41.80", await TotalAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Attach_process_consent_never_authorizes_a_write()
    {
        var (bridge, sessionId) = await BrokenAsync();
        bridge.ProcessOwners[4242] = bridge.CurrentUser;
        await SessionTools.AttachProcessAsync(bridge, sessionId, 4242, consent: true, 30000, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, sessionId, "f0", "total", "0.01", consent: false, CancellationToken.None));

        Assert.Contains("consent-required", ex.Message);
    }

    [Fact]
    public async Task Consent_is_checked_before_mode()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, null, CancellationToken.None); // design mode

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, attach.SessionId, "f0", "total", "0.01", consent: false, CancellationToken.None));

        Assert.Contains("consent-required", ex.Message);
    }

    [Fact]
    public async Task Write_outside_break_mode_is_a_mode_error()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, null, CancellationToken.None);

        var design = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, attach.SessionId, "f0", "total", "0.01", consent: true, CancellationToken.None));
        Assert.Contains("not-in-break-mode", design.Message);

        await ExecutionTools.ResumeAsync(bridge, attach.SessionId, 5000, CancellationToken.None); // no breakpoints: runs
        var run = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, attach.SessionId, "f0", "total", "0.01", consent: true, CancellationToken.None));
        Assert.Contains("not-in-break-mode", run.Message);
    }

    [Fact]
    public async Task Type_mismatch_names_expected_and_provided_and_writes_nothing()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, sessionId, "f0", "total", "\"cheap\"", consent: true, CancellationToken.None));

        Assert.Contains("type-mismatch", ex.Message);
        Assert.Contains("decimal", ex.Message);
        Assert.Contains("\"cheap\"", ex.Message);
        Assert.Equal("41.80", await TotalAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Unknown_path_and_frame_are_named_and_write_nothing()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var path = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, sessionId, "f0", "nope", "1", consent: true, CancellationToken.None));
        Assert.Contains("path-not-found", path.Message);
        Assert.Contains("nope", path.Message);
        Assert.Contains("f0", path.Message);

        var frame = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.SetVariableAsync(bridge, sessionId, "f99", "total", "1", consent: true, CancellationToken.None));
        Assert.Contains("frame-not-found", frame.Message);
    }

    [Fact]
    public async Task Write_never_leaks_into_a_sibling_debug_session()
    {
        var bridge = new FakeDebugBridge();
        var a = await BreakIn(bridge, "vs-a");
        var b = await BreakIn(bridge, "vs-b");

        await InspectionTools.SetVariableAsync(bridge, a, "f0", "total", "0.01", consent: true, CancellationToken.None);

        Assert.Equal("0.01", await TotalAsync(bridge, a));
        Assert.Equal("41.80", await TotalAsync(bridge, b));
    }

    [Fact]
    public async Task Tool_boundary_rejects_empty_and_write_like_arguments()
    {
        var (bridge, sessionId) = await BrokenAsync();

        foreach (var (path, value) in new[] { (" ", "1"), ("total", " "), ("total = 0", "1"), ("total++", "1") })
        {
            var ex = await Assert.ThrowsAsync<McpException>(() =>
                InspectionTools.SetVariableAsync(bridge, sessionId, "f0", path, value, consent: true, CancellationToken.None));
            Assert.Contains("invalid-argument", ex.Message);
        }
        Assert.Equal("41.80", await TotalAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Evaluate_stays_read_only()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, "total = 0.01m", 5000, CancellationToken.None));

        Assert.Contains("invalid-argument", ex.Message);
        Assert.Equal("41.80", await TotalAsync(bridge, sessionId));
    }

    // Seam: strict coercion shared by both bridges. Expected literals are worked by hand.
    [Theory]
    [InlineData("int", "42", "42")]
    [InlineData("int", "-7", "-7")]
    [InlineData("int?", "null", "null")]
    [InlineData("long", "9000000000", "9000000000L")]
    [InlineData("byte", "255", "(byte)255")]
    [InlineData("bool", "True", "true")]
    [InlineData("decimal", "41.80", "41.80M")]
    [InlineData("double", "1.5", "1.5D")]
    [InlineData("float", "0.25", "0.25F")]
    [InlineData("char", "'a'", "'a'")]
    [InlineData("char", "97 'a'", "'a'")]   // VS's display form round-trips
    [InlineData("string", "\"hi \\\"you\\\"\"", "\"hi \\\"you\\\"\"")]
    [InlineData("string", "null", "null")]
    public void Coerce_accepts_exact_values(string type, string value, string literal) =>
        Assert.Equal(literal, VariableWrite.Coerce(type, value));

    [Theory]
    [InlineData("int", "2147483648")]   // overflow, never wrapped
    [InlineData("int", "1.5")]          // no truncation
    [InlineData("int", "1,000")]        // no thousands separators
    [InlineData("byte", "-1")]
    [InlineData("decimal", "1,5")]      // no locale-dependent decimal comma
    [InlineData("decimal", "1e3")]
    [InlineData("double", "NaN")]
    [InlineData("bool", "1")]
    [InlineData("int", "null")]         // not nullable
    [InlineData("char", "'ab'")]
    [InlineData("char", "98 'a'")]      // code and glyph disagree
    [InlineData("string", "hi")]        // unquoted
    [InlineData("string", "\"a\"b\"")]  // bare inner quote
    [InlineData("string", "\"a\\\"")]   // escaped closing quote
    [InlineData("Item", "null")]        // unsupported type: fail closed
    public void Coerce_rejects_mismatches_with_type_mismatch(string type, string value)
    {
        var ex = Assert.Throws<DebugDomainException>(() => VariableWrite.Coerce(type, value));
        Assert.Equal("type-mismatch", ex.Code);
    }

    [Theory]
    [InlineData("41.80", "41.8M", true)]
    [InlineData("255", "(byte)255", true)]
    [InlineData("-3", "-3L", true)]
    [InlineData("true", "true", true)]
    [InlineData("97 'a'", "'a'", true)]
    [InlineData("\"a\"", "\"a\"", true)]
    [InlineData("\"a\"", "\"A\"", false)]
    [InlineData("41.80", "41.81M", false)]
    [InlineData(null, "1", false)]
    public void Same_shown_value_compares_the_display_form(string? shown, string literal, bool same) =>
        Assert.Equal(same, VariableWrite.IsSameShownValue(shown, literal));

    [Fact]
    public async Task Echo_is_the_value_as_shown_not_the_agents_spelling()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var set = await InspectionTools.SetVariableAsync(bridge, sessionId, "f0", "total", " 7.50 ", consent: true, CancellationToken.None);

        Assert.Equal("7.50", set.Value);
        Assert.Equal("7.50", await TotalAsync(bridge, sessionId));
    }
}
