using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace VsDebugMcp;

/// <summary>Same-user check for process attach: the bridge may only attach to processes
/// owned by the user it runs as. WMI is the owner source; failures return null (fail closed).</summary>
internal static class ProcessOwner
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(2);
    private static readonly object CacheLock = new();
    private static Dictionary<int, string?>? _cache;
    private static DateTime _cacheAt = DateTime.MinValue;

    /// <summary>WMI/hosting failures that mean unknown owner: fail closed, never a bare runtime error.
    /// Covers a stale publish placeholder System.Management.dll (PlatformNotSupported).</summary>
    private static bool IsHostingFailure(Exception ex) => ex is ManagementException
        or UnauthorizedAccessException or COMException or InvalidOperationException or InvalidComObjectException
        or PlatformNotSupportedException or FileNotFoundException or BadImageFormatException;

    private static bool IsEntryFailure(Exception ex) => IsHostingFailure(ex)
        || ex is FormatException or InvalidCastException or OverflowException;

    private static bool TryGetFresh(out Dictionary<int, string?>? cache)
    {
        lock (CacheLock)
        {
            cache = _cache;
            return cache is not null && DateTime.UtcNow - _cacheAt < CacheTtl;
        }
    }

    /// <summary>One WMI enumeration with deterministic disposal.
    /// NB: SELECT * (not ProcessId-only): property-only results cannot
    /// InvokeMethod("GetOwner") — every call throws InvalidOperationException.
    /// Row returns false to stop early.</summary>
    private static void ForEachProcess(string query, Func<ManagementObject, bool> row)
    {
        using var searcher = new ManagementObjectSearcher(query);
        using var results = searcher.Get();
        foreach (ManagementObject mo in results)
        {
            using (mo)
            {
                if (!row(mo))
                    return;
            }
        }
    }

    public static string CurrentUserName()
    {
        try
        {
            return WindowsIdentity.GetCurrent().Name;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Identity/SID resolution varies by token and elevation: surface a mapped
            // code, never a bare host error, so both process tools stay named.
            throw new DebugDomainException(
                "automation-failed", $"could not determine the bridge user: {ex.Message}",
                "retry; if it persists, run the MCP host as the same user and elevation as the target processes");
        }
    }

    /// <summary>One WMI query for every local process (2s cache): prefer this over
    /// per-pid <see cref="OwnerOf"/> when listing. Missing pids mean unknown owner.</summary>
    public static IReadOnlyDictionary<int, string?> SnapshotOwners()
    {
        if (TryGetFresh(out var cached) && cached is not null)
            return cached;
        var fresh = QueryAllOwners();
        lock (CacheLock)
        {
            _cache = fresh;
            _cacheAt = DateTime.UtcNow;
            return _cache;
        }
    }

    public static string? OwnerOf(int processId)
    {
        if (TryGetFresh(out var cached) && cached is not null && cached.TryGetValue(processId, out var hit))
            return hit;
        string? owner = null;
        try
        {
            ForEachProcess($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}", mo =>
            {
                owner = ReadOwner(mo);
                return false;
            });
        }
        catch (Exception ex) when (IsHostingFailure(ex))
        {
            // Stale publish dirs ship a placeholder System.Management.dll that throws
            // PlatformNotSupported: fail closed to unknown owner, never a bare runtime error.
            return null;
        }
        return owner;
    }

    private static Dictionary<int, string?> QueryAllOwners()
    {
        var owners = new Dictionary<int, string?>();
        try
        {
            ForEachProcess("SELECT * FROM Win32_Process", mo =>
            {
                try
                {
                    var pid = Convert.ToInt32(mo["ProcessId"], CultureInfo.InvariantCulture);
                    owners[pid] = ReadOwner(mo);
                }
                catch (Exception ex) when (IsEntryFailure(ex))
                {
                    // One unreadable entry must not poison the whole snapshot.
                }
                return true;
            });
        }
        catch (Exception ex) when (IsHostingFailure(ex))
        {
            // Fail closed: empty map means every owner reads unknown downstream.
        }
        return owners;
    }

    private static string? ReadOwner(ManagementObject mo)
    {
        var outParams = mo.InvokeMethod("GetOwner", null, null);
        var user = outParams?["User"]?.ToString();
        var domain = outParams?["Domain"]?.ToString();
        if (!string.IsNullOrWhiteSpace(user))
            return string.IsNullOrWhiteSpace(domain) ? user : $"{domain}\\{user}";
        return null;
    }
}
