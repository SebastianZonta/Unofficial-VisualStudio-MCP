using VsDebugMcp;

// Truncation contract: snapshotted values are capped at
// MaxValueLength chars with a " [truncated]" suffix. EnvDTE COM types
// cannot be constructed without VS, so this pins the shared rule
// (ExceptionConfig.Truncate) directly, plus the exact 500-vs-501 boundary.
public sealed class TruncationContractTests
{
    private const string Suffix = " [truncated]";

    [Fact]
    public void MaxValueLength_is_500()
    {
        Assert.Equal(500, ExceptionConfig.MaxValueLength);
    }

    [Fact]
    public void Exactly_500_chars_is_not_truncated()
    {
        var raw = new string('x', ExceptionConfig.MaxValueLength);

        Assert.Equal(ExceptionConfig.MaxValueLength, raw.Length);
        Assert.Same(raw, ExceptionConfig.Truncate(raw));
    }

    [Fact]
    public void Char_501_triggers_truncation_with_exact_shape()
    {
        var max = ExceptionConfig.MaxValueLength;
        var raw = new string('x', max + 1);

        var expected = raw[..max] + Suffix;
        var actual = ExceptionConfig.Truncate(raw);
        Assert.Equal(raw.Length - 1 + Suffix.Length, actual!.Length);
        Assert.Equal(expected, actual);
        Assert.EndsWith(Suffix, actual);
    }
}