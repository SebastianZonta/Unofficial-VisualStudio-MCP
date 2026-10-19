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

    public static string CurrentUserName() => WindowsIdentity.GetCurrent().Name;

    /// <summary>One WMI query for every local process (2s cache): prefer this over
    /// per-pid <see cref="OwnerOf"/> when listing. Missing pids mean unknown owner.</summary>
    public static IReadOnlyDictionary<int, string?> SnapshotOwners()
    {
        lock (CacheLock)
        {
            if (_cache is not null && DateTime.UtcNow - _cacheAt < CacheTtl)
                return _cache;
        }
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
        lock (CacheLock)
        {
            if (_cache is not null && DateTime.UtcNow - _cacheAt < CacheTtl && _cache.TryGetValue(processId, out var cached))
                return cached;
        }
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    return ReadOwner(mo);
                }
            }
            return null;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            return null;
        }
    }

    private static Dictionary<int, string?> QueryAllOwners()
    {
        var owners = new Dictionary<int, string?>();
        try
        {
            // NB: SELECT * (not ProcessId-only): property-only results cannot
            // InvokeMethod("GetOwner") — every call throws InvalidOperationException.
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    try
                    {
                        var pid = Convert.ToInt32(mo["ProcessId"], CultureInfo.InvariantCulture);
                        owners[pid] = ReadOwner(mo);
                    }
                    catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException
                        or InvalidOperationException or FormatException or InvalidCastException or OverflowException)
                    {
                        // One unreadable entry must not poison the whole snapshot.
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
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
