using Avalonia;
using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Diagnostics;
using OPLauncher.Models;
using OPLauncher.Utilities;

namespace OPLauncher;

sealed class Program
{
    // Stores deep link information to be accessed by the App
    public static DeepLinkInfo? PendingDeepLink { get; private set; }

    // Single instance manager for enforcing one running instance
    public static SingleInstanceManager? SingleInstanceManager { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure Serilog FIRST so we can log any errors
        ConfigureLogging();

        try
        {
            Log.Information("========================================");
            Log.Information("OldPortal Launcher starting up");
            Log.Information("Version: {LauncherVersion}", GetLauncherVersion());
            Log.Information("OS: {OS}", Environment.OSVersion);
            Log.Information("Working Directory: {WorkingDir}", Environment.CurrentDirectory);
            Log.Information("Command Line Args: {Args}", string.Join(" ", args));

            // Register deep link protocol on first run if needed
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                {
                    RegisterDeepLinkProtocol(exePath);
                    Log.Debug("Deep link protocol registered");
                }
            }
            catch (Exception dex)
            {
                Log.Warning(dex, "Failed to register deep link protocol (may require admin rights)");
            }

            // Check for deep link in command line arguments
            string? deepLinkUri = null;
            if (DeepLinkParser.TryFindDeepLinkInArgs(args, out Guid serverId))
            {
                deepLinkUri = Array.Find(args, arg => DeepLinkParser.IsDeepLink(arg)) ?? "";
                PendingDeepLink = new DeepLinkInfo(serverId, deepLinkUri);
                Log.Information("Deep link detected: {Uri} -> Server ID: {ServerId}", deepLinkUri, serverId);
            }

            // Check if another instance is already running
            SingleInstanceManager = new SingleInstanceManager();
            if (!SingleInstanceManager.IsFirstInstance)
            {
                Log.Information("Another instance is already running. Sending deep link and exiting.");

                // Send deep link to existing instance if present
                if (!string.IsNullOrEmpty(deepLinkUri))
                {
                    var sendTask = SingleInstanceManager.SendDeepLinkToExistingInstanceAsync(deepLinkUri);
                    sendTask.Wait(TimeSpan.FromSeconds(5));

                    if (sendTask.Result)
                    {
                        Log.Information("Deep link sent to existing instance successfully");
                    }
                    else
                    {
                        Log.Warning("Failed to send deep link to existing instance");
                    }
                }

                SingleInstanceManager.Dispose();
                return; // Exit - let the existing instance handle it
            }

            Log.Information("This is the first instance - starting normally");
            Log.Information("========================================");

            // Start listening for deep links from other instances
            SingleInstanceManager.StartListening();

            // Start Avalonia app
            Log.Debug("Starting Avalonia UI...");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);

            Log.Information("OldPortal Launcher shutting down gracefully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "OldPortal Launcher terminated unexpectedly");
            Log.Fatal("Log file location: {LogPath}", GetLogFilePath());

            // Write error to a simple text file in case logs aren't accessible
            try
            {
                var errorFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "OPLauncher_Error.txt");

                File.WriteAllText(errorFile,
                    $"OldPortal Launcher Error\n" +
                    $"======================\n\n" +
                    $"Time: {DateTime.Now}\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"Stack Trace:\n{ex.StackTrace}\n\n" +
                    $"Log file: {GetLogFilePath()}\n");

                Log.Information("Error details written to desktop: {ErrorFile}", errorFile);
            }
            catch (Exception writeEx)
            {
                Log.Error(writeEx, "Failed to write error file to desktop");
            }

            throw;
        }
        finally
        {
            SingleInstanceManager?.Dispose();
            Log.Information("========================================");
            Log.Information("OldPortal Launcher shutdown complete");
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void ConfigureLogging()
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        // Get log file path from config or use default
        var logPath = configuration["Logging:LogFilePath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                           "OldPortal", "launcher", "logs", "launcher-.log");

        // Expand environment variables in path
        logPath = Environment.ExpandEnvironmentVariables(logPath);

        // Ensure log directory exists
        var logDirectory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Get minimum log level from config
        var minimumLevel = configuration["Logging:MinimumLevel"];
        var logLevel = Enum.TryParse<LogEventLevel>(minimumLevel, out var level)
            ? level
            : LogEventLevel.Information;

        // Get retained file count
        var retainedFileCount = int.TryParse(configuration["Logging:RetainedFileCountLimit"], out var count)
            ? count
            : 30;

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "OldPortalLauncher")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedFileCount,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static string GetLauncherVersion()
    {
        try
        {
            // Read version from assembly (single source of truth from .csproj)
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }

            return "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    private static string GetLogFilePath()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var logPath = configuration["Logging:LogFilePath"]
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                               "OldPortal", "launcher", "logs", "launcher-.log");

            return Environment.ExpandEnvironmentVariables(logPath);
        }
        catch
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                               "OldPortal", "launcher", "logs", "launcher-.log");
        }
    }

    /// <summary>
    /// Called when app is first installed/run.
    /// InnoSetup handles shortcuts automatically, we just need to register the deep link protocol.
    /// </summary>
    private static void OnFirstRun()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            RegisterDeepLinkProtocol(exePath);
        }
        catch
        {
            // Silently fail - not critical for app operation
        }
    }

    /// <summary>
    /// Registers the oldportal:// protocol in Windows Registry.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void RegisterDeepLinkProtocol(string exePath)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\oldportal");
            key?.SetValue("", "URL:OldPortal Protocol");
            key?.SetValue("URL Protocol", "");

            using var iconKey = key?.CreateSubKey("DefaultIcon");
            iconKey?.SetValue("", $"\"{exePath}\",0");

            using var commandKey = key?.CreateSubKey(@"shell\open\command");
            commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        catch
        {
            // Silently fail - registry access might be denied
        }
    }

    /// <summary>
    /// Unregisters the oldportal:// protocol from Windows Registry.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void UnregisterDeepLinkProtocol()
    {
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\oldportal", false);
        }
        catch
        {
            // Silently fail - key might not exist or access denied
        }
    }
}
