using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace VsDebugMcp;

/// <summary>Same-user check for process attach: the bridge may only attach to processes
/// owned by the user it runs as. WMI is the owner source; failures return null (fail closed).</summary>
internal static class ProcessOwner
{
    public static string CurrentUserName() => WindowsIdentity.GetCurrent().Name;

    public static string? OwnerOf(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    var outParams = mo.InvokeMethod("GetOwner", null, null);
                    var user = outParams?["User"]?.ToString();
                    var domain = outParams?["Domain"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(user))
                        return string.IsNullOrWhiteSpace(domain) ? user : $"{domain}\\{user}";
                }
            }
            return null;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        {
            return null;
        }
    }
}
