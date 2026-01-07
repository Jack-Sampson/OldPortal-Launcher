using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using OPLauncher.Models;
using OPLauncher.DTOs;

namespace OPLauncher.Services;

/// <summary>
/// Service for launching the Asheron's Call client with proper connection parameters.
/// Handles process creation, command-line argument building, and secure password handling.
/// </summary>
public class GameLaunchService
{
    private readonly ConfigService _configService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly DecalService _decalService;
    private readonly LoggingService _logger;

    /// <summary>
    /// Event that fires during game launch to report progress.
    /// Fired every second during the verification phase.
    /// </summary>
    public event EventHandler<LaunchProgressInfo>? LaunchProgress;

    /// <summary>
    /// Gets or sets whether progress events should be suppressed (used during multi-client launches).
    /// </summary>
    public bool SuppressProgressEvents { get; set; }

    /// <summary>
    /// Fires a launch progress event if not suppressed.
    /// </summary>
    private void FireProgressEvent(LaunchProgressInfo progressInfo)
    {
        if (!SuppressProgressEvents)
        {
            LaunchProgress?.Invoke(this, progressInfo);
        }
    }

    /// <summary>
    /// Initializes a new instance of the GameLaunchService.
    /// </summary>
    /// <param name="configService">The configuration service for AC client path.</param>
    /// <param name="credentialVaultService">The credential vault service for password decryption.</param>
    /// <param name="decalService">The Decal service for Decal detection and paths.</param>
    /// <param name="logger">The logging service for diagnostics.</param>
    public GameLaunchService(
        ConfigService configService,
        CredentialVaultService credentialVaultService,
        DecalService decalService,
        LoggingService logger)
    {
        _configService = configService;
        _credentialVaultService = credentialVaultService;
        _decalService = decalService;
        _logger = logger;

        _logger.Debug("GameLaunchService initialized");
    }

    /// <summary>
    /// Launches the Asheron's Call client with the specified connection and credentials.
    /// This method supports launching multiple simultaneous game instances.
    /// </summary>
    /// <param name="connection">The world connection information (host, port, world details).</param>
    /// <param name="credential">Optional saved credential to use for auto-login. If null, launches without account.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the launch during verification.</param>
    /// <returns>A LaunchResult containing success status, error information, and process ID.</returns>
    public async Task<LaunchResult> LaunchGameAsync(WorldConnectionDto connection, SavedCredential? credential = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.Information("========================================");
                _logger.Information("Attempting to launch AC client for world: {WorldName} (ID: {WorldId})",
                    connection.WorldName, connection.WorldId);

                // Validate AC client path
                var acClientPath = _configService.Current.AcClientPath;
                var (isValid, validationError) = _configService.ValidateAcClientPath(acClientPath);

                if (!isValid)
                {
                    _logger.Error("AC client path validation failed: {Error}", validationError);
                    return LaunchResult.CreateFailure(
                        validationError ?? "AC client path is not configured or invalid",
                        connection.WorldId,
                        connection.WorldName);
                }

                // Launch method priority:
                // PRIORITY 1: Decal (if enabled, it can handle multi-client via its "Dual Log" feature)
                // PRIORITY 2: OPLauncher multi-client hook (if multi-client enabled but Decal disabled)
                // PRIORITY 3: Standard launch (no injection)
                var enableMultiClient = _configService.Current.EnableMultiClient;
                var useDecal = _configService.Current.UseDecal;
                bool useDirectInjection = false;
                bool useMultiClientLaunch = false;
                bool useDecalInjection = false;
                string? executablePath = acClientPath;

                _logger.Information("Launch method decision:");
                _logger.Information("  - UseDecal: {UseDecal}", useDecal);
                _logger.Information("  - EnableMultiClient: {EnableMultiClient}", enableMultiClient);

                // PRIORITY 1: Check if we should use Decal (takes precedence - can handle multi-client via Dual Log)
                if (useDecal)
                {
                    if (enableMultiClient)
                    {
                        _logger.Information("Decal ENABLED + Multi-client ENABLED - Decal will handle multi-client via Dual Log feature");
                        _logger.Information("Note: Users should enable 'Dual Log' in Decal Options if not already enabled");
                    }
                    else
                    {
                        _logger.Information("Decal ENABLED, Multi-client DISABLED - using standard Decal injection");
                    }

                    // Validate Decal installation using DecalService
                    if (!_decalService.IsDecalInstalled())
                    {
                        _logger.Error("Decal is not installed (checked via registry)");
                        return LaunchResult.CreateFailure(
                            "Decal is not installed. Please install Decal or disable 'Use Decal' in settings.",
                            connection.WorldId,
                            connection.WorldName);
                    }

                    // Check if injector.dll is available for direct injection (ThwargLauncher method)
                    if (_decalService.IsInjectorDllAvailable())
                    {
                        _logger.Information("Using direct Decal injection (ThwargLauncher method)");
                        useDirectInjection = true;
                        useDecalInjection = true;
                        // Keep executablePath as acClientPath - we'll use direct injection
                    }
                    else
                    {
                        // Fall back to wrapper method (launching Decal.exe)
                        _logger.Information("injector.dll not found - using Decal.exe wrapper method");

                        var decalPath = _decalService.GetDecalLauncherPath();
                        if (decalPath == null || !File.Exists(decalPath))
                        {
                            _logger.Error("Decal.exe not found");
                            return LaunchResult.CreateFailure(
                                "Decal.exe not found. Please reinstall Decal or disable 'Use Decal' in settings.",
                                connection.WorldId,
                                connection.WorldName);
                        }

                        executablePath = decalPath;
                        _logger.Information("Using Decal wrapper to launch game from: {DecalPath}", decalPath);
                    }
                }
                // PRIORITY 2: Check if multi-client is enabled (only if Decal is NOT enabled)
                else if (enableMultiClient)
                {
                    _logger.Information("Decal DISABLED, Multi-client ENABLED - using OPLauncher.Hook.dll for multi-client");

                    // Multi-client requires injector.dll + OPLauncher.Hook.dll for mutex bypass
                    if (!_decalService.IsInjectorDllAvailable())
                    {
                        _logger.Error("Multi-client requires injector.dll which was not found");
                        return LaunchResult.CreateFailure(
                            "Multi-client requires injector.dll. Please reinstall the launcher or disable multi-client.",
                            connection.WorldId,
                            connection.WorldName);
                    }

                    if (!_decalService.IsMultiClientHookAvailable())
                    {
                        _logger.Error("Multi-client requires OPLauncher.Hook.dll which was not found");
                        return LaunchResult.CreateFailure(
                            "Multi-client requires OPLauncher.Hook.dll. Please reinstall the launcher or disable multi-client.",
                            connection.WorldId,
                            connection.WorldName);
                    }

                    // Use direct injection for multi-client (this provides the mutex bypass)
                    _logger.Information("Multi-client validation passed - using injector.dll + OPLauncher.Hook.dll for mutex bypass");
                    useDirectInjection = true;
                    useMultiClientLaunch = true;
                }
                else
                {
                    _logger.Information("Decal DISABLED, Multi-client DISABLED - using standard launch");
                }

                // Validate connection information
                if (string.IsNullOrWhiteSpace(connection.Host))
                {
                    _logger.Error("Connection host is empty for world {WorldId}", connection.WorldId);
                    return LaunchResult.CreateFailure(
                        "Connection host is not available for this world",
                        connection.WorldId,
                        connection.WorldName);
                }

                if (connection.Port <= 0 || connection.Port > 65535)
                {
                    _logger.Error("Invalid port number {Port} for world {WorldId}", connection.Port, connection.WorldId);
                    return LaunchResult.CreateFailure(
                        $"Invalid port number: {connection.Port}",
                        connection.WorldId,
                        connection.WorldName);
                }

                // Build command-line arguments (different for Decal wrapper vs direct launch)
                string arguments;
                if (useDecal && !useDirectInjection)
                {
                    // Decal wrapper method: build args for Decal.exe
                    arguments = BuildDecalCommandLineArguments(acClientPath!, connection, credential);
                }
                else
                {
                    // Direct launch, Decal injection, or multi-client hook: build args for acclient.exe
                    arguments = BuildCommandLineArguments(connection, credential);
                }

                // Log launch details (without sensitive data)
                _logger.Information("Launch details:");
                _logger.Information("  - Executable Path: {ExecutablePath}", executablePath);
                _logger.Information("  - Using Decal Injection: {UseDecalInjection}", useDecalInjection);
                _logger.Information("  - Using Multi-Client Hook: {UseMultiClientHook}", useMultiClientLaunch);
                _logger.Information("  - Direct Injection: {DirectInjection}", useDirectInjection);
                if (useDirectInjection)
                {
                    _logger.Information("  - AC Client Path: {ClientPath}", acClientPath);
                    if (useDecalInjection)
                    {
                        _logger.Information("  - Injection DLL: Decal Inject.dll");
                    }
                    else if (useMultiClientLaunch)
                    {
                        _logger.Information("  - Injection DLL: OPLauncher.Hook.dll");
                    }
                }
                _logger.Information("  - Server Type: {ServerType}", connection.ServerType);
                _logger.Information("  - Host: {Host}", connection.Host);
                _logger.Information("  - Port: {Port}", connection.Port);
                _logger.Information("  - Using Credentials: {HasCredentials}", credential != null);
                if (credential != null)
                {
                    _logger.Information("  - Username: {Username}", credential.Username);
                }

                // Launch the process
                // Priority: Decal injection > Multi-client hook > Standard launch
                LaunchResult result;
                if (useDirectInjection)
                {
                    // Direct injection (for Decal OR multi-client hook)
                    if (useDecalInjection)
                    {
                        // Decal injection: Use Decal's Inject.dll (handles multi-client via Dual Log if enabled)
                        _logger.Information("Using Decal injection (injector.dll + Decal Inject.dll)");
                        result = LaunchProcessWithDirectInjection(acClientPath!, arguments, connection, credential, cancellationToken);
                    }
                    else if (useMultiClientLaunch)
                    {
                        // Multi-client only: Use our hook DLL
                        _logger.Information("Using multi-client hook injection (injector.dll + OPLauncher.Hook.dll)");
                        result = LaunchProcessWithMultiClientHook(acClientPath!, arguments, connection, credential, cancellationToken);
                    }
                    else
                    {
                        // Shouldn't reach here, but fallback to standard launch
                        _logger.Warning("Direct injection flag set but no specific injection method - falling back to standard launch");
                        result = LaunchProcess(executablePath, arguments, connection, credential, cancellationToken);
                    }
                }
                else
                {
                    // Standard launch (or Decal wrapper)
                    _logger.Information("Using standard launch path");
                    result = LaunchProcess(executablePath, arguments, connection, credential, cancellationToken);
                }

                // Update credential last used timestamp if launch was successful
                if (result.Success && credential != null)
                {
                    _ = _credentialVaultService.UpdateLastUsedAsync(credential.WorldId, credential.Username);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error while launching AC client for world {WorldId}", connection.WorldId);
                return LaunchResult.CreateFailure(
                    $"Unexpected error: {ex.Message}",
                    connection.WorldId,
                    connection.WorldName);
            }
        });
    }

    /// <summary>
    /// Builds the command-line arguments for launching the AC client.
    /// Supports both ACE and GDLE server formats based on ServerType.
    /// ACE Format: -a {username} -v {password} -h {host}:{port} -rodat off
    /// GDLE Format: -h {host} -p {port} -a {username}:{password} -rodat off
    /// </summary>
    /// <param name="connection">The connection information.</param>
    /// <param name="credential">Optional credential for auto-login.</param>
    /// <returns>The formatted command-line arguments string.</returns>
    private string BuildCommandLineArguments(WorldConnectionDto connection, SavedCredential? credential)
    {
        // Switch between ACE and GDLE formats based on ServerType
        return connection.ServerType switch
        {
            ServerType.ACE => BuildAceArguments(connection, credential),
            ServerType.GDLE => BuildGdleArguments(connection, credential),
            _ => BuildAceArguments(connection, credential) // Default to ACE
        };
    }

    /// <summary>
    /// Builds ACE-format command-line arguments.
    /// Format: -a accountName -v password -h host:port -rodat off
    /// </summary>
    private string BuildAceArguments(WorldConnectionDto connection, SavedCredential? credential)
    {
        var args = "";

        // Add account credentials if provided (must come first for ACE)
        if (credential != null)
        {
            try
            {
                // Decrypt the password
                var password = _credentialVaultService.DecryptPassword(credential);

                if (!string.IsNullOrWhiteSpace(password))
                {
                    // ACE format: -a accountName -v password
                    args += $"-a {credential.Username} -v {password}";

                    // Secure memory cleanup: clear the password string from memory
                    ClearPasswordFromMemory(ref password);
                }
                else
                {
                    _logger.Warning("Failed to decrypt password for credential {Username}. Launching without auto-login.",
                        credential.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error decrypting password for credential {Username}. Launching without auto-login.",
                    credential?.Username);
            }
        }

        // Add host and port in ACE format: -h host:port
        if (!string.IsNullOrWhiteSpace(args))
        {
            args += " ";
        }
        args += $"-h {connection.Host}:{connection.Port}";

        // Add RODAT setting (off by default - uses custom server data files)
        args += " -rodat off";

        return args;
    }

    /// <summary>
    /// Builds GDLE-format command-line arguments.
    /// Format: -h host -p port -a username:password -rodat off
    /// </summary>
    private string BuildGdleArguments(WorldConnectionDto connection, SavedCredential? credential)
    {
        var args = "";

        // GDLE format: -h host -p port comes first
        args += $"-h {connection.Host} -p {connection.Port}";

        // Add account credentials if provided
        if (credential != null)
        {
            try
            {
                // Decrypt the password
                var password = _credentialVaultService.DecryptPassword(credential);

                if (!string.IsNullOrWhiteSpace(password))
                {
                    // GDLE format: -a username:password
                    args += $" -a {credential.Username}:{password}";

                    // Secure memory cleanup: clear the password string from memory
                    ClearPasswordFromMemory(ref password);
                }
                else
                {
                    _logger.Warning("Failed to decrypt password for credential {Username}. Launching without auto-login.",
                        credential.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error decrypting password for credential {Username}. Launching without auto-login.",
                    credential?.Username);
            }
        }

        // Add RODAT setting (off by default - uses custom server data files)
        args += " -rodat off";

        return args;
    }

    /// <summary>
    /// Builds command-line arguments for launching via Decal.
    /// Format: /decal "path\to\acclient.exe" -a accountName -v password -h host:port -rodat off
    /// </summary>
    /// <param name="acClientPath">The path to acclient.exe.</param>
    /// <param name="connection">The connection information.</param>
    /// <param name="credential">Optional credential for auto-login.</param>
    /// <returns>The formatted command-line arguments for Decal.</returns>
    private string BuildDecalCommandLineArguments(string acClientPath, WorldConnectionDto connection, SavedCredential? credential)
    {
        // Start with /decal flag and path to AC client (quoted)
        var args = $"/decal \"{acClientPath}\"";

        // Add account credentials if provided (ACE format for Decal)
        if (credential != null)
        {
            try
            {
                // Decrypt the password
                var password = _credentialVaultService.DecryptPassword(credential);

                if (!string.IsNullOrWhiteSpace(password))
                {
                    // Decal uses ACE format: -a accountName -v password
                    args += $" -a {credential.Username} -v {password}";

                    // Secure memory cleanup: clear the password string from memory
                    ClearPasswordFromMemory(ref password);
                }
                else
                {
                    _logger.Warning("Failed to decrypt password for credential {Username}. Launching without auto-login.",
                        credential.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error decrypting password for credential {Username}. Launching without auto-login.",
                    credential?.Username);
            }
        }

        // Add host and port in ACE format: -h host:port
        args += $" -h {connection.Host}:{connection.Port}";

        // Add RODAT setting (off by default - uses custom server data files)
        args += " -rodat off";

        return args;
    }

    /// <summary>
    /// Launches the AC client process with the specified arguments.
    /// </summary>
    /// <param name="clientPath">The full path to acclient.exe or Decal.exe.</param>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="connection">The connection information for logging.</param>
    /// <param name="credential">The credential for logging (optional).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the launch during verification.</param>
    /// <returns>A LaunchResult indicating success or failure.</returns>
    private LaunchResult LaunchProcess(
        string? clientPath,
        string arguments,
        WorldConnectionDto connection,
        SavedCredential? credential,
        CancellationToken cancellationToken)
    {
        Process? process = null;

        try
        {
            if (string.IsNullOrWhiteSpace(clientPath))
            {
                return LaunchResult.CreateFailure(
                    "AC client path is not configured",
                    connection.WorldId,
                    connection.WorldName);
            }

            // Get the directory containing the AC client
            var workingDirectory = Path.GetDirectoryName(clientPath);

            if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            {
                _logger.Error("AC client directory not found: {Directory}", workingDirectory);
                return LaunchResult.CreateFailure(
                    "AC client directory not found",
                    connection.WorldId,
                    connection.WorldName);
            }

            // Create process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = clientPath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false, // Don't use shell execute to avoid security issues
                CreateNoWindow = true,   // Matches ThwargLauncher - prevents "only run from launcher" error
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            _logger.Debug("Starting process: {FileName} {Arguments}", clientPath, SanitizeArgumentsForLogging(arguments));

            // Start the process (use suspended launch if multi-client enabled)
            var enableMultiClient = _configService.Current.EnableMultiClient;
            _logger.Information("=== GAME LAUNCH DECISION POINT ===");
            _logger.Information("EnableMultiClient config value: {EnableMultiClient}", enableMultiClient);
            _logger.Information("Launch method: {Method}", enableMultiClient ? "SUSPENDED PROCESS (multi-client)" : "STANDARD PROCESS (single-client)");

            if (enableMultiClient)
            {
                _logger.Information("Multi-client enabled - using suspended process launch to bypass AC mutex check");

                try
                {
                    var suspendedProcessId = Utilities.SuspendedProcessLauncher.LaunchSuspended(
                        clientPath,
                        arguments,
                        workingDirectory,
                        _logger);

                    if (suspendedProcessId <= 0)
                    {
                        _logger.Error("SuspendedProcessLauncher returned invalid PID: {ProcessId}", suspendedProcessId);
                        return LaunchResult.CreateFailure(
                            "Failed to start AC client process via suspended launch",
                            connection.WorldId,
                            connection.WorldName);
                    }

                    _logger.Debug("Suspended launch successful, getting Process object for PID {ProcessId}", suspendedProcessId);

                    // Get Process object from PID
                    process = Process.GetProcessById(suspendedProcessId);

                    if (process == null)
                    {
                        _logger.Error("Failed to get Process object for PID {ProcessId}", suspendedProcessId);
                        return LaunchResult.CreateFailure(
                            "Failed to get process handle after suspended launch",
                            connection.WorldId,
                            connection.WorldName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to launch process via suspended launch");
                    return LaunchResult.CreateFailure(
                        $"Suspended launch failed: {ex.Message}",
                        connection.WorldId,
                        connection.WorldName);
                }
            }
            else
            {
                // Standard launch (existing behavior)
                _logger.Debug("Multi-client disabled - using standard Process.Start launch");
                process = Process.Start(startInfo);

                if (process == null)
                {
                    _logger.Error("Process.Start returned null for AC client");
                    return LaunchResult.CreateFailure(
                        "Failed to start AC client process",
                        connection.WorldId,
                        connection.WorldName);
                }
            }

            // Check if the process started successfully
            if (process.HasExited)
            {
                _logger.Error("AC client process exited immediately with code: {ExitCode}", process.ExitCode);
                return LaunchResult.CreateFailure(
                    $"AC client exited immediately (Exit code: {process.ExitCode})",
                    connection.WorldId,
                    connection.WorldName);
            }

            var processId = process.Id;

            _logger.Information("AC client process started (PID: {ProcessId}). Beginning verification...", processId);
            _logger.Debug("Waiting for input idle with 30 second timeout");

            // Verify launch with WaitForInputIdle (30 second timeout)
            const int timeoutSeconds = 30;
            var startTime = DateTime.UtcNow;
            bool inputIdleReached = false;

            try
            {
                // Monitor process and wait for input idle with progress reporting
                for (int elapsed = 0; elapsed < timeoutSeconds; elapsed++)
                {
                    // Check if user cancelled
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Warning("Launch cancelled by user during verification");

                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                                _logger.Information("Killed process {ProcessId} due to cancellation", processId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Failed to kill process {ProcessId} during cancellation", processId);
                        }

                        return LaunchResult.CreateCancelled(connection.WorldId, connection.WorldName);
                    }

                    // Check if process exited early (failure)
                    if (process.HasExited)
                    {
                        var exitCode = process.ExitCode;
                        var elapsedTime = (DateTime.UtcNow - startTime).TotalSeconds;

                        if (elapsedTime < 5)
                        {
                            _logger.Error("Game crashed immediately (exit code: {ExitCode})", exitCode);
                            return LaunchResult.CreateFailure(
                                "Game crashed immediately after launch",
                                connection.WorldId,
                                connection.WorldName);
                        }
                        else
                        {
                            _logger.Error("Game failed to start - process exited with code: {ExitCode}", exitCode);
                            return LaunchResult.CreateFailure(
                                $"Game failed to start (exit code: {exitCode})",
                                connection.WorldId,
                                connection.WorldName);
                        }
                    }

                    // Try WaitForInputIdle with 1 second timeout
                    try
                    {
                        if (process.WaitForInputIdle(1000))
                        {
                            inputIdleReached = true;
                            _logger.Information("Input idle reached after {Elapsed} seconds", elapsed + 1);
                            break;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process has no UI or has exited
                        if (process.HasExited)
                        {
                            _logger.Error("Process exited during verification (exit code: {ExitCode})", process.ExitCode);
                            return LaunchResult.CreateFailure(
                                $"Game failed to start (exit code: {process.ExitCode})",
                                connection.WorldId,
                                connection.WorldName);
                        }
                        // If no UI, treat as success after a few seconds
                        if (elapsed >= 3)
                        {
                            _logger.Information("Process has no UI, treating as successful after {Elapsed} seconds", elapsed + 1);
                            inputIdleReached = true;
                            break;
                        }
                    }

                    // Fire progress event every second
                    FireProgressEvent(new LaunchProgressInfo
                    {
                        ElapsedSeconds = elapsed + 1,
                        TimeoutSeconds = timeoutSeconds,
                        StatusMessage = $"Starting game... ({elapsed + 1}s / {timeoutSeconds}s)",
                        CanCancel = true
                    });

                    // Log every 5 seconds for diagnostics
                    if ((elapsed + 1) % 5 == 0)
                    {
                        _logger.Debug("Still waiting for input idle... {Elapsed}s / {Timeout}s", elapsed + 1, timeoutSeconds);
                    }
                }

                if (!inputIdleReached)
                {
                    _logger.Warning("Input idle timeout after {Timeout} seconds. Game may still be launching.", timeoutSeconds);
                    // Don't kill process on timeout - it might still be loading
                    return LaunchResult.CreateFailure(
                        "Game took too long to start (may still be launching)",
                        connection.WorldId,
                        connection.WorldName);
                }

                _logger.Information("========================================");
                _logger.Information("AC client launched successfully!");
                _logger.Information("  - Process ID: {ProcessId}", processId);
                _logger.Information("  - World: {WorldName} ({WorldId})", connection.WorldName, connection.WorldId);
                _logger.Information("  - Server: {Host}:{Port}", connection.Host, connection.Port);
                _logger.Information("========================================");

                return LaunchResult.CreateSuccess(processId, connection.WorldId, connection.WorldName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during launch verification");
                return LaunchResult.CreateFailure(
                    $"Verification failed: {ex.Message}",
                    connection.WorldId,
                    connection.WorldName);
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // Win32Exception typically indicates file not found, access denied, etc.
            _logger.Error(ex, "Win32 error while launching AC client: {Message}", ex.Message);

            string errorMessage = ex.NativeErrorCode switch
            {
                2 => "AC client executable not found. Please check your configuration.",
                5 => "Access denied. Try running the launcher as administrator.",
                _ => $"Failed to start AC client: {ex.Message}"
            };

            return LaunchResult.CreateFailure(errorMessage, connection.WorldId, connection.WorldName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Access denied while launching AC client");
            return LaunchResult.CreateFailure(
                "Access denied. Try running the launcher as administrator.",
                connection.WorldId,
                connection.WorldName);
        }
        catch (FileNotFoundException ex)
        {
            _logger.Error(ex, "AC client executable not found: {Path}", clientPath);
            return LaunchResult.CreateFailure(
                "AC client executable not found. Please check your configuration.",
                connection.WorldId,
                connection.WorldName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error while launching AC client");
            return LaunchResult.CreateFailure(
                $"Failed to launch AC client: {ex.Message}",
                connection.WorldId,
                connection.WorldName);
        }
        finally
        {
            // Don't dispose the process - we want to allow multiple instances
            // and don't need to track them after launch
        }
    }

    /// <summary>
    /// Sanitizes command-line arguments for logging by removing password information.
    /// Handles both ACE and GDLE password formats.
    /// </summary>
    /// <param name="arguments">The raw arguments string.</param>
    /// <returns>Sanitized arguments safe for logging.</returns>
    private string SanitizeArgumentsForLogging(string arguments)
    {
        var sanitized = arguments;

        // Remove password from -v parameter for ACE format
        // ACE format: -a username -v password -h host:port
        if (sanitized.Contains("-v "))
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                @"-v\s+([^\s]+)",
                "-v ********");
        }

        // Remove password from -a parameter for GDLE format
        // GDLE format: -h host -p port -a username:password
        if (sanitized.Contains("-a ") && sanitized.Contains(":"))
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                @"-a\s+([^:]+):([^\s]+)",
                "-a $1:********");
        }

        return sanitized;
    }

    /// <summary>
    /// Clears a password string from memory for security.
    /// This helps prevent the password from being recoverable from memory dumps.
    /// </summary>
    /// <param name="password">Reference to the password string to clear.</param>
    private void ClearPasswordFromMemory(ref string? password)
    {
        if (password == null)
            return;

        try
        {
            // Overwrite the string in memory with zeros
            // Note: This is best-effort. Strings in .NET are immutable and the CLR may have
            // multiple copies. For maximum security, use SecureString in future versions.
            unsafe
            {
                fixed (char* ptr = password)
                {
                    for (int i = 0; i < password.Length; i++)
                    {
                        ptr[i] = '\0';
                    }
                }
            }

            // Set to null to help GC
            password = null;

            _logger.Debug("Password cleared from memory");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to clear password from memory");
        }
    }

    /// <summary>
    /// Validates whether the AC client can be launched with the current configuration.
    /// This is a pre-flight check that can be called before attempting to launch.
    /// </summary>
    /// <returns>Validation result with success flag and error message if applicable.</returns>
    public (bool IsValid, string? ErrorMessage) ValidateLaunchConfiguration()
    {
        var acClientPath = _configService.Current.AcClientPath;
        var (isValid, errorMessage) = _configService.ValidateAcClientPath(acClientPath);

        if (!isValid)
        {
            _logger.Debug("Launch configuration validation failed: {Error}", errorMessage);
            return (false, errorMessage);
        }

        _logger.Debug("Launch configuration is valid");
        return (true, null);
    }

    /// <summary>
    /// Launches AC client using direct Decal injection (ThwargLauncher method)
    /// </summary>
    private LaunchResult LaunchProcessWithDirectInjection(
        string acClientPath,
        string arguments,
        WorldConnectionDto connection,
        SavedCredential? credential,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get working directory
            var workingDirectory = Path.GetDirectoryName(acClientPath);
            if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            {
                _logger.Error("AC client directory not found: {Directory}", workingDirectory);
                return LaunchResult.CreateFailure(
                    "AC client directory not found",
                    connection.WorldId,
                    connection.WorldName);
            }

            _logger.Debug("Calling DecalService.LaunchWithDecalInjection");
            _logger.Debug("  AC Client: {Path}", acClientPath);
            _logger.Debug("  Arguments: {Args}", SanitizeArgumentsForLogging(arguments));
            _logger.Debug("  Working Dir: {Dir}", workingDirectory);

            // Call DecalService to perform injection
            int processId = _decalService.LaunchWithDecalInjection(acClientPath, arguments, workingDirectory);

            if (processId <= 0)
            {
                _logger.Error("Direct Decal injection failed - LaunchWithDecalInjection returned {ProcessId}", processId);
                return LaunchResult.CreateFailure(
                    "Failed to launch AC client with Decal injection. Check logs for details.",
                    connection.WorldId,
                    connection.WorldName);
            }

            _logger.Information("AC client launched with direct Decal injection (PID: {ProcessId})", processId);

            // Get process handle for verification
            Process? process = null;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                _logger.Error("Process {ProcessId} not found - may have crashed immediately", processId);
                return LaunchResult.CreateFailure(
                    "Game process not found after launch",
                    connection.WorldId,
                    connection.WorldName);
            }

            // Verify process didn't exit immediately
            if (process.HasExited)
            {
                _logger.Error("AC client process {ProcessId} exited immediately with code: {ExitCode}",
                    processId, process.ExitCode);
                return LaunchResult.CreateFailure(
                    $"AC client exited immediately (Exit code: {process.ExitCode})",
                    connection.WorldId,
                    connection.WorldName);
            }

            _logger.Information("Direct injection successful - beginning verification...");

            // Perform launch verification (same as standard launch)
            const int timeoutSeconds = 30;
            var startTime = DateTime.UtcNow;
            bool inputIdleReached = false;

            try
            {
                for (int elapsed = 0; elapsed < timeoutSeconds; elapsed++)
                {
                    // Check cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Warning("Launch cancelled by user during verification");
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                                _logger.Information("Killed process {ProcessId} due to cancellation", processId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Failed to kill process {ProcessId} during cancellation", processId);
                        }
                        return LaunchResult.CreateCancelled(connection.WorldId, connection.WorldName);
                    }

                    // Check if process exited early
                    if (process.HasExited)
                    {
                        var exitCode = process.ExitCode;
                        var elapsedTime = (DateTime.UtcNow - startTime).TotalSeconds;

                        if (elapsedTime < 5)
                        {
                            _logger.Error("Game crashed immediately (exit code: {ExitCode})", exitCode);
                            return LaunchResult.CreateFailure(
                                "Game crashed immediately after launch",
                                connection.WorldId,
                                connection.WorldName);
                        }
                        else
                        {
                            _logger.Error("Game failed to start - process exited with code: {ExitCode}", exitCode);
                            return LaunchResult.CreateFailure(
                                $"Game failed to start (exit code: {exitCode})",
                                connection.WorldId,
                                connection.WorldName);
                        }
                    }

                    // Try WaitForInputIdle
                    try
                    {
                        if (process.WaitForInputIdle(1000))
                        {
                            inputIdleReached = true;
                            _logger.Information("Input idle reached after {Elapsed} seconds", elapsed + 1);
                            break;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        if (process.HasExited)
                        {
                            _logger.Error("Process exited during verification (exit code: {ExitCode})", process.ExitCode);
                            return LaunchResult.CreateFailure(
                                $"Game failed to start (exit code: {process.ExitCode})",
                                connection.WorldId,
                                connection.WorldName);
                        }
                        if (elapsed >= 3)
                        {
                            _logger.Information("Process has no UI, treating as successful after {Elapsed} seconds", elapsed + 1);
                            inputIdleReached = true;
                            break;
                        }
                    }

                    // Fire progress event
                    FireProgressEvent(new LaunchProgressInfo
                    {
                        ElapsedSeconds = elapsed + 1,
                        TimeoutSeconds = timeoutSeconds,
                        StatusMessage = $"Starting game with Decal... ({elapsed + 1}s / {timeoutSeconds}s)",
                        CanCancel = true
                    });

                    if ((elapsed + 1) % 5 == 0)
                    {
                        _logger.Debug("Still waiting for input idle... {Elapsed}s / {Timeout}s", elapsed + 1, timeoutSeconds);
                    }
                }

                if (!inputIdleReached)
                {
                    _logger.Warning("Input idle timeout after {Timeout} seconds. Game may still be launching.", timeoutSeconds);
                    return LaunchResult.CreateFailure(
                        "Game took too long to start (may still be launching)",
                        connection.WorldId,
                        connection.WorldName);
                }

                _logger.Information("========================================");
                _logger.Information("AC client launched successfully with Decal injection!");
                _logger.Information("  - Process ID: {ProcessId}", processId);
                _logger.Information("  - World: {WorldName} ({WorldId})", connection.WorldName, connection.WorldId);
                _logger.Information("  - Server: {Host}:{Port}", connection.Host, connection.Port);
                _logger.Information("  - Decal: Direct injection (ThwargLauncher method)");
                _logger.Information("========================================");

                return LaunchResult.CreateSuccess(processId, connection.WorldId, connection.WorldName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during launch verification");
                return LaunchResult.CreateFailure(
                    $"Verification failed: {ex.Message}",
                    connection.WorldId,
                    connection.WorldName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error during direct Decal injection launch");
            return LaunchResult.CreateFailure(
                $"Failed to launch with Decal injection: {ex.Message}",
                connection.WorldId,
                connection.WorldName);
        }
    }

    /// <summary>
    /// Launches AC client with multi-client hook injection (no Decal required)
    /// Uses OPLauncher.Hook.dll to hook the AC mutex check
    /// </summary>
    private LaunchResult LaunchProcessWithMultiClientHook(
        string acClientPath,
        string arguments,
        WorldConnectionDto connection,
        SavedCredential? credential,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get working directory
            var workingDirectory = Path.GetDirectoryName(acClientPath);
            if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            {
                _logger.Error("AC client directory not found: {Directory}", workingDirectory);
                return LaunchResult.CreateFailure(
                    "AC client directory not found",
                    connection.WorldId,
                    connection.WorldName);
            }

            _logger.Debug("Calling DecalService.LaunchWithMultiClientHook");
            _logger.Debug("  AC Client: {Path}", acClientPath);
            _logger.Debug("  Arguments: {Args}", SanitizeArgumentsForLogging(arguments));
            _logger.Debug("  Working Dir: {Dir}", workingDirectory);

            // Call DecalService to perform multi-client hook injection
            int processId = _decalService.LaunchWithMultiClientHook(acClientPath, arguments, workingDirectory);

            if (processId <= 0)
            {
                _logger.Error("Multi-client hook injection failed - LaunchWithMultiClientHook returned {ProcessId}", processId);
                return LaunchResult.CreateFailure(
                    "Failed to launch AC client with multi-client hook. Check logs for details.",
                    connection.WorldId,
                    connection.WorldName);
            }

            _logger.Information("AC client launched with multi-client hook (PID: {ProcessId})", processId);

            // Get process handle for verification
            Process? process = null;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                _logger.Error("Process {ProcessId} not found - may have crashed immediately", processId);
                return LaunchResult.CreateFailure(
                    "Game process not found after launch",
                    connection.WorldId,
                    connection.WorldName);
            }

            // Verify process didn't exit immediately
            if (process.HasExited)
            {
                _logger.Error("AC client process {ProcessId} exited immediately with code: {ExitCode}",
                    processId, process.ExitCode);
                return LaunchResult.CreateFailure(
                    $"AC client exited immediately (Exit code: {process.ExitCode})",
                    connection.WorldId,
                    connection.WorldName);
            }

            _logger.Information("Multi-client hook injection successful - beginning verification...");

            // Perform launch verification (same as standard launch)
            const int timeoutSeconds = 30;
            var startTime = DateTime.UtcNow;
            bool inputIdleReached = false;

            // Fire initial progress event to show the launch dialog
            FireProgressEvent(new LaunchProgressInfo
            {
                ElapsedSeconds = 0,
                TimeoutSeconds = timeoutSeconds,
                StatusMessage = "Starting game...",
                CanCancel = true
            });

            try
            {
                for (int elapsed = 0; elapsed < timeoutSeconds; elapsed++)
                {
                    // Check cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Warning("Launch cancelled by user during verification");

                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                                _logger.Information("Killed process {ProcessId} due to cancellation", processId);
                            }
                        }
                        catch (Exception killEx)
                        {
                            _logger.Warning(killEx, "Failed to kill process {ProcessId} during cancellation", processId);
                        }

                        return LaunchResult.CreateFailure(
                            "Launch cancelled by user",
                            connection.WorldId,
                            connection.WorldName);
                    }

                    // Check if process exited
                    if (process.HasExited)
                    {
                        _logger.Error("Process exited during verification (exit code: {ExitCode})", process.ExitCode);
                        return LaunchResult.CreateFailure(
                            $"Game failed to start (exit code: {process.ExitCode})",
                            connection.WorldId,
                            connection.WorldName);
                    }

                    // Try WaitForInputIdle with 1 second timeout
                    try
                    {
                        if (process.WaitForInputIdle(1000))
                        {
                            inputIdleReached = true;
                            _logger.Information("Process reached input idle state");
                            break;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process has no UI - this is actually fine for headless servers
                        // Check if process still running for a few seconds
                        if (process.HasExited)
                        {
                            _logger.Error("Process exited during verification (exit code: {ExitCode})", process.ExitCode);
                            return LaunchResult.CreateFailure(
                                $"Game failed to start (exit code: {process.ExitCode})",
                                connection.WorldId,
                                connection.WorldName);
                        }
                        if (elapsed >= 3)
                        {
                            _logger.Information("Process has no UI, treating as successful after {Elapsed} seconds", elapsed + 1);
                            inputIdleReached = true;
                            break;
                        }
                    }

                    // Fire progress event
                    FireProgressEvent(new LaunchProgressInfo
                    {
                        ElapsedSeconds = elapsed + 1,
                        TimeoutSeconds = timeoutSeconds,
                        StatusMessage = $"Verifying game startup... ({elapsed + 1}/{timeoutSeconds}s)",
                        CanCancel = true
                    });

                    Thread.Sleep(1000);
                }

                if (!inputIdleReached)
                {
                    _logger.Warning("Process did not reach input idle within {Timeout} seconds, but is still running", timeoutSeconds);
                    // Don't fail the launch - some configurations may take longer or have no UI
                }
            }
            catch (Exception verifyEx)
            {
                _logger.Warning(verifyEx, "Error during verification, but process appears to be running");
                // Don't fail the launch
            }

            // Success!
            var elapsedTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.Information("✓ Launch completed successfully in {ElapsedTime:F1} seconds", elapsedTime);

            // Fire completion event to hide the launch dialog
            FireProgressEvent(new LaunchProgressInfo
            {
                ElapsedSeconds = timeoutSeconds, // Use timeout value to ensure dialog closes
                TimeoutSeconds = timeoutSeconds,
                StatusMessage = "Launch completed successfully!",
                CanCancel = false
            });

            return LaunchResult.CreateSuccess(
                processId,
                connection.WorldId,
                connection.WorldName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error during multi-client hook injection launch");

            // Fire completion event to hide the launch dialog on error
            FireProgressEvent(new LaunchProgressInfo
            {
                ElapsedSeconds = 30,
                TimeoutSeconds = 30,
                StatusMessage = "Launch failed",
                CanCancel = false
            });

            return LaunchResult.CreateFailure(
                $"Failed to launch with multi-client hook: {ex.Message}",
                connection.WorldId,
                connection.WorldName);
        }
    }

    /// <summary>
    /// Gets diagnostic information about the AC client configuration.
    /// Useful for troubleshooting and support.
    /// </summary>
    /// <returns>A dictionary of diagnostic information.</returns>
    public Dictionary<string, string> GetDiagnosticInfo()
    {
        var info = new Dictionary<string, string>();

        var acClientPath = _configService.Current.AcClientPath;
        info["AC Client Path"] = acClientPath ?? "(not configured)";

        if (!string.IsNullOrWhiteSpace(acClientPath))
        {
            info["Client Exists"] = File.Exists(acClientPath).ToString();

            if (File.Exists(acClientPath))
            {
                try
                {
                    var fileInfo = new FileInfo(acClientPath);
                    info["File Size"] = $"{fileInfo.Length:N0} bytes";
                    info["Last Modified"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

                    var versionInfo = FileVersionInfo.GetVersionInfo(acClientPath);
                    info["File Version"] = versionInfo.FileVersion ?? "Unknown";
                    info["Product Name"] = versionInfo.ProductName ?? "Unknown";
                }
                catch (Exception ex)
                {
                    info["File Info Error"] = ex.Message;
                }
            }

            var directory = Path.GetDirectoryName(acClientPath);
            info["Working Directory"] = directory ?? "(unknown)";
            info["Directory Exists"] = (directory != null && Directory.Exists(directory)).ToString();
        }

        var (isValid, errorMessage) = ValidateLaunchConfiguration();
        info["Configuration Valid"] = isValid.ToString();
        if (!isValid)
        {
            info["Validation Error"] = errorMessage ?? "Unknown error";
        }

        return info;
    }
}

