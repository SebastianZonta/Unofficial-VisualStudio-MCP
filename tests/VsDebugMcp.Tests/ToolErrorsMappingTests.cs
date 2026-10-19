using ModelContextProtocol;
using VsDebugMcp;

// ToolErrors seam: bridge-seam domain failures become McpException;
// cancellation and unexpected failures pass through untouched.
public sealed class ToolErrorsMappingTests
{
    [Fact]
    public async Task Domain_failure_maps_to_McpException_preserving_code()
    {
        var domain = new DebugDomainException("bogus-code", "something broke", "do the fix");

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ToolErrors.MapAsync<int>(() => Task.FromException<int>(domain)));

        Assert.Contains("bogus-code", ex.Message);
        Assert.Same(domain, ex.InnerException);
        Assert.Equal("bogus-code", ex.Data[ErrorCodes.DataKey]);
    }

    [Fact]
    public async Task OperationCanceled_passes_through_unwrapped()
    {
        var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ToolErrors.MapAsync<int>(() => Task.FromCanceled<int>(new CancellationToken(canceled: true))));

        Assert.IsNotType<McpException>(canceled);
    }

    [Fact]
    public async Task Unexpected_failure_passes_through_untouched()
    {
        var boom = new InvalidOperationException("boom");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ToolErrors.MapAsync<int>(() => Task.FromException<int>(boom)));

        Assert.Same(boom, ex);
    }

    [Fact]
    public async Task Success_value_passes_through()
    {
        var result = await ToolErrors.MapAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }
}
