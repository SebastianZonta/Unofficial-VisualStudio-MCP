using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VsDebugMcp;

/// <summary>launch_process start rules shared by both bridges: checked before any
/// process exists, so a refused launch never leaves a partial binding behind.</summary>
internal static class ProcessLaunch
{
    /// <summary>Validates the start request and returns the working directory to use
    /// (the executable's directory when none is given).</summary>
    public static string CheckStart(string executablePath, string? workingDirectory)
    {
        CheckExecutableExists(executablePath);
        return ResolveWorkingDirectory(executablePath, workingDirectory);
    }

    private static void CheckExecutableExists(string executablePath)
    {
        if (!ArgumentValidator.IsAbsolutePath(executablePath))
            throw new DebugDomainException(
                ErrorCodes.InvalidArgument, $"executablePath must be an absolute path: {executablePath}",
                "pass the full path to a built target executable");
        if (BaseName(executablePath).Equals("devenv", StringComparison.OrdinalIgnoreCase))
            throw new DebugDomainException(
                ErrorCodes.InvalidArgument, "devenv is a VS instance, never a target process",
                "pass the program under test, not Visual Studio itself");
        if (!File.Exists(executablePath))
            throw new DebugDomainException(
                ErrorCodes.ExecutableNotFound, $"no executable at {executablePath}",
                "pass the full path to a built target executable");
        // The live bridge attaches the CoreCLR engine only: anything else would run undebugged.
        // shortcut: single-file bundles embed runtimeconfig and are refused, relax when one is needed.
        if (!File.Exists(Path.ChangeExtension(executablePath, ".runtimeconfig.json")))
            throw new DebugDomainException(
                ErrorCodes.InvalidArgument, $"{executablePath} is not a .NET 5+ app (no .runtimeconfig.json beside it)",
                "launch a .NET 5+ build output, or start it yourself and use attach_process");
    }

    private static string BaseName(string name) => Path.GetFileNameWithoutExtension(name.Trim().Trim('"'));

    private static string ResolveWorkingDirectory(string executablePath, string? workingDirectory)
    {
        if (workingDirectory is not null && workingDirectory.Length == 0)
            throw new DebugDomainException(
                ErrorCodes.InvalidArgument, "working directory must not be empty",
                "pass an existing working directory or omit it to use the executable's directory");
        var cwd = workingDirectory ?? Path.GetDirectoryName(executablePath);
        if (string.IsNullOrEmpty(cwd))
            throw new DebugDomainException(
                ErrorCodes.InvalidArgument, $"could not determine a working directory for {executablePath}",
                "pass an existing working directory explicitly");
        if (!Directory.Exists(cwd))
            throw new DebugDomainException(
                ErrorCodes.InvalidArgument, $"working directory {cwd} does not exist",
                "pass an existing working directory or omit it to use the executable's directory");
        return cwd;
    }

    /// <summary>One debug session owns at most one target process; never an implicit kill.</summary>
    public static DebugDomainException AlreadyBound(int targetPid) => new(
        ErrorCodes.AlreadyBound, $"the VS instance of this debug session is already debugging target PID {targetPid}",
        "end the current run (or detach) before launching");

    /// <summary>Recycled-PID guard: when the caller names the target it listed, a PID
    /// now naming something else fails closed instead of debugging the wrong process.</summary>
    public static void CheckExpectedName(string actualName, string? expectedName, int processId)
    {
        if (string.IsNullOrWhiteSpace(expectedName))
            return;
        var actual = BaseName(actualName);
        var expected = BaseName(expectedName);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new DebugDomainException(
                "process-not-found", $"process {processId} is now {actualName}, expected {expectedName} (PID may have been recycled)",
                "list local processes and retry");
    }

    /// <summary>CreateProcess Unicode environment block: extra variables override inherited
    /// ones case-insensitively (Windows semantics), sorted, double-NUL terminated.</summary>
    public static string BuildEnvironmentBlock(
        IReadOnlyDictionary<string, string> inherited, IReadOnlyDictionary<string, string>? extra)
    {
        var extras = extra ?? new Dictionary<string, string>();
        foreach (var kv in extras)
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Key.Contains('=') || kv.Key.Contains('\0'))
                throw new DebugDomainException(
                    ErrorCodes.InvalidArgument, $"invalid environment variable name {kv.Key}",
                    "pass names without '=' or NUL");
            if (kv.Value is null || kv.Value.Contains('\0'))
                throw new DebugDomainException(
                    ErrorCodes.InvalidArgument, $"invalid environment value for {kv.Key}",
                    "pass values without NUL");
        }
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in inherited)
            merged[kv.Key] = kv.Value;
        foreach (var kv in extras)
        {
            merged.Remove(kv.Key);
            merged[kv.Key] = kv.Value;
        }
        var entries = merged.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => $"{kv.Key}={kv.Value}\0");
        return string.Concat(entries) + "\0";
    }

    public static IReadOnlyDictionary<string, string> InheritedEnvironment()
    {
        var inherited = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry e in Environment.GetEnvironmentVariables())
            inherited[(string)e.Key] = (string?)e.Value ?? "";
        return inherited;
    }

    /// <summary>Starts the target process suspended in its own console (never the MCP
    /// stdio pipe), so the debugger attaches before any user code runs.</summary>
    public static SuspendedProcess StartSuspended(string executablePath, string? arguments, string workingDirectory, string environmentBlock)
    {
        var commandLine = string.IsNullOrWhiteSpace(arguments) ? $"\"{executablePath}\"" : $"\"{executablePath}\" {arguments}";
        var cmd = Marshal.StringToHGlobalUni(commandLine);
        var env = Marshal.StringToHGlobalUni(environmentBlock);
        try
        {
            var si = new NativeMethods.StartupInfo { Cb = Marshal.SizeOf<NativeMethods.StartupInfo>() };
            if (!NativeMethods.CreateProcessW(executablePath, cmd, IntPtr.Zero, IntPtr.Zero, false,
                    NativeMethods.CreateSuspended | NativeMethods.CreateNewConsole | NativeMethods.CreateUnicodeEnvironment,
                    env, workingDirectory, ref si, out var pi))
            {
                var win32 = new Win32Exception(Marshal.GetLastWin32Error());
                throw new DebugDomainException(
                    ErrorCodes.LaunchFailed, $"could not start {executablePath}: {win32.Message}",
                    "check the executable is runnable by the bridge user");
            }
            return new SuspendedProcess(pi.ProcessId, pi.Process, pi.Thread);
        }
        finally
        {
            Marshal.FreeHGlobal(cmd);
            Marshal.FreeHGlobal(env);
        }
    }

    /// <summary>OS handles of a just-started target process; closed on dispose, never escaping the bridge.</summary>
    internal sealed class SuspendedProcess(int pid, IntPtr process, IntPtr thread) : IDisposable
    {
        public int Pid { get; } = pid;

        public void Resume()
        {
            if (NativeMethods.ResumeThread(thread) == -1)
                throw new DebugDomainException(
                    ErrorCodes.LaunchFailed, $"could not resume target process {Pid}",
                    "check the executable is runnable by the bridge user");
        }

        /// <summary>No partial binding: a launch that fails after start ends its target process.</summary>
        public void Kill() => _ = NativeMethods.TerminateProcess(process, 1);

        public void Dispose()
        {
            _ = NativeMethods.CloseHandle(thread);
            _ = NativeMethods.CloseHandle(process);
        }
    }

    private static class NativeMethods
    {
        internal const int CreateSuspended = 0x4;
        internal const int CreateNewConsole = 0x10;
        internal const int CreateUnicodeEnvironment = 0x400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StartupInfo
        {
            public int Cb;
            public IntPtr Reserved, Desktop, Title;
            public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
            public short ShowWindow, Reserved2;
            public IntPtr Reserved2Ptr, StdInput, StdOutput, StdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessInformation
        {
            public IntPtr Process, Thread;
            public int ProcessId, ThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcessW(string applicationName, IntPtr commandLine, IntPtr processAttributes,
            IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, int creationFlags,
            IntPtr environment, string currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int ResumeThread(IntPtr thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
