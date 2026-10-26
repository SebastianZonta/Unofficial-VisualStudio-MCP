using VsDebugMcp;

// Seam 1 (tool-call boundary): drive SessionTools.attach against the fake bridge.
// Seam 2 (bridge interface): unknown session ids fail with a named error.
public sealed class AttachTests
{
    [Fact]
    public async Task Attach_returns_session_id_with_mode_and_last_hit_envelope()
    {
        var bridge = new FakeDebugBridge();
        var result = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.SessionId));
        Assert.False(string.IsNullOrWhiteSpace(result.Mode));
        // lastHit is null when nothing has run yet, but the envelope field must exist (JSON null).
        Assert.Null(result.LastHit);
    }

    [Fact]
    public async Task Unknown_session_id_fails_with_named_no_session_error()
    {
        var bridge = new FakeDebugBridge();

        var ex = await Assert.ThrowsAsync<DebugDomainException>(() =>
            bridge.RequireSessionAsync("bogus-session-id", CancellationToken.None));

        Assert.Contains(ErrorCodes.NoSession, ex.Message);
    }

    [Fact]
    public async Task Reattach_to_same_instance_returns_existing_session()
    {
        var bridge = new FakeDebugBridge();
        var first = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        var second = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);

        Assert.Equal(first.SessionId, second.SessionId);
        // The existing session stays usable: attach replaced nothing.
        await bridge.RequireSessionAsync(first.SessionId, CancellationToken.None);
    }
}