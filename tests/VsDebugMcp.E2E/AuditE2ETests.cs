// Live regression guard for the audit-should-fix spec (plus the must-fix behaviours
// the manual pass used to own): one E2E case per behaviour, driven over MCP stdio
// against the real server binary in live mode. Uses the in-repo Sandbox target;
// markers resolve by name, never by hardcoded line. Opt-in: skips without VSDEBUG_E2E=1.
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace VsDebugMcp.E2E;

[Collection("e2e")]
public sealed class AuditE2ETests(E2EFixture fx)
{
    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    private static string Prop(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var exact))
            return exact.ValueKind == JsonValueKind.Null ? "" : exact.ToString();
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return prop.Value.ValueKind == JsonValueKind.Null ? "" : prop.Value.ToString();
        }
        throw new InvalidOperationException($"property {name} missing in {el}");
    }

    private static JsonElement ArrayProp(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var exact))
            return exact;
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }
        throw new InvalidOperationException($"property {name} missing in {el}");
    }

    private static CancellationToken TestCt(TimeSpan? budget = null) =>
        new CancellationTokenSource(budget ?? TimeSpan.FromMinutes(5)).Token;

    /// <summary>Attach (default or VS A); skips when no VS instance is reachable in this config.</summary>
    private async Task<string> AttachAsync(CancellationToken ct, string? instanceOverride = null)
    {
        var want = instanceOverride ?? fx.VsA;
        var args = want is null ? new Dictionary<string, object?>() : Args(("instanceOverride", want));
        try
        {
            var result = await E2EFixture.CallAsync(fx.Client, "attach", args, ct);
            return Prop(result, "sessionId");
        }
        catch (E2EToolException ex) when (ex.Code is "vs-not-running" or "timeout")
        {
            throw new InvalidOperationException($"no live VS reachable in this config ({ex.Code}); start VS 2026 and retry.", ex);
        }
    }

    private async Task DetachAsync(string sessionId, CancellationToken ct)
    {
        try { await E2EFixture.CallAsync(fx.Client, "detach", Args(("sessionId", sessionId)), ct); }
        catch { /* best effort */ }
    }

    [E2EFact]
    public async Task ParallelAttach_SameInstance_SameSession()
    {
        E2EFixture.Gate();
        var ct = TestCt();
        await E2EFixture.WithCleanupAsync<object?>(async cleanups =>
        {
            var first = await AttachAsync(ct);
            cleanups.Add(() => DetachAsync(first, CancellationToken.None));
            var parallel = await Task.WhenAll(AttachAsync(ct), AttachAsync(ct));
            Assert.Equal(first, parallel[0]);
            Assert.Equal(first, parallel[1]);
            var sessions = await E2EFixture.CallAsync(fx.Client, "list_sessions", null, ct);
            Assert.Single(ArrayProp(sessions, "sessions").EnumerateArray(), s => Prop(s, "sessionId") == first);
            if (fx.VsB is not null)
            {
                var other = await AttachAsync(ct, fx.VsB);
                cleanups.Add(() => DetachAsync(other, CancellationToken.None));
                Assert.NotEqual(first, other);
            }
            return null;
        });
    }

    [E2EFact]
    public async Task EvaluateBudget_FullTimeoutMsHonored()
    {
        E2EFixture.Gate();
        var ct = TestCt();
        await E2EFixture.WithCleanupAsync<object?>(async cleanups =>
        {
            var sessionId = await AttachAsync(ct);
            cleanups.Add(() => DetachAsync(sessionId, CancellationToken.None));
            var file = fx.SandboxScenarios;
            var set = await E2EFixture.CallAsync(fx.Client, "set_breakpoint",
                Args(("sessionId", sessionId), ("file", file), ("line", fx.MarkerLine("INSPECT"))), ct);
            var bp = Prop(ArrayProp(set, "breakpoint"), "id");
            cleanups.Add(async () =>
            {
                try { await E2EFixture.CallAsync(fx.Client, "remove_breakpoint", Args(("sessionId", sessionId), ("breakpointId", bp)), CancellationToken.None); } catch { }
            });
            var launch = await E2EFixture.CallAsync(fx.Client, "launch_process",
                Args(("sessionId", sessionId), ("executablePath", fx.SandboxExe), ("arguments", "inspect"), ("timeoutMs", 30000)), ct);
            Assert.Equal("break", E2EFixture.ModeOf(launch));
            // The documented 1000-60000 budget is the real one: 45 s is accepted and succeeds.
            var eval = await E2EFixture.CallAsync(fx.Client, "evaluate",
                Args(("sessionId", sessionId), ("expression", "order.Id"), ("timeoutMs", 45000)), ct);
            Assert.Equal("1001", Prop(eval, "value"));
            var rejected = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "evaluate",
                Args(("sessionId", sessionId), ("expression", "order.Id"), ("timeoutMs", 60001)), ct));
            Assert.Equal("invalid-argument", rejected.Code);
            cleanups.Add(async () =>
            {
                try { await E2EFixture.CallAsync(fx.Client, "resume", Args(("sessionId", sessionId), ("timeoutMs", 30000)), CancellationToken.None); } catch { }
            });
            return null;
        });
    }

    [E2EFact]
    public async Task OneTargetRule_BothDirections()
    {
        E2EFixture.Gate();
        var ct = TestCt();
        await E2EFixture.WithCleanupAsync<object?>(async cleanups =>
        {
            var sessionId = await AttachAsync(ct);
            cleanups.Add(() => DetachAsync(sessionId, CancellationToken.None));
            using var wait = Process.Start(new ProcessStartInfo(fx.SandboxExe, "wait 900")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("could not start the Sandbox wait target.");
            cleanups.Add(() => { try { if (!wait.HasExited) wait.Kill(); } catch { } return Task.CompletedTask; });
            var waitPid = wait.Id;
            // Trust gate first: consent and identity are evaluated before the one-target check.
            var consent = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "attach_process",
                Args(("sessionId", sessionId), ("processId", waitPid), ("consent", false), ("timeoutMs", 30000)), ct));
            Assert.Equal("consent-required", consent.Code);
            var attached = await E2EFixture.CallAsync(fx.Client, "attach_process",
                Args(("sessionId", sessionId), ("processId", waitPid), ("consent", true), ("timeoutMs", 30000)), ct);
            Assert.Equal("ok", Prop(attached, "status"));
            var relaunch = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "launch_process",
                Args(("sessionId", sessionId), ("executablePath", fx.SandboxExe), ("arguments", "exit"), ("timeoutMs", 30000)), ct));
            Assert.Equal("already-bound", relaunch.Code);
            Assert.Contains(waitPid.ToString(), relaunch.Text);
            // Still-running resume is success (no ghost report), never a hang.
            var paused = await E2EFixture.CallAsync(fx.Client, "pause", Args(("sessionId", sessionId)), ct);
            Assert.Equal("break", E2EFixture.ModeOf(paused));
            var stack = await E2EFixture.CallAsync(fx.Client, "read_stack", Args(("sessionId", sessionId), ("maxFrames", 20)), ct);
            Assert.NotEmpty(ArrayProp(stack, "frames").EnumerateArray());
            var resumed = await E2EFixture.CallAsync(fx.Client, "resume", Args(("sessionId", sessionId), ("timeoutMs", 1000)), ct);
            Assert.Equal("ok", Prop(resumed, "status"));
            Assert.Equal("run", E2EFixture.ModeOf(resumed));
            try { if (!wait.HasExited) wait.Kill(); } catch { }
            await wait.WaitForExitAsync(ct);
            // The rule no longer strands the session once the target ended.
            var exit = await E2EFixture.CallAsync(fx.Client, "launch_process",
                Args(("sessionId", sessionId), ("executablePath", fx.SandboxExe), ("arguments", "exit"), ("timeoutMs", 30000)), ct);
            Assert.Equal("design", E2EFixture.ModeOf(exit));
            return null;
        });
    }

    [E2EFact]
    public async Task ReadOnlyGate_NoSideEffects()
    {
        E2EFixture.Gate();
        var ct = TestCt();
        await E2EFixture.WithCleanupAsync<object?>(async cleanups =>
        {
            var sessionId = await AttachAsync(ct);
            cleanups.Add(() => DetachAsync(sessionId, CancellationToken.None));
            var set = await E2EFixture.CallAsync(fx.Client, "set_breakpoint",
                Args(("sessionId", sessionId), ("file", fx.SandboxScenarios), ("line", fx.MarkerLine("INSPECT"))), ct);
            var bp = Prop(ArrayProp(set, "breakpoint"), "id");
            cleanups.Add(async () =>
            {
                try { await E2EFixture.CallAsync(fx.Client, "remove_breakpoint", Args(("sessionId", sessionId), ("breakpointId", bp)), CancellationToken.None); } catch { }
            });
            var launch = await E2EFixture.CallAsync(fx.Client, "launch_process",
                Args(("sessionId", sessionId), ("executablePath", fx.SandboxExe), ("arguments", "inspect"), ("timeoutMs", 30000)), ct);
            Assert.Equal("break", E2EFixture.ModeOf(launch));
            foreach (var expr in new[] { "counter.Increment()", "order.Items.Clear()", "order.ComputeSubtotal()" })
            {
                var refused = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "evaluate",
                    Args(("sessionId", sessionId), ("expression", expr), ("timeoutMs", 10000)), ct));
                Assert.Equal("invalid-expression", refused.Code);
            }
            Assert.Equal("0", Prop(await E2EFixture.CallAsync(fx.Client, "evaluate",
                Args(("sessionId", sessionId), ("expression", "counter.Value"), ("timeoutMs", 10000)), ct), "value"));
            Assert.Equal("1500", Prop(await E2EFixture.CallAsync(fx.Client, "evaluate",
                Args(("sessionId", sessionId), ("expression", "order.Items.Count"), ("timeoutMs", 10000)), ct), "value"));
            foreach (var expr in new[] { "counter.Value++", "order.Id = 0", "a <<= 1", "a; b" })
            {
                var refused = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "evaluate",
                    Args(("sessionId", sessionId), ("expression", expr), ("timeoutMs", 10000)), ct));
                Assert.Equal("invalid-argument", refused.Code);
            }
            var noPath = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "read_variables",
                Args(("sessionId", sessionId), ("frameId", "f0"), ("path", "order.Nope"), ("maxVariables", 50)), ct));
            Assert.Equal("path-not-found", noPath.Code);
            var noFrame = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "read_variables",
                Args(("sessionId", sessionId), ("frameId", "f99"), ("path", "node"), ("maxVariables", 50)), ct));
            Assert.Equal("frame-not-found", noFrame.Code);
            // Truncation contract: the long note ends with the marker and stays expandable.
            var vars = await E2EFixture.CallAsync(fx.Client, "read_variables",
                Args(("sessionId", sessionId), ("frameId", "f0"), ("maxVariables", 50)), ct);
            var note = ArrayProp(vars, "variables").EnumerateArray().First(v => Prop(v, "name") == "note");
            Assert.EndsWith(" [truncated]", Prop(note, "value"));
            Assert.Equal("True", Prop(note, "expandable"));
            cleanups.Add(async () =>
            {
                try { await E2EFixture.CallAsync(fx.Client, "resume", Args(("sessionId", sessionId), ("timeoutMs", 30000)), CancellationToken.None); } catch { }
            });
            return null;
        });
    }

    [E2EFact]
    public async Task FrameTrue_StackLevels()
    {
        E2EFixture.Gate();
        var ct = TestCt();
        await E2EFixture.WithCleanupAsync<object?>(async cleanups =>
        {
            var sessionId = await AttachAsync(ct);
            cleanups.Add(() => DetachAsync(sessionId, CancellationToken.None));
            var set = await E2EFixture.CallAsync(fx.Client, "set_breakpoint",
                Args(("sessionId", sessionId), ("file", fx.SandboxScenarios), ("line", fx.MarkerLine("STACK_BOTTOM"))), ct);
            var bp = Prop(ArrayProp(set, "breakpoint"), "id");
            cleanups.Add(async () =>
            {
                try { await E2EFixture.CallAsync(fx.Client, "remove_breakpoint", Args(("sessionId", sessionId), ("breakpointId", bp)), CancellationToken.None); } catch { }
            });
            var launch = await E2EFixture.CallAsync(fx.Client, "launch_process",
                Args(("sessionId", sessionId), ("executablePath", fx.SandboxExe), ("arguments", "stack"), ("timeoutMs", 30000)), ct);
            Assert.Equal("break", E2EFixture.ModeOf(launch));
            var stack = await E2EFixture.CallAsync(fx.Client, "read_stack", Args(("sessionId", sessionId), ("maxFrames", 20)), ct);
            var frames = ArrayProp(stack, "frames").EnumerateArray().ToList();
            Assert.True(frames.Count >= 5, $"expected f0..f4, got {frames.Count} frames");
            Assert.Contains("Recurse", Prop(frames[0], "function"));
            static string LevelOf(JsonElement vars) =>
                Prop(ArrayProp(vars, "variables").EnumerateArray().First(v => Prop(v, "name") == "level"), "value");
            var f0vars = await E2EFixture.CallAsync(fx.Client, "read_variables",
                Args(("sessionId", sessionId), ("frameId", "f0"), ("maxVariables", 50)), ct);
            var f2vars = await E2EFixture.CallAsync(fx.Client, "read_variables",
                Args(("sessionId", sessionId), ("frameId", "f2"), ("maxVariables", 50)), ct);
            var f4vars = await E2EFixture.CallAsync(fx.Client, "read_variables",
                Args(("sessionId", sessionId), ("frameId", "f4"), ("maxVariables", 50)), ct);
            Assert.Equal("40", LevelOf(f0vars));
            Assert.Equal("20", LevelOf(f2vars));
            Assert.Equal("0", LevelOf(f4vars));
            var drilled = await E2EFixture.CallAsync(fx.Client, "read_variables",
                Args(("sessionId", sessionId), ("frameId", "f2"), ("path", "node"), ("maxVariables", 50)), ct);
            var members = ArrayProp(drilled, "variables").EnumerateArray().ToList();
            Assert.Equal("2", Prop(members.First(v => Prop(v, "name") == "Depth"), "value"));
            Assert.Contains("frame2", Prop(members.First(v => Prop(v, "name") == "Label"), "value"));
            // Evaluate stays in f0 after reading other frames.
            Assert.Equal("40", Prop(await E2EFixture.CallAsync(fx.Client, "evaluate",
                Args(("sessionId", sessionId), ("expression", "level"), ("timeoutMs", 10000)), ct), "value"));
            Assert.Equal("4", Prop(await E2EFixture.CallAsync(fx.Client, "evaluate",
                Args(("sessionId", sessionId), ("expression", "node.Depth"), ("timeoutMs", 10000)), ct), "value"));
            cleanups.Add(async () =>
            {
                try { await E2EFixture.CallAsync(fx.Client, "resume", Args(("sessionId", sessionId), ("timeoutMs", 30000)), CancellationToken.None); } catch { }
            });
            return null;
        });
    }

    [E2EFact]
    public async Task StableIds_BreakpointIdentity()
    {
        E2EFixture.Gate();
        var ct = TestCt();
        await E2EFixture.WithCleanupAsync<object?>(async cleanups =>
        {
            var sessionId = await AttachAsync(ct);
            cleanups.Add(() => DetachAsync(sessionId, CancellationToken.None));
            var file = fx.SandboxScenarios;
            async Task<string> SetBp(string marker)
            {
                var set = await E2EFixture.CallAsync(fx.Client, "set_breakpoint",
                    Args(("sessionId", sessionId), ("file", file), ("line", fx.MarkerLine(marker))), ct);
                return Prop(ArrayProp(set, "breakpoint"), "id");
            }
            var x = await SetBp("TICK");
            var y = await SetBp("AFTER_LOOP");
            var z = await SetBp("HEARTBEAT");
            Assert.True(new[] { x, y, z }.Distinct().Count() == 3, "ids must be distinct");
            var rx = await E2EFixture.CallAsync(fx.Client, "remove_breakpoint", Args(("sessionId", sessionId), ("breakpointId", x)), ct);
            var rz = await E2EFixture.CallAsync(fx.Client, "remove_breakpoint", Args(("sessionId", sessionId), ("breakpointId", z)), ct);
            Assert.Equal(x, Prop(rx, "removedId"));
            Assert.Equal(z, Prop(rz, "removedId"));
            var list = await E2EFixture.CallAsync(fx.Client, "list_breakpoints", Args(("sessionId", sessionId)), ct);
            var items = ArrayProp(list, "breakpoints").EnumerateArray().ToList();
            Assert.Single(items, bp => Prop(bp, "id") == y);
            var gone = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "remove_breakpoint",
                Args(("sessionId", sessionId), ("breakpointId", x)), ct));
            Assert.Equal("breakpoint-not-found", gone.Code);
            var fresh = await SetBp("AFTER_LOOP");
            Assert.DoesNotContain(fresh, new[] { x, y, z });
            cleanups.Add(async () =>
            {
                foreach (var id in new[] { y, fresh })
                {
                    try { await E2EFixture.CallAsync(fx.Client, "remove_breakpoint", Args(("sessionId", sessionId), ("breakpointId", id)), CancellationToken.None); } catch { }
                }
            });
            var badFile = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "set_breakpoint",
                Args(("sessionId", sessionId), ("file", ""), ("line", 42)), ct));
            Assert.Equal("invalid-argument", badFile.Code);
            var badLine = await Assert.ThrowsAsync<E2EToolException>(() => E2EFixture.CallAsync(fx.Client, "set_breakpoint",
                Args(("sessionId", sessionId), ("file", file), ("line", 0)), ct));
            Assert.Equal("invalid-argument", badLine.Code);
            return null;
        });
    }

    [E2EFact]
    public async Task AttachMode_ReportsKnownMode()
    {
        E2EFixture.Gate();
        var ct = TestCt();
        await E2EFixture.WithCleanupAsync<object?>(async cleanups =>
        {
            var sessionId = await AttachAsync(ct);
            cleanups.Add(() => DetachAsync(sessionId, CancellationToken.None));
            var sessions = await E2EFixture.CallAsync(fx.Client, "list_sessions", null, ct);
            Assert.Contains(ArrayProp(sessions, "sessions").EnumerateArray(), s => Prop(s, "sessionId") == sessionId);
            return null;
        });
    }

    [Fact(Skip = "manual: open a modal dialog in VS, then run with VSDEBUG_E2E=1 to prove vs-busy still surfaces after the filter window.")]
    public async Task BusyVs_StillSurfacesAfterRetryWindow()
    {
        E2EFixture.Gate();
        await Task.CompletedTask;
    }
}
