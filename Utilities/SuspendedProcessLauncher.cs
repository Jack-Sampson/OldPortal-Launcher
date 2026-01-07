using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using OPLauncher.Services;

namespace OPLauncher.Utilities;

/// <summary>
/// Provides functionality to launch processes in a suspended state using Windows API.
/// This bypasses the Asheron's Call client's single-instance mutex check, allowing
/// multiple game clients to run simultaneously without requiring Decal or external DLLs.
///
/// Technical Details:
/// - Uses CREATE_SUSPENDED flag in CreateProcessW to start process in suspended state
/// - Suspended processes don't execute their mutex checks until resumed
/// - No DLL injection needed - pure P/Invoke to Windows API
/// - Requires UserPreferences.ini to have ComputeUniquePort=True for network ports
///
/// References:
/// - https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw
/// - Chorizite.Injector implementation (reference for technique)
/// </summary>
public static class SuspendedProcessLauncher
{
    #region Windows API Constants

    private const uint CREATE_SUSPENDED = 0x00000004;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    #endregion

    #region Windows API Structures

    /// <summary>
    /// Specifies the window station, desktop, standard handles, and appearance of the main window
    /// for a process at creation time.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public uint cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    /// <summary>
    /// Contains information about a newly created process and its primary thread.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    #endregion

    #region Windows API P/Invoke Declarations

    /// <summary>
    /// Creates a new process and its primary thread. The new process runs in the security context of the calling process.
    /// </summary>
    /// <param name="lpApplicationName">The name of the module to be executed.</param>
    /// <param name="lpCommandLine">The command line to be executed.</param>
    /// <param name="lpProcessAttributes">A pointer to a SECURITY_ATTRIBUTES structure.</param>
    /// <param name="lpThreadAttributes">A pointer to a SECURITY_ATTRIBUTES structure.</param>
    /// <param name="bInheritHandles">If this parameter is TRUE, each inheritable handle in the calling process is inherited by the new process.</param>
    /// <param name="dwCreationFlags">The flags that control the priority class and the creation of the process.</param>
    /// <param name="lpEnvironment">A pointer to the environment block for the new process.</param>
    /// <param name="lpCurrentDirectory">The full path to the current directory for the process.</param>
    /// <param name="lpStartupInfo">A pointer to a STARTUPINFO structure.</param>
    /// <param name="lpProcessInformation">A pointer to a PROCESS_INFORMATION structure.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation
    );

    /// <summary>
    /// Decrements a thread's suspend count. When the suspend count is decremented to zero, the execution of the thread is resumed.
    /// </summary>
    /// <param name="hThread">A handle to the thread to be restarted.</param>
    /// <returns>If the function succeeds, the return value is the thread's previous suspend count.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr hThread);

    /// <summary>
    /// Closes an open object handle.
    /// </summary>
    /// <param name="hObject">A valid handle to an open object.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    #endregion

    #region Public Methods

    /// <summary>
    /// Launches a process in suspended state, then immediately resumes it.
    /// This bypasses the AC client's single-instance mutex check.
    /// </summary>
    /// <param name="executablePath">Full path to the executable to launch.</param>
    /// <param name="arguments">Command line arguments to pass to the process.</param>
    /// <param name="workingDirectory">Working directory for the process (typically the executable's directory).</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <returns>Process ID of the launched process, or 0 on failure.</returns>
    /// <exception cref="ArgumentException">Thrown when executablePath is null or empty.</exception>
    /// <exception cref="Win32Exception">Thrown when CreateProcessW fails.</exception>
    public static int LaunchSuspended(
        string executablePath,
        string arguments,
        string workingDirectory,
        LoggingService? logger = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or empty.", nameof(executablePath));
        }

        logger?.Debug("SuspendedProcessLauncher: Launching suspended process");
        logger?.Debug("  Executable: {ExecutablePath}", executablePath);
        logger?.Debug("  Arguments: {Arguments}", arguments);
        logger?.Debug("  Working Directory: {WorkingDirectory}", workingDirectory);

        // Build command line (executable path must be quoted if it contains spaces)
        var commandLine = $"\"{executablePath}\" {arguments}";
        logger?.Debug("  Command Line: {CommandLine}", commandLine);

        // Initialize STARTUPINFO structure
        var startupInfo = new STARTUPINFO
        {
            cb = (uint)Marshal.SizeOf<STARTUPINFO>()
        };

        // Create process in suspended state
        var creationFlags = CREATE_SUSPENDED | CREATE_UNICODE_ENVIRONMENT;

        logger?.Debug("  Creating process with CREATE_SUSPENDED flag...");

        bool success = CreateProcessW(
            null, // lpApplicationName (null to use command line)
            commandLine,
            IntPtr.Zero, // lpProcessAttributes (default security)
            IntPtr.Zero, // lpThreadAttributes (default security)
            false, // bInheritHandles
            creationFlags,
            IntPtr.Zero, // lpEnvironment (inherit from parent)
            workingDirectory,
            ref startupInfo,
            out PROCESS_INFORMATION processInfo
        );

        if (!success)
        {
            var error = Marshal.GetLastWin32Error();
            var errorMessage = new Win32Exception(error).Message;
            logger?.Error("CreateProcessW failed with error {ErrorCode}: {ErrorMessage}", error, errorMessage);

            throw new Win32Exception(error, $"Failed to create suspended process: {errorMessage}");
        }

        logger?.Information("Process created in suspended state. PID: {ProcessId}", processInfo.dwProcessId);

        try
        {
            // Resume the suspended thread (this allows the process to start executing)
            logger?.Debug("  Resuming thread {ThreadId}...", processInfo.dwThreadId);

            uint previousSuspendCount = ResumeThread(processInfo.hThread);

            if (previousSuspendCount == unchecked((uint)-1))
            {
                var error = Marshal.GetLastWin32Error();
                var errorMessage = new Win32Exception(error).Message;
                logger?.Error("ResumeThread failed with error {ErrorCode}: {ErrorMessage}", error, errorMessage);

                throw new Win32Exception(error, $"Failed to resume thread: {errorMessage}");
            }

            logger?.Debug("  Thread resumed successfully. Previous suspend count: {SuspendCount}", previousSuspendCount);
            logger?.Information("Process launched successfully via suspended launch. PID: {ProcessId}", processInfo.dwProcessId);

            return (int)processInfo.dwProcessId;
        }
        finally
        {
            // Clean up handles (critical - prevents handle leaks)
            logger?.Debug("  Closing process and thread handles...");

            if (processInfo.hProcess != IntPtr.Zero)
            {
                CloseHandle(processInfo.hProcess);
            }

            if (processInfo.hThread != IntPtr.Zero)
            {
                CloseHandle(processInfo.hThread);
            }

            logger?.Debug("  Handles closed successfully.");
        }
    }

    #endregion
}
