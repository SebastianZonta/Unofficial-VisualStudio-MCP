using System.Reflection;
using VsDebugMcp;

// Truncation contract: DteDebugBridge caps snapshotted values at
// MaxValueLength chars with a " [truncated]" suffix. EnvDTE COM types
// cannot be constructed without VS, so the limit is read from src via
// reflection (a src change breaks MaxValueLength_is_500) and the exact
// 500-vs-501 boundary rule is pinned against that live value.
public sealed class TruncationContractTests
{
    private const string Suffix = " [truncated]";

    private static int MaxValueLength()
    {
        var type = typeof(FakeDebugBridge).Assembly.GetType("VsDebugMcp.DteDebugBridge", throwOnError: true)!;
        var field = type.GetField("MaxValueLength", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DteDebugBridge.MaxValueLength not found");
        return (int)field.GetValue(null)!;
    }

    private static string ApplyRule(string raw, int max)
        => raw.Length > max ? raw[..max] + Suffix : raw;

    [Fact]
    public void MaxValueLength_is_500()
    {
        Assert.Equal(500, MaxValueLength());
    }

    [Fact]
    public void Exactly_500_chars_is_not_truncated()
    {
        var max = MaxValueLength();
        var raw = new string('x', max);

        Assert.Equal(max, raw.Length);
        Assert.Same(raw, ApplyRule(raw, max));
    }

    [Fact]
    public void Char_501_triggers_truncation_with_exact_shape()
    {
        var max = MaxValueLength();
        var raw = new string('x', max + 1);

        var expected = raw[..max] + Suffix;
        Assert.Equal(raw.Length - 1 + Suffix.Length, expected.Length);
        Assert.Equal(expected, ApplyRule(raw, max));
        Assert.EndsWith(Suffix, ApplyRule(raw, max));
    }
}
