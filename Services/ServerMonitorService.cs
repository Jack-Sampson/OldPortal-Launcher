// Component: OPLauncher
// Module: Server Monitoring
// Description: Real-time UDP-based server status checking service
// Based on ThwargLauncher ServerMonitor implementation

using System.Net;
using System.Net.Sockets;
using OPLauncher.DTOs;
using OPLauncher.Utilities;

namespace OPLauncher.Services;

/// <summary>
/// Service for monitoring AC server status using UDP packet pings
/// Provides real-time server online/offline detection independent of API
/// </summary>
public class ServerMonitorService : IDisposable
{
    private readonly LoggingService _logger;
    private readonly Timer? _monitorTimer;
    private readonly Dictionary<Guid, ServerCheckStatus> _serverStatuses = new();
    private readonly object _lock = new();
    private bool _isRunning;
    private bool _disposed;

    private const int DefaultCheckIntervalSeconds = 15;
    private const int UdpTimeoutSeconds = 3;

    /// <summary>
    /// Event fired when a server's status changes
    /// </summary>
    public event EventHandler<ServerStatusChangedEventArgs>? ServerStatusChanged;

    /// <summary>
    /// Tracks last check time and status for a server
    /// </summary>
    private class ServerCheckStatus
    {
        public DateTime LastCheckedUtc { get; set; } = DateTime.MinValue;
        public bool LastKnownOnlineStatus { get; set; }
        public int OnlineIntervalSeconds { get; set; } = 60;    // Check online servers every 60s
        public int OfflineIntervalSeconds { get; set; } = 15;   // Check offline servers every 15s
    }

    /// <summary>
    /// Initializes the server monitor service
    /// </summary>
    /// <param name="logger">Logging service</param>
    public ServerMonitorService(LoggingService logger)
    {
        _logger = logger;
        _monitorTimer = new Timer(
            MonitorCallback,
            null,
            Timeout.Infinite,
            Timeout.Infinite);

        _logger.Debug("ServerMonitorService initialized");
    }

    /// <summary>
    /// Starts monitoring servers
    /// </summary>
    /// <param name="checkIntervalSeconds">Interval between monitoring cycles (default: 15s)</param>
    public void Start(int checkIntervalSeconds = DefaultCheckIntervalSeconds)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                _logger.Warning("ServerMonitorService already running");
                return;
            }

            _logger.Information("Starting server monitoring (interval: {Interval}s)", checkIntervalSeconds);
            _isRunning = true;

            // Start timer
            _monitorTimer?.Change(
                TimeSpan.Zero,  // Start immediately
                TimeSpan.FromSeconds(checkIntervalSeconds));
        }
    }

    /// <summary>
    /// Stops monitoring servers
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
            {
                return;
            }

            _logger.Information("Stopping server monitoring");
            _isRunning = false;

            // Stop timer
            _monitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            // Clear status cache
            _serverStatuses.Clear();
        }
    }

    /// <summary>
    /// Timer callback for periodic server checking.
    /// NOTE: This service is designed for manual checking via CheckServerAsync().
    /// The timer is maintained for future automatic background checking feature.
    /// </summary>
    private void MonitorCallback(object? state)
    {
        // Prevent re-entry
        if (!_isRunning)
            return;

        // Services call CheckServerAsync() manually when needed
        _logger.Debug("Server monitor cycle (manual checking mode)");
    }

    /// <summary>
    /// Checks the status of a specific server using UDP ping
    /// </summary>
    /// <param name="world">World to check</param>
    /// <returns>Task that completes with true if server is online</returns>
    public async Task<bool> CheckServerAsync(WorldDto world)
    {
        try
        {
            // Get or create status tracking
            ServerCheckStatus status;
            lock (_lock)
            {
                if (!_serverStatuses.TryGetValue(world.ServerId, out var existingStatus))
                {
                    existingStatus = new ServerCheckStatus();
                    _serverStatuses[world.ServerId] = existingStatus;
                }
                status = existingStatus;

                // Check if we should skip this check (too soon since last check)
                var elapsedSinceLastCheck = DateTime.UtcNow - status.LastCheckedUtc;
                var requiredInterval = status.LastKnownOnlineStatus
                    ? TimeSpan.FromSeconds(status.OnlineIntervalSeconds)
                    : TimeSpan.FromSeconds(status.OfflineIntervalSeconds);

                if (elapsedSinceLastCheck < requiredInterval)
                {
                    _logger.Debug("Skipping check for {Server} - too soon (last check: {Elapsed}s ago)",
                        world.Name, (int)elapsedSinceLastCheck.TotalSeconds);
                    return status.LastKnownOnlineStatus;
                }

                status.LastCheckedUtc = DateTime.UtcNow;
            }

            // Validate host and port
            if (string.IsNullOrWhiteSpace(world.Host) || world.Port <= 0 || world.Port > 65535)
            {
                _logger.Debug("Invalid host/port for {Server}: {Host}:{Port}",
                    world.Name, world.Host, world.Port);
                return false;
            }

            // Perform UDP ping
            bool isOnline = await IsUdpServerOnlineAsync(world.Host, world.Port);

            // Update status and fire event if changed
            lock (_lock)
            {
                if (status.LastKnownOnlineStatus != isOnline)
                {
                    _logger.Information("Server {Server} status changed: {OldStatus} → {NewStatus}",
                        world.Name,
                        status.LastKnownOnlineStatus ? "ONLINE" : "OFFLINE",
                        isOnline ? "ONLINE" : "OFFLINE");

                    status.LastKnownOnlineStatus = isOnline;

                    // Fire event
                    ServerStatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
                    {
                        ServerId = world.ServerId,
                        ServerName = world.Name,
                        IsOnline = isOnline,
                        CheckedAtUtc = DateTime.UtcNow
                    });
                }
            }

            return isOnline;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking server {Server}", world.Name);
            return false;
        }
    }

    /// <summary>
    /// Checks the status of a manual server using UDP ping (simple version without caching)
    /// </summary>
    /// <param name="host">Server hostname or IP</param>
    /// <param name="port">Server port</param>
    /// <returns>Task that completes with true if server is online</returns>
    public async Task<bool> CheckManualServerAsync(string host, int port)
    {
        try
        {
            // Validate host and port
            if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
            {
                _logger.Debug("Invalid host/port for manual server: {Host}:{Port}", host, port);
                return false;
            }

            // Perform UDP ping
            bool isOnline = await IsUdpServerOnlineAsync(host, port);

            _logger.Debug("Manual server {Host}:{Port} status: {Status}",
                host, port, isOnline ? "ONLINE" : "OFFLINE");

            return isOnline;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking manual server {Host}:{Port}", host, port);
            return false;
        }
    }

    /// <summary>
    /// Performs a UDP ping to check if an AC server is responding
    /// </summary>
    /// <param name="host">Server hostname or IP</param>
    /// <param name="port">Server port</param>
    /// <returns>True if server responded within timeout</returns>
    private async Task<bool> IsUdpServerOnlineAsync(string host, int port)
    {
        using var udpClient = new UdpClient();
        try
        {
            // Connect to server
            udpClient.Connect(host, port);

            // Create and send login packet
            byte[] sendBytes = Packet.MakeLoginPacket();
            await udpClient.SendAsync(sendBytes, sendBytes.Length);

            // Wait for response with timeout
            var receiveTask = udpClient.ReceiveAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(UdpTimeoutSeconds));

            var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

            if (completedTask == receiveTask)
            {
                var result = await receiveTask;

                // Validate response packet
                // Note: Different AC server implementations may return different response flags
                // The important thing is that the server responded at all - this indicates it's online
                // Common flags: 0x00080000 (ConnectResponse), 0xFFFFFFFF (error response, but server is still online)
                if (result.Buffer.Length >= 4)
                {
                    // Any valid response (at least 4 bytes) indicates server is online
                    uint responseFlag = BitConverter.ToUInt32(result.Buffer, 0);
                    _logger.Debug("Server {Host}:{Port} is ONLINE (flag: 0x{Flag:X8})", host, port, responseFlag);
                    return true;
                }

                _logger.Debug("Server {Host}:{Port} sent invalid response (too short: {Length} bytes)", host, port, result.Buffer.Length);
                return false;
            }
            else
            {
                // Timeout
                _logger.Debug("Server {Host}:{Port} ping timeout after {Timeout}s", host, port, UdpTimeoutSeconds);
                return false;
            }
        }
        catch (SocketException ex)
        {
            _logger.Debug("Socket error checking {Host}:{Port}: {Error}", host, port, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Unexpected error pinging {Host}:{Port}", host, port);
            return false;
        }
    }

    /// <summary>
    /// Checks multiple servers in parallel
    /// </summary>
    /// <param name="worlds">Worlds to check</param>
    /// <returns>Task that completes when all checks are done</returns>
    public async Task CheckServersAsync(IEnumerable<WorldDto> worlds)
    {
        var tasks = worlds.Select(w => CheckServerAsync(w));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Gets the last known status for a server
    /// </summary>
    /// <param name="serverId">Server ID</param>
    /// <returns>Last known online status, or null if never checked</returns>
    public bool? GetLastKnownStatus(Guid serverId)
    {
        lock (_lock)
        {
            if (_serverStatuses.TryGetValue(serverId, out var status))
            {
                return status.LastKnownOnlineStatus;
            }
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _monitorTimer?.Dispose();
        _disposed = true;

        _logger.Debug("ServerMonitorService disposed");
    }
}

/// <summary>
/// Event args for server status changes
/// </summary>
public class ServerStatusChangedEventArgs : EventArgs
{
    public Guid ServerId { get; init; }
    public string ServerName { get; init; } = string.Empty;
    public bool IsOnline { get; init; }
    public DateTime CheckedAtUtc { get; init; }
}
