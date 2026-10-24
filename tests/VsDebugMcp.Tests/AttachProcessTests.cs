using ModelContextProtocol;
using VsDebugMcp;

// Seam 1 (tool-call boundary): attach_process consent + ownership gating against the fake bridge.
public sealed class AttachProcessTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync(FakeDebugBridge? bridge = null)
    {
        bridge ??= new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    [Fact]
    public async Task Attach_without_consent_stays_frictionless()
    {
        var (bridge, sessionId) = await AttachedAsync();
        Assert.False(string.IsNullOrWhiteSpace(sessionId));
        _ = bridge;
    }

    [Fact]
    public async Task Process_attach_without_consent_fails_named()
    {
        var bridge = new FakeDebugBridge();
        bridge.ProcessOwners[4321] = "FAKE\\dev"; // valid pid, same user — consent still required first
        var (_, sessionId) = await AttachedAsync(bridge);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            SessionTools.AttachProcessAsync(bridge, sessionId, 4321, consent: false, 30_000, CancellationToken.None));
        Assert.Contains("consent-required", ex.Message);
    }

    [Fact]
    public async Task Process_attach_to_unknown_pid_fails_named()
    {
        var (_, sessionId) = await AttachedAsync();
        var (bridge, _) = (new FakeDebugBridge(), sessionId); // fresh bridge has no known processes
        var attached = await SessionTools.AttachAsync(bridge, null, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            SessionTools.AttachProcessAsync(bridge, attached.SessionId, 99999, consent: true, 30_000, CancellationToken.None));
        Assert.Contains("process-not-found", ex.Message);
    }

    [Fact]
    public async Task Process_attach_to_other_user_fails_named()
    {
        var bridge = new FakeDebugBridge();
        bridge.ProcessOwners[9876] = "OTHER\\eve";
        var (_, sessionId) = await AttachedAsync(bridge);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            SessionTools.AttachProcessAsync(bridge, sessionId, 9876, consent: true, 30_000, CancellationToken.None));
        Assert.Contains("user-mismatch", ex.Message);
    }

    [Fact]
    public async Task Process_attach_same_user_with_consent_succeeds()
    {
        var bridge = new FakeDebugBridge();
        bridge.ProcessOwners[4321] = "FAKE\\dev";
        var (_, sessionId) = await AttachedAsync(bridge);

        var result = await SessionTools.AttachProcessAsync(bridge, sessionId, 4321, consent: true, 30_000, CancellationToken.None);

        Assert.Equal("ok", result.Status);
        Assert.Contains("4321", result.Note);
    }

    [Fact]
    public async Task Second_attach_process_while_bound_fails_named()
    {
        var bridge = new FakeDebugBridge();
        bridge.ProcessOwners[4321] = "FAKE\\dev";
        bridge.ProcessOwners[4322] = "FAKE\\dev";
        var (_, sessionId) = await AttachedAsync(bridge);
        _ = await SessionTools.AttachProcessAsync(bridge, sessionId, 4321, consent: true, 30_000, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            SessionTools.AttachProcessAsync(bridge, sessionId, 4322, consent: true, 30_000, CancellationToken.None));
        Assert.Contains("already-bound", ex.Message);
        Assert.Contains("4321", ex.Message);
    }

    [Fact]
    public async Task Consent_gate_runs_before_the_one_target_check()
    {
        var bridge = new FakeDebugBridge();
        bridge.ProcessOwners[4321] = "FAKE\\dev";
        bridge.ProcessOwners[4322] = "FAKE\\dev";
        var (_, sessionId) = await AttachedAsync(bridge);
        _ = await SessionTools.AttachProcessAsync(bridge, sessionId, 4321, consent: true, 30_000, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            SessionTools.AttachProcessAsync(bridge, sessionId, 4322, consent: false, 30_000, CancellationToken.None));
        Assert.Contains("consent-required", ex.Message);
    }

    [Fact]
    public async Task Same_user_gate_runs_before_the_one_target_check()
    {
        var bridge = new FakeDebugBridge();
        bridge.ProcessOwners[4321] = "FAKE\\dev";
        bridge.ProcessOwners[4322] = "OTHER\\eve";
        var (_, sessionId) = await AttachedAsync(bridge);
        _ = await SessionTools.AttachProcessAsync(bridge, sessionId, 4321, consent: true, 30_000, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            SessionTools.AttachProcessAsync(bridge, sessionId, 4322, consent: true, 30_000, CancellationToken.None));
        Assert.Contains("user-mismatch", ex.Message);
    }

    [Fact]
    public async Task Unknown_session_fails_named_no_session()
    {
        var bridge = new FakeDebugBridge();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            SessionTools.AttachProcessAsync(bridge, "bogus-session-id", 4321, consent: true, 30_000, CancellationToken.None));
        Assert.Contains("no-session", ex.Message);
    }

    [Fact]
    public void Owner_lookup_resolves_the_current_process_to_the_current_user()
    {
        if (!OperatingSystem.IsWindows())
            throw Xunit.Sdk.SkipException.ForSkip("WMI owner lookup is Windows-only.");
        var me = ProcessOwner.CurrentUserName();
        Assert.False(string.IsNullOrWhiteSpace(me));
        Assert.Equal(me, ProcessOwner.OwnerOf(Environment.ProcessId));
    }
}
