using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OPLauncher.DTOs;
using OPLauncher.Models;

namespace OPLauncher.Services;

/// <summary>
/// Model representing a single launch task in a multi-client launch sequence.
/// </summary>
public class LaunchTask
{
    /// <summary>
    /// Gets or sets the world connection information.
    /// </summary>
    public WorldConnectionDto Connection { get; set; } = null!;

    /// <summary>
    /// Gets or sets the saved credential for this account.
    /// </summary>
    public SavedCredential Credential { get; set; } = null!;

    /// <summary>
    /// Gets or sets the order in which this task should be executed.
    /// Lower values execute first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the delay in seconds to wait after launching this client.
    /// </summary>
    public int DelaySeconds { get; set; }

    /// <summary>
    /// Gets or sets optional notes or description for this launch task.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Event args for when a launch task starts.
/// </summary>
public class LaunchStartedEventArgs : EventArgs
{
    public LaunchTask Task { get; set; } = null!;
    public int TaskNumber { get; set; }
    public int TotalTasks { get; set; }
}

/// <summary>
/// Event args for when a launch task completes.
/// </summary>
public class LaunchCompletedEventArgs : EventArgs
{
    public LaunchTask Task { get; set; } = null!;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TaskNumber { get; set; }
    public int TotalTasks { get; set; }
}

/// <summary>
/// Event args for when the entire launch sequence completes.
/// </summary>
public class SequenceCompletedEventArgs : EventArgs
{
    public int TotalTasks { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public bool WasCancelled { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

/// <summary>
/// Event args for launch progress updates.
/// </summary>
public class LaunchProgressEventArgs : EventArgs
{
    public int LaunchedCount { get; set; }
    public int TotalClients { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

/// <summary>
/// Result of a launch sequence operation.
/// </summary>
public class LaunchSequenceResult
{
    public bool Success { get; set; }
    public int TotalTasks { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public bool WasCancelled { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<string> Errors { get; set; } = new();
    public string Summary => $"Launched {SuccessCount}/{TotalTasks} clients successfully";
}

/// <summary>
/// Service for orchestrating sequential multi-client game launches with delays and progress tracking.
/// Coordinates multiple GameLaunchService calls with configurable delays between each launch.
/// </summary>
public class LaunchSequencerService
{
    private readonly GameLaunchService _gameLaunchService;
    private readonly ConfigService _configService;
    private readonly UserPreferencesManager _userPreferencesManager;
    private readonly DecalService _decalService;
    private readonly LoggingService _logger;

    /// <summary>
    /// Gets whether a launch sequence is currently in progress.
    /// </summary>
    public bool IsLaunching { get; private set; }

    /// <summary>
    /// Gets the total number of clients in the current/last sequence.
    /// </summary>
    public int TotalClients { get; private set; }

    /// <summary>
    /// Gets the number of clients launched so far in the current sequence.
    /// </summary>
    public int LaunchedCount { get; private set; }

    /// <summary>
    /// Gets the number of successful launches in the current sequence.
    /// </summary>
    public int SuccessCount { get; private set; }

    /// <summary>
    /// Gets the number of failed launches in the current sequence.
    /// </summary>
    public int FailureCount { get; private set; }

    /// <summary>
    /// Event fired when a launch task starts.
    /// </summary>
    public event EventHandler<LaunchStartedEventArgs>? LaunchStarted;

    /// <summary>
    /// Event fired when a launch task completes.
    /// </summary>
    public event EventHandler<LaunchCompletedEventArgs>? LaunchCompleted;

    /// <summary>
    /// Event fired when the entire launch sequence completes.
    /// </summary>
    public event EventHandler<SequenceCompletedEventArgs>? SequenceCompleted;

    /// <summary>
    /// Event fired when launch progress is updated.
    /// </summary>
    public event EventHandler<LaunchProgressEventArgs>? ProgressUpdated;

    /// <summary>
    /// Initializes a new instance of the LaunchSequencerService.
    /// </summary>
    /// <param name="gameLaunchService">The game launch service for launching individual clients.</param>
    /// <param name="configService">The configuration service for validation checks.</param>
    /// <param name="userPreferencesManager">The UserPreferences manager for validation checks.</param>
    /// <param name="logger">The logging service.</param>
    public LaunchSequencerService(
        GameLaunchService gameLaunchService,
        ConfigService configService,
        UserPreferencesManager userPreferencesManager,
        DecalService decalService,
        LoggingService logger)
    {
        _gameLaunchService = gameLaunchService;
        _configService = configService;
        _userPreferencesManager = userPreferencesManager;
        _decalService = decalService;
        _logger = logger;
    }

    /// <summary>
    /// Validates pre-launch conditions for multi-client launch.
    /// </summary>
    /// <param name="taskCount">The number of clients being launched.</param>
    /// <returns>A tuple containing (isValid, errorMessage).</returns>
    private (bool isValid, string? errorMessage) ValidatePreLaunch(int taskCount)
    {
        _logger.Information("=== MULTI-CLIENT VALIDATION START ===");
        _logger.Information("Validating multi-client pre-launch conditions for {TaskCount} clients", taskCount);

        // Check if multi-client is enabled
        var enableMultiClient = _configService.Current.EnableMultiClient;
        _logger.Information("  [CHECK 1/7] EnableMultiClient setting: {EnableMultiClient}", enableMultiClient);
        if (!enableMultiClient)
        {
            _logger.Warning("  VALIDATION FAILED: Multi-client is disabled in config");
            return (false, "Multi-client support is disabled. Enable it in Settings.");
        }
        _logger.Information("  [CHECK 1/7] ✓ PASSED");

        // Check UserPreferences.ini configuration
        _logger.Information("  [CHECK 2/7] Checking UserPreferences.ini configuration...");
        var userPrefsPath = _userPreferencesManager.GetUserPreferencesPath();
        _logger.Information("    UserPreferences.ini path: {Path}", userPrefsPath);
        _logger.Information("    File exists: {Exists}", _userPreferencesManager.FileExists());

        try
        {
            var isComputeUniquePortEnabled = _userPreferencesManager.IsComputeUniquePortEnabled();
            _logger.Information("    ComputeUniquePort enabled: {Enabled}", isComputeUniquePortEnabled);

            if (!isComputeUniquePortEnabled)
            {
                _logger.Warning("  VALIDATION FAILED: ComputeUniquePort is not enabled in UserPreferences.ini");
                return (false, "UserPreferences.ini is not configured for multi-client (ComputeUniquePort=True required). Configure it in Settings → Multi-Client Settings.");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "  VALIDATION FAILED: Error checking UserPreferences.ini configuration");
            return (false, $"Error checking UserPreferences.ini: {ex.Message}. Ensure the file exists and is readable.");
        }
        _logger.Information("  [CHECK 2/7] ✓ PASSED");

        // Check AC client path is valid
        _logger.Information("  [CHECK 3/7] Checking AC client path...");
        var acPath = _configService.Current.AcClientPath;
        _logger.Information("    AC client path: {Path}", acPath ?? "(null)");
        _logger.Information("    File exists: {Exists}", !string.IsNullOrWhiteSpace(acPath) && System.IO.File.Exists(acPath));

        if (string.IsNullOrWhiteSpace(acPath) || !System.IO.File.Exists(acPath))
        {
            _logger.Warning("  VALIDATION FAILED: AC client path is invalid or not set");
            return (false, "AC client path is invalid or not set. Configure it in Settings.");
        }
        _logger.Information("  [CHECK 3/7] ✓ PASSED");

        // Check minimum task count for multi-launch
        _logger.Information("  [CHECK 4/7] Checking minimum task count...");
        _logger.Information("    Task count: {TaskCount} (minimum: 2)", taskCount);

        if (taskCount < 2)
        {
            _logger.Warning("  VALIDATION FAILED: Task count ({TaskCount}) is less than minimum (2)", taskCount);
            return (false, "Multi-client launch requires at least 2 accounts. Select more accounts.");
        }
        _logger.Information("  [CHECK 4/7] ✓ PASSED");

        // Check max simultaneous clients limit
        _logger.Information("  [CHECK 5/7] Checking max simultaneous clients limit...");
        var maxClients = _configService.Current.MaxSimultaneousClients;
        _logger.Information("    Task count: {TaskCount}, Max allowed: {MaxClients}", taskCount, maxClients);

        if (taskCount > maxClients)
        {
            _logger.Warning("  VALIDATION FAILED: Task count ({TaskCount}) exceeds max limit ({MaxClients})", taskCount, maxClients);
            return (false, $"Cannot launch {taskCount} clients (your configured limit is {maxClients}). Reduce selection or increase limit in Settings.");
        }
        _logger.Information("  [CHECK 5/7] ✓ PASSED");

        // Check injector.dll is available (required for multi-client)
        _logger.Information("  [CHECK 6/7] Checking injector.dll availability...");
        var injectorAvailable = _decalService.IsInjectorDllAvailable();
        _logger.Information("    injector.dll available: {Available}", injectorAvailable);

        if (!injectorAvailable)
        {
            _logger.Warning("  VALIDATION FAILED: injector.dll not found");
            return (false, "Multi-client requires injector.dll which was not found. Please reinstall the launcher or disable multi-client.");
        }
        _logger.Information("  [CHECK 6/7] ✓ PASSED");

        // Check OPLauncher.Hook.dll is available (required for multi-client mutex bypass)
        _logger.Information("  [CHECK 7/7] Checking OPLauncher.Hook.dll availability...");
        var hookAvailable = _decalService.IsMultiClientHookAvailable();
        _logger.Information("    OPLauncher.Hook.dll available: {Available}", hookAvailable);

        if (!hookAvailable)
        {
            _logger.Warning("  VALIDATION FAILED: OPLauncher.Hook.dll not found");
            return (false, "Multi-client requires OPLauncher.Hook.dll which was not found. Please reinstall the launcher or disable multi-client.");
        }
        _logger.Information("  [CHECK 7/7] ✓ PASSED");

        // All validations passed
        _logger.Information("=== MULTI-CLIENT VALIDATION PASSED - ALL CHECKS OK ===");
        return (true, null);
    }

    /// <summary>
    /// Launches a sequence of game clients with configurable delays between each.
    /// </summary>
    /// <param name="tasks">The list of launch tasks to execute.</param>
    /// <param name="abortOnFailure">If true, stops the sequence on first failure.</param>
    /// <param name="cancellationToken">Cancellation token to stop the sequence.</param>
    /// <returns>A result object with summary information.</returns>
    public async Task<LaunchSequenceResult> LaunchSequenceAsync(
        List<LaunchTask> tasks,
        bool abortOnFailure = false,
        CancellationToken cancellationToken = default)
    {
        if (IsLaunching)
        {
            throw new InvalidOperationException("A launch sequence is already in progress");
        }

        if (tasks == null || tasks.Count == 0)
        {
            throw new ArgumentException("Task list cannot be null or empty", nameof(tasks));
        }

        // Validate pre-launch conditions
        var (isValid, errorMessage) = ValidatePreLaunch(tasks.Count);
        if (!isValid)
        {
            _logger.Warning("Pre-launch validation failed: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.Information("Starting launch sequence: {Count} clients, abortOnFailure={AbortOnFailure}",
            tasks.Count, abortOnFailure);

        var startTime = DateTime.UtcNow;
        var result = new LaunchSequenceResult
        {
            TotalTasks = tasks.Count
        };

        try
        {
            IsLaunching = true;
            TotalClients = tasks.Count;
            LaunchedCount = 0;
            SuccessCount = 0;
            FailureCount = 0;

            // Sort tasks by Order property
            var sortedTasks = tasks.OrderBy(t => t.Order).ToList();

            // Execute each task in sequence
            for (int i = 0; i < sortedTasks.Count; i++)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Warning("Launch sequence cancelled by user at task {TaskNumber}/{TotalTasks}",
                        i + 1, TotalClients);
                    result.WasCancelled = true;
                    break;
                }

                var task = sortedTasks[i];
                var taskNumber = i + 1;

                _logger.Information("========================================");
                _logger.Information("Launching client {TaskNumber}/{TotalTasks}", taskNumber, TotalClients);
                _logger.Information("  Account: {Account}", task.Credential.GetDisplayText());
                _logger.Information("  World: {World}", task.Connection.WorldName);
                _logger.Information("  Delay after launch: {Delay}s", task.DelaySeconds);
                _logger.Information("========================================");

                // Fire LaunchStarted event
                OnLaunchStarted(new LaunchStartedEventArgs
                {
                    Task = task,
                    TaskNumber = taskNumber,
                    TotalTasks = TotalClients
                });

                // Update progress
                OnProgressUpdated(new LaunchProgressEventArgs
                {
                    LaunchedCount = LaunchedCount,
                    TotalClients = TotalClients,
                    StatusMessage = $"Launching {task.Credential.GetDisplayText()}..."
                });

                // Launch the client
                var launchResult = await _gameLaunchService.LaunchGameAsync(
                    task.Connection,
                    task.Credential);

                LaunchedCount++;

                // Process result
                bool taskSuccess = launchResult.Success;
                _logger.Information("=== LAUNCH RESULT FOR CLIENT {TaskNumber}/{TotalTasks} ===", taskNumber, TotalClients);
                _logger.Information("  Success: {Success}", taskSuccess);
                _logger.Information("  Process ID: {ProcessId}", launchResult.ProcessId);
                _logger.Information("  Error: {Error}", launchResult.ErrorMessage ?? "(none)");

                if (taskSuccess)
                {
                    SuccessCount++;
                    _logger.Information("✓ Client {TaskNumber} launched successfully: {Account}",
                        taskNumber, task.Credential.GetDisplayText());
                }
                else
                {
                    FailureCount++;
                    var errorMsg = launchResult.ErrorMessage ?? "Unknown error";
                    result.Errors.Add($"Task {taskNumber} ({task.Credential.GetDisplayText()}): {errorMsg}");
                    _logger.Error("✗ Client {TaskNumber} failed to launch: {Error}",
                        taskNumber, errorMsg);
                }

                // Fire LaunchCompleted event
                OnLaunchCompleted(new LaunchCompletedEventArgs
                {
                    Task = task,
                    Success = taskSuccess,
                    ErrorMessage = launchResult.ErrorMessage,
                    TaskNumber = taskNumber,
                    TotalTasks = TotalClients
                });

                // Check if we should abort on failure
                if (!taskSuccess && abortOnFailure)
                {
                    _logger.Warning("Aborting launch sequence due to failure at task {TaskNumber}",
                        taskNumber);
                    break;
                }

                // Wait for delay before next launch (unless this is the last task)
                if (i < sortedTasks.Count - 1 && task.DelaySeconds > 0)
                {
                    _logger.Debug("Waiting {Delay} seconds before next launch...", task.DelaySeconds);

                    OnProgressUpdated(new LaunchProgressEventArgs
                    {
                        LaunchedCount = LaunchedCount,
                        TotalClients = TotalClients,
                        StatusMessage = $"Waiting {task.DelaySeconds} seconds..."
                    });

                    await Task.Delay(TimeSpan.FromSeconds(task.DelaySeconds), cancellationToken);
                }
            }

            // Calculate duration and set success flag
            result.TotalDuration = DateTime.UtcNow - startTime;
            result.SuccessCount = SuccessCount;
            result.FailureCount = FailureCount;
            result.Success = FailureCount == 0 && !result.WasCancelled;

            _logger.Information("Launch sequence completed: {Summary}, duration: {Duration:F1}s",
                result.Summary, result.TotalDuration.TotalSeconds);

            // Fire SequenceCompleted event
            OnSequenceCompleted(new SequenceCompletedEventArgs
            {
                TotalTasks = TotalClients,
                SuccessCount = SuccessCount,
                FailureCount = FailureCount,
                WasCancelled = result.WasCancelled,
                TotalDuration = result.TotalDuration
            });

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Launch sequence was cancelled");
            result.WasCancelled = true;
            result.Success = false;
            result.TotalDuration = DateTime.UtcNow - startTime;
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during launch sequence");
            result.Success = false;
            result.Errors.Add($"Sequence error: {ex.Message}");
            result.TotalDuration = DateTime.UtcNow - startTime;
            return result;
        }
        finally
        {
            IsLaunching = false;
        }
    }

    /// <summary>
    /// Raises the LaunchStarted event.
    /// </summary>
    protected virtual void OnLaunchStarted(LaunchStartedEventArgs e)
    {
        LaunchStarted?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the LaunchCompleted event.
    /// </summary>
    protected virtual void OnLaunchCompleted(LaunchCompletedEventArgs e)
    {
        LaunchCompleted?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the SequenceCompleted event.
    /// </summary>
    protected virtual void OnSequenceCompleted(SequenceCompletedEventArgs e)
    {
        SequenceCompleted?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the ProgressUpdated event.
    /// </summary>
    protected virtual void OnProgressUpdated(LaunchProgressEventArgs e)
    {
        ProgressUpdated?.Invoke(this, e);
    }
}
