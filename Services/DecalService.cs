// Component: OPLauncher
using System.Runtime.Versioning;
// Module: Decal Support
// Description: Service for detecting and managing Decal installation
// Includes direct DLL injection for Decal support (ThwargLauncher method)

using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OPLauncher.Services;

/// <summary>
/// Service for detecting Decal installation and getting Decal paths
/// Provides direct DLL injection using injector.dll (ThwargLauncher method)
/// </summary>
[SupportedOSPlatform("windows")]
public class DecalService
{
    private readonly LoggingService _logger;

    /// <summary>
    /// P/Invoke declaration for injector.dll
    /// Launches AC client with Decal DLL injection
    /// </summary>
    /// <param name="command_line">Full command line including executable and arguments</param>
    /// <param name="working_directory">Working directory for the process</param>
    /// <param name="inject_dll_path">Path to Decal's Inject.dll</param>
    /// <param name="initialize_function">Decal initialization function name (usually "DecalStartup")</param>
    /// <returns>Process ID of the launched AC client, or 0 on failure</returns>
    [DllImport("injector.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int LaunchInjected(
        string command_line,
        string working_directory,
        string inject_dll_path,
        [MarshalAs(UnmanagedType.LPStr)] string initialize_function);

    public DecalService(LoggingService logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if Decal is installed by looking in the Windows Registry
    /// </summary>
    /// <returns>True if Decal is installed, false otherwise</returns>
    public bool IsDecalInstalled()
    {
        try
        {
            // Decal is a 32-bit application, so on 64-bit Windows it may be in either location
            // Try both 64-bit view first, then 32-bit view
            string subKey = @"SOFTWARE\Decal\Agent";

            // Try default view (64-bit on 64-bit Windows, 32-bit on 32-bit Windows)
            using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(subKey))
            {
                if (key != null && CheckDecalPath(key))
                {
                    return true;
                }
            }

            // Try explicit 32-bit registry view (for 32-bit Decal on 64-bit Windows)
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (RegistryKey? key = baseKey.OpenSubKey(subKey))
                {
                    if (key != null && CheckDecalPath(key))
                    {
                        return true;
                    }
                }
            }

            _logger.Warning("Decal registry key not found in either 32-bit or 64-bit registry views");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error checking Decal installation in registry");
            return false;
        }
    }

    /// <summary>
    /// Helper method to check if a registry key contains a valid Decal installation
    /// </summary>
    private bool CheckDecalPath(RegistryKey key)
    {
        string? agentPath = key.GetValue("AgentPath") as string;
        if (string.IsNullOrEmpty(agentPath))
        {
            _logger.Debug("Decal AgentPath value not found in registry key");
            return false;
        }

        // Check if Inject.dll exists at the AgentPath
        string injectDllPath = Path.Combine(agentPath, "Inject.dll");
        if (!File.Exists(injectDllPath))
        {
            _logger.Debug("Decal Inject.dll not found at: {Path}", injectDllPath);
            return false;
        }

        _logger.Information("Decal installation found at: {Path}", agentPath);
        return true;
    }

    /// <summary>
    /// Gets the path to Decal's Inject.dll from the registry
    /// </summary>
    /// <returns>Path to Inject.dll, or null if not found</returns>
    public string? GetDecalInjectPath()
    {
        try
        {
            string subKey = @"SOFTWARE\Decal\Agent";

            // Try default registry view first
            using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(subKey))
            {
                if (key != null)
                {
                    string? path = GetInjectPathFromKey(key);
                    if (path != null) return path;
                }
            }

            // Try 32-bit registry view
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (RegistryKey? key = baseKey.OpenSubKey(subKey))
                {
                    if (key != null)
                    {
                        string? path = GetInjectPathFromKey(key);
                        if (path != null) return path;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting Decal inject path from registry");
            return null;
        }
    }

    /// <summary>
    /// Helper method to get Inject.dll path from a registry key
    /// </summary>
    private string? GetInjectPathFromKey(RegistryKey key)
    {
        string? agentPath = key.GetValue("AgentPath") as string;
        if (string.IsNullOrEmpty(agentPath)) return null;

        string injectDllPath = Path.Combine(agentPath, "Inject.dll");
        if (!File.Exists(injectDllPath)) return null;

        return injectDllPath;
    }

    /// <summary>
    /// Gets the path to Decal.exe launcher
    /// </summary>
    /// <returns>Path to Decal.exe, or null if not found</returns>
    public string? GetDecalLauncherPath()
    {
        try
        {
            // First, try to get Decal path from registry AgentPath
            string? agentPath = GetAgentPathFromRegistry();
            if (!string.IsNullOrEmpty(agentPath))
            {
                // Decal.exe is in the same directory as AgentPath
                string decalExePath = Path.Combine(agentPath, "Decal.exe");
                if (File.Exists(decalExePath))
                {
                    _logger.Information("Found Decal.exe at registry location: {Path}", decalExePath);
                    return decalExePath;
                }
                _logger.Debug("Decal.exe not found at registry location: {Path}", decalExePath);
            }

            // Fall back to common installation paths
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var possiblePaths = new[]
            {
                Path.Combine(programFilesX86, "Decal 3.0", "Decal.exe"),
                Path.Combine(programFilesX86, "Decal", "Decal.exe"),
                @"C:\Program Files (x86)\Decal 3.0\Decal.exe",
                @"C:\Program Files\Decal 3.0\Decal.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _logger.Information("Found Decal.exe at: {Path}", path);
                    return path;
                }
            }

            _logger.Warning("Decal.exe not found in registry location or common paths");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching for Decal.exe");
            return null;
        }
    }

    /// <summary>
    /// Helper method to get AgentPath from registry
    /// </summary>
    private string? GetAgentPathFromRegistry()
    {
        try
        {
            string subKey = @"SOFTWARE\Decal\Agent";

            // Try default registry view first
            using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(subKey))
            {
                if (key != null)
                {
                    string? agentPath = key.GetValue("AgentPath") as string;
                    if (!string.IsNullOrEmpty(agentPath))
                    {
                        return agentPath;
                    }
                }
            }

            // Try 32-bit registry view
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (RegistryKey? key = baseKey.OpenSubKey(subKey))
                {
                    if (key != null)
                    {
                        string? agentPath = key.GetValue("AgentPath") as string;
                        if (!string.IsNullOrEmpty(agentPath))
                        {
                            return agentPath;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading AgentPath from registry");
            return null;
        }
    }

    /// <summary>
    /// Launches AC client with direct Decal injection using injector.dll
    /// This is the ThwargLauncher method - more control than wrapper method
    /// </summary>
    /// <param name="acClientPath">Full path to acclient.exe</param>
    /// <param name="arguments">Command-line arguments for AC client</param>
    /// <param name="workingDirectory">Working directory (AC install folder)</param>
    /// <returns>Process ID of launched AC client, or 0 on failure</returns>
    public int LaunchWithDecalInjection(string acClientPath, string arguments, string workingDirectory)
    {
        try
        {
            // Get Decal inject DLL path
            var injectDllPath = GetDecalInjectPath();
            if (string.IsNullOrEmpty(injectDllPath))
            {
                _logger.Error("Cannot launch with Decal injection - Inject.dll not found");
                return 0;
            }

            // Build full command line
            var commandLine = $"\"{acClientPath}\" {arguments}";

            _logger.Information("Launching AC client with Decal injection");
            _logger.Debug("  Command: {CommandLine}", commandLine);
            _logger.Debug("  Working Dir: {WorkingDirectory}", workingDirectory);
            _logger.Debug("  Inject DLL: {InjectDll}", injectDllPath);

            // Call native injector.dll
            int processId = LaunchInjected(
                commandLine,
                workingDirectory,
                injectDllPath,
                "DecalStartup");

            if (processId > 0)
            {
                _logger.Information("AC client launched with Decal injection (PID: {ProcessId})", processId);
                return processId;
            }
            else
            {
                _logger.Error("LaunchInjected returned 0 - injection failed");
                return 0;
            }
        }
        catch (DllNotFoundException ex)
        {
            _logger.Error(ex, "injector.dll not found - cannot perform Decal injection");
            _logger.Error("Make sure injector.dll is in the same directory as the launcher executable");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error launching AC client with Decal injection");
            return 0;
        }
    }

    /// <summary>
    /// Checks if injector.dll is available for direct injection
    /// </summary>
    /// <returns>True if injector.dll exists in application directory</returns>
    public bool IsInjectorDllAvailable()
    {
        try
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var injectorPath = Path.Combine(appDirectory, "injector.dll");
            var exists = File.Exists(injectorPath);

            if (exists)
            {
                _logger.Debug("injector.dll found at: {Path}", injectorPath);
            }
            else
            {
                _logger.Warning("injector.dll not found at: {Path}", injectorPath);
            }

            return exists;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for injector.dll");
            return false;
        }
    }

    /// <summary>
    /// Gets the path to our multi-client hook DLL
    /// </summary>
    /// <returns>Path to OPLauncher.Hook.dll, or null if not found</returns>
    public string? GetMultiClientHookPath()
    {
        try
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var hookDllPath = Path.Combine(appDirectory, "OPLauncher.Hook.dll");

            if (File.Exists(hookDllPath))
            {
                _logger.Debug("OPLauncher.Hook.dll found at: {Path}", hookDllPath);
                return hookDllPath;
            }
            else
            {
                _logger.Warning("OPLauncher.Hook.dll not found at: {Path}", hookDllPath);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for OPLauncher.Hook.dll");
            return null;
        }
    }

    /// <summary>
    /// Checks if the multi-client hook DLL is available
    /// </summary>
    /// <returns>True if OPLauncher.Hook.dll exists in application directory</returns>
    public bool IsMultiClientHookAvailable()
    {
        return GetMultiClientHookPath() != null;
    }

    /// <summary>
    /// Launches AC client with multi-client hook injection using injector.dll
    /// This enables multiple AC client instances by hooking the mutex check
    /// </summary>
    /// <param name="acClientPath">Full path to acclient.exe</param>
    /// <param name="arguments">Command-line arguments for AC client</param>
    /// <param name="workingDirectory">Working directory (AC install folder)</param>
    /// <returns>Process ID of launched AC client, or 0 on failure</returns>
    public int LaunchWithMultiClientHook(string acClientPath, string arguments, string workingDirectory)
    {
        try
        {
            // Get our hook DLL path
            var hookDllPath = GetMultiClientHookPath();
            if (string.IsNullOrEmpty(hookDllPath))
            {
                _logger.Error("Cannot launch with multi-client hook - OPLauncher.Hook.dll not found");
                return 0;
            }

            // Build full command line
            var commandLine = $"\"{acClientPath}\" {arguments}";

            _logger.Information("Launching AC client with multi-client hook injection");
            _logger.Debug("  Command: {CommandLine}", commandLine);
            _logger.Debug("  Working Dir: {WorkingDirectory}", workingDirectory);
            _logger.Debug("  Hook DLL: {HookDll}", hookDllPath);

            // Call native injector.dll to inject our hook DLL
            // The hook DLL will call ACClientHooks.Initialize() which hooks Client::IsAlreadyRunning
            int processId = LaunchInjected(
                commandLine,
                workingDirectory,
                hookDllPath,
                "HookStartup");  // Entry point function name in OPLauncher.Hook.dll

            if (processId > 0)
            {
                _logger.Information("✓ AC client launched with multi-client hook (PID: {ProcessId})", processId);
                return processId;
            }
            else
            {
                _logger.Error("LaunchInjected returned 0 - hook injection failed");
                return 0;
            }
        }
        catch (DllNotFoundException ex)
        {
            _logger.Error(ex, "injector.dll not found - cannot perform hook injection");
            _logger.Error("Make sure injector.dll is in the same directory as the launcher executable");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error launching AC client with multi-client hook");
            return 0;
        }
    }
}
