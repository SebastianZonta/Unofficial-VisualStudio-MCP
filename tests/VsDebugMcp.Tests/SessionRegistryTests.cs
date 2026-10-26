using System.Reflection;
using VsDebugMcp;

// Direct SessionRegistry cover: mint ids, logpoint specs, session gating.
// No live VS: the DTE is a DispatchProxy stub built through the parameter
// type itself (EnvDTE flows only into src, so GetOrAdd/MarkDead/MintOwned
// go through MethodInfo); the Breakpoint slot is never touched, so null suffices.
public sealed class SessionRegistryTests
{
    private class StubDte : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;
            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    private static readonly MethodInfo GetOrAddMethod = typeof(SessionRegistry).GetMethod("GetOrAdd")!;
    private static readonly MethodInfo MintMethod = typeof(SessionRegistry).GetMethod("MintOwned")!;
    private static readonly MethodInfo MarkDeadMethod = typeof(SessionRegistry).GetMethod("MarkDead")!;

    private static object NewDte()
    {
        var dteType = GetOrAddMethod.GetParameters()[1].ParameterType;
        return typeof(DispatchProxy).GetMethods()
            .Single(m => m.Name == "Create" && m.GetParameters().Length == 0)
            .MakeGenericMethod(dteType, typeof(StubDte)).Invoke(null, null)!;
    }

    private static string Attached(SessionRegistry registry, object dte)
    {
        var args = new object?[] { "VS [fake]", dte, 1234, null };
        var sessionId = (string)GetOrAddMethod.Invoke(registry, args)!;

        Assert.True((bool)args[3]!);
        return sessionId;
    }

    private static string Mint(SessionRegistry registry, string sessionId, string file, int line, string? logMessage)
    {
        try
        {
            return (string)MintMethod.Invoke(registry, [sessionId, null, file, line, logMessage])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    [Fact]
    public void Mint_increments_bp_ids_and_never_reuses()
    {
        var registry = new SessionRegistry();
        var sessionId = Attached(registry, NewDte());

        Assert.Equal("bp1", Mint(registry, sessionId, "A.cs", 1, null));
        Assert.Equal("bp2", Mint(registry, sessionId, "B.cs", 2, null));

        registry.Forget(sessionId, ["bp1"]);

        Assert.Equal("bp3", Mint(registry, sessionId, "C.cs", 3, null));
    }

    [Fact]
    public void Mint_records_logpoint_spec_only_when_log_message_present()
    {
        var registry = new SessionRegistry();
        var sessionId = Attached(registry, NewDte());

        var plain = Mint(registry, sessionId, "A.cs", 1, null);
        var logged = Mint(registry, sessionId, "B.cs", 2, "hit {x}");

        var (_, specs, _) = registry.SnapshotOwned(sessionId);

        Assert.DoesNotContain(specs, s => s.Id == plain);
        var spec = Assert.Single(specs, s => s.Id == logged);
        Assert.Equal("B.cs", spec.File);
        Assert.Equal(2, spec.Line);
        Assert.Equal("hit {x}", spec.LogMessage);
    }

    [Fact]
    public void Mint_unknown_session_throws_no_session()
    {
        var registry = new SessionRegistry();

        var ex = Assert.Throws<DebugDomainException>(() =>
            Mint(registry, "bogus-session-id", "A.cs", 1, null));
        Assert.Contains(ErrorCodes.NoSession, ex.Message);
    }

    [Fact]
    public void Mint_dead_binding_throws_vs_not_running()
    {
        var registry = new SessionRegistry();
        var dte = NewDte();
        var sessionId = Attached(registry, dte);

        MarkDeadMethod.Invoke(registry, [sessionId, dte]);

        var ex = Assert.Throws<DebugDomainException>(() =>
            Mint(registry, sessionId, "A.cs", 1, null));
        Assert.Contains(ErrorCodes.VsNotRunning, ex.Message);
    }
}