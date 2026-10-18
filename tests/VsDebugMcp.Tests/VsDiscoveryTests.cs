using VsDebugMcp;

// Pure seam: ROT display-name selection. No COM needed.
public sealed class VsDiscoveryTests
{
    private static readonly string[] RotNames =
    [
        "VisualStudio.DTE.18.0:1234",
        "!VisualStudio.DTE.17.0:111",
        "!VisualStudio.DTE.18.0:222",
        "!Something.Else:333",
        "!VisualStudio.DTE.18.0:444",
    ];

    [Fact]
    public void Newest_version_wins_without_override()
    {
        var picked = VsDiscovery.SelectCandidate(RotNames, instanceOverride: null);
        Assert.Equal("!VisualStudio.DTE.18.0:222", picked);
    }

    [Fact]
    public void Non_dte_entries_are_ignored()
    {
        var picked = VsDiscovery.SelectCandidate(["!Something.Else:333"], instanceOverride: null);
        Assert.Null(picked);
    }

    [Fact]
    public void Explicit_display_name_override_wins_over_newest()
    {
        var picked = VsDiscovery.SelectCandidate(RotNames, instanceOverride: "!VisualStudio.DTE.17.0:111");
        Assert.Equal("!VisualStudio.DTE.17.0:111", picked);
    }

    [Fact]
    public void ProgId_override_matches_running_instance()
    {
        var picked = VsDiscovery.SelectCandidate(RotNames, instanceOverride: "VisualStudio.DTE.17.0");
        Assert.Equal("!VisualStudio.DTE.17.0:111", picked);
    }

    [Fact]
    public void Unknown_override_selects_nothing()
    {
        var picked = VsDiscovery.SelectCandidate(RotNames, instanceOverride: "VisualStudio.DTE.99.0");
        Assert.Null(picked);
    }

    [Fact]
    public void Empty_rot_selects_nothing()
    {
        Assert.Null(VsDiscovery.SelectCandidate([], instanceOverride: null));
    }
}
