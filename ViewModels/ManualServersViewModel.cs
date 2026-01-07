using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Models;
using OPLauncher.Services;
using OPLauncher.DTOs;

namespace OPLauncher.ViewModels;

/// <summary>
/// ViewModel for the manual servers management screen.
/// Allows users to add, edit, and delete localhost/private servers.
/// </summary>
public partial class ManualServersViewModel : ViewModelBase, IDisposable
{
    private readonly ManualServersService _manualServersService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly ConfigService _configService;
    private readonly INavigationService _navigationService;
    private readonly FavoritesService _favoritesService;
    private readonly LoggingService _logger;
    private readonly MainWindowViewModel _mainWindow;
    private System.Threading.Timer? _statusCheckTimer;
    private bool _disposed;

    /// <summary>
    /// Collection of all manual servers.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ManualServer> _servers = new();

    /// <summary>
    /// Collection of server cards for display in card grid.
    /// Wraps ManualServer objects in ServerCardViewModel.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServerCardViewModel> _serverCards = new();

    /// <summary>
    /// The currently selected server.
    /// </summary>
    [ObservableProperty]
    private ManualServer? _selectedServer;

    /// <summary>
    /// Whether the add/edit form is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isFormVisible;

    /// <summary>
    /// Whether we're editing an existing server (vs adding new).
    /// </summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>
    /// The server being edited/created in the form.
    /// </summary>
    [ObservableProperty]
    private ManualServer _editingServer = new();

    /// <summary>
    /// Whether servers are currently being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Error message to display.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Success message to display.
    /// </summary>
    [ObservableProperty]
    private string? _successMessage;

    /// <summary>
    /// Gets the available server types for the dropdown.
    /// </summary>
    public ServerType[] ServerTypes { get; } = Enum.GetValues<ServerType>();

    /// <summary>
    /// Initializes a new instance of the ManualServersViewModel.
    /// </summary>
    public ManualServersViewModel(
        ManualServersService manualServersService,
        GameLaunchService gameLaunchService,
        CredentialVaultService credentialVaultService,
        ConfigService configService,
        INavigationService navigationService,
        FavoritesService favoritesService,
        LoggingService logger,
        MainWindowViewModel mainWindow)
    {
        _manualServersService = manualServersService;
        _gameLaunchService = gameLaunchService;
        _credentialVaultService = credentialVaultService;
        _configService = configService;
        _navigationService = navigationService;
        _favoritesService = favoritesService;
        _logger = logger;
        _mainWindow = mainWindow;

        _logger.Debug("ManualServersViewModel initialized");

        // Load servers
        _ = LoadServersAsync();

        // Start periodic status checking (every 30 seconds)
        _statusCheckTimer = new System.Threading.Timer(
            _ =>
            {
                // Run async method in background task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await CheckAllServerStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error in periodic server status check");
                    }
                });
            },
            null,
            TimeSpan.FromSeconds(5),  // Initial delay
            TimeSpan.FromSeconds(30)); // Check every 30 seconds
    }

    /// <summary>
    /// Loads all manual servers from the database.
    /// </summary>
    [RelayCommand]
    private async Task LoadServersAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            _logger.Debug("Loading manual servers");
            var servers = await _manualServersService.GetAllServersAsync();

            Servers.Clear();
            foreach (var server in servers)
            {
                Servers.Add(server);
            }

            // Rebuild server cards for card grid display
            RebuildServerCards();

            _logger.Information("Loaded {Count} manual servers", servers.Count);

            // Check server statuses immediately after loading
            _ = CheckAllServerStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading manual servers");
            ErrorMessage = "Failed to load servers. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Checks the online status of all manual servers via UDP ping.
    /// Uses ThwargLauncher's efficient approach:
    /// - Parallel checking (Task.WhenAll)
    /// - Adaptive intervals (5min for online, 15sec for offline)
    /// - Per-server check time tracking
    /// </summary>
    private async Task CheckAllServerStatusAsync()
    {
        if (Servers.Count == 0)
            return;

        _logger.Debug("Checking status for {Count} manual servers", Servers.Count);

        // Check all servers in parallel (ThwargLauncher approach)
        var checkTasks = Servers.ToList().Select(async server =>
        {
            try
            {
                // Adaptive polling: skip if checked too recently
                var elapsedSinceCheck = DateTime.UtcNow - (server.LastStatusCheck ?? DateTime.MinValue);
                var requiredInterval = server.IsOnline
                    ? TimeSpan.FromSeconds(server.StatusOnlineIntervalSeconds)  // 5 min for online servers
                    : TimeSpan.FromSeconds(server.StatusOfflineIntervalSeconds); // 15 sec for offline servers

                if (elapsedSinceCheck < requiredInterval)
                {
                    _logger.Debug("Skipping check for {Server} - last checked {Elapsed}s ago (required: {Required}s)",
                        server.Name, (int)elapsedSinceCheck.TotalSeconds, (int)requiredInterval.TotalSeconds);
                    return;
                }

                // Perform UDP ping with player count extraction
                var (isOnline, playerCount) = await IsServerOnlineWithPlayerCountAsync(server.Host, server.Port);

                // Update on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var wasOnline = server.IsOnline;
                    server.IsOnline = isOnline;
                    server.LastStatusCheck = DateTime.UtcNow;
                    server.PlayerCount = playerCount;

                    // Log status changes
                    if (wasOnline != isOnline)
                    {
                        _logger.Information("Server {Server} status changed: {OldStatus} → {NewStatus}",
                            server.Name,
                            wasOnline ? "ONLINE" : "OFFLINE",
                            isOnline ? "ONLINE" : "OFFLINE");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error checking server status for {Host}:{Port}", server.Host, server.Port);
            }
        }).ToArray();

        await Task.WhenAll(checkTasks);
    }

    /// <summary>
    /// Checks if a server is online via UDP ping (AC protocol) and extracts player count.
    /// Returns tuple of (isOnline, playerCount) where playerCount is -1 if unavailable.
    /// </summary>
    private async Task<(bool isOnline, int playerCount)> IsServerOnlineWithPlayerCountAsync(string host, int port)
    {
        try
        {
            using var udpClient = new System.Net.Sockets.UdpClient();
            udpClient.Connect(host, port);

            // Send AC protocol login packet
            byte[] sendBytes = Utilities.Packet.MakeLoginPacket();
            await udpClient.SendAsync(sendBytes, sendBytes.Length);

            // Wait for response with 3-second timeout
            var receiveTask = udpClient.ReceiveAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));

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

                    // Try to extract player count if response is long enough and has ConnectResponse flag
                    if (result.Buffer.Length >= 24)
                    {
                        var header = Utilities.Packet.ByteArrayToPacketHeader(result.Buffer);
                        if ((header.Flags & Utilities.Packet.PacketHeaderFlags.ConnectResponse) != 0)
                        {
                            // Server is online, try to extract player count
                            if (Utilities.Packet.TryExtractPlayerCount(result.Buffer, out int playerCount))
                            {
                                _logger.Debug("Server {Host}:{Port} is online with {PlayerCount} players", host, port, playerCount);
                                return (true, playerCount);
                            }
                        }
                    }

                    // Server is online, but player count not available
                    _logger.Debug("Server {Host}:{Port} is online (flag: 0x{Flag:X8})", host, port, responseFlag);
                    return (true, -1);
                }
            }

            return (false, -1); // Offline
        }
        catch
        {
            return (false, -1);
        }
    }

    /// <summary>
    /// Shows the form to add a new server.
    /// </summary>
    [RelayCommand]
    private void ShowAddForm()
    {
        _logger.Debug("User showing add server form");
        EditingServer = new ManualServer
        {
            Name = "",
            Host = "127.0.0.1",
            Port = 9000,
            ServerType = ServerType.ACE
        };
        IsEditing = false;
        IsFormVisible = true;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    /// <summary>
    /// Shows the form to edit an existing server.
    /// </summary>
    [RelayCommand]
    private void ShowEditForm(ManualServer? server)
    {
        if (server == null)
        {
            _logger.Warning("ShowEditForm called with null server");
            return;
        }

        _logger.Debug("User editing manual server: {Name}", server.Name);
        EditingServer = server.Clone();
        IsEditing = true;
        IsFormVisible = true;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    /// <summary>
    /// Cancels the add/edit operation and hides the form.
    /// </summary>
    [RelayCommand]
    private void CancelForm()
    {
        _logger.Debug("User cancelled add/edit form");
        IsFormVisible = false;
        EditingServer = new ManualServer();
        ErrorMessage = null;
        SuccessMessage = null;
    }

    /// <summary>
    /// Saves the server (add or update).
    /// </summary>
    [RelayCommand]
    private async Task SaveServerAsync()
    {
        try
        {
            ErrorMessage = null;
            SuccessMessage = null;

            // Validate
            var validationErrors = EditingServer.Validate();
            if (validationErrors.Count > 0)
            {
                ErrorMessage = string.Join("\n", validationErrors);
                _logger.Warning("Server validation failed: {Errors}", ErrorMessage);
                return;
            }

            if (IsEditing)
            {
                // Update existing server
                var success = await _manualServersService.UpdateServerAsync(EditingServer);
                if (success)
                {
                    SuccessMessage = $"Server '{EditingServer.Name}' updated successfully.";
                    _logger.Information("Updated manual server: {Name}", EditingServer.Name);

                    // Update in the list
                    var existingServer = Servers.FirstOrDefault(s => s.Id == EditingServer.Id);
                    if (existingServer != null)
                    {
                        var index = Servers.IndexOf(existingServer);
                        Servers[index] = EditingServer.Clone();

                        // Update ServerCards collection for immediate UI update
                        var existingCard = ServerCards.FirstOrDefault(c => c.ManualServer?.Id == EditingServer.Id);
                        if (existingCard != null)
                        {
                            var cardIndex = ServerCards.IndexOf(existingCard);
                            var updatedCard = new ServerCardViewModel(Servers[index], _navigationService, _favoritesService, _logger);
                            updatedCard.DeleteRequested += OnServerDeleteRequested;
                            ServerCards[cardIndex] = updatedCard;
                        }
                    }

                    IsFormVisible = false;
                }
                else
                {
                    ErrorMessage = "Failed to update server. Check for duplicate name or connection.";
                }
            }
            else
            {
                // Add new server
                var addedServer = await _manualServersService.AddServerAsync(EditingServer);
                if (addedServer != null)
                {
                    SuccessMessage = $"Server '{addedServer.Name}' added successfully.";
                    _logger.Information("Added new manual server: {Name}", addedServer.Name);

                    Servers.Insert(0, addedServer); // Add to top of list

                    // Add to ServerCards collection for immediate UI update
                    var newCard = new ServerCardViewModel(addedServer, _navigationService, _favoritesService, _logger);
                    newCard.DeleteRequested += OnServerDeleteRequested;
                    ServerCards.Insert(0, newCard); // Add to top of card list

                    // Check the new server's status immediately
                    _ = Task.Run(async () =>
                    {
                        var (isOnline, playerCount) = await IsServerOnlineWithPlayerCountAsync(addedServer.Host, addedServer.Port);
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            addedServer.IsOnline = isOnline;
                            addedServer.LastStatusCheck = DateTime.UtcNow;
                            addedServer.PlayerCount = playerCount;
                        });
                    });

                    IsFormVisible = false;
                }
                else
                {
                    ErrorMessage = "Failed to add server. Check for duplicate name or connection.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving manual server");
            ErrorMessage = $"An error occurred: {ex.Message}";
        }
    }

    /// <summary>
    /// Handles the DeleteRequested event from ServerCardViewModel.
    /// </summary>
    private void OnServerDeleteRequested(object? sender, ServerDeleteRequestedEventArgs e)
    {
        _logger.Debug("Delete requested for server: {ServerName}", e.Server.Name);
        _ = DeleteServerAsync(e.Server);
    }

    /// <summary>
    /// Deletes a manual server after confirmation.
    /// </summary>
    [RelayCommand]
    private async Task DeleteServerAsync(ManualServer? server)
    {
        if (server == null)
        {
            _logger.Warning("DeleteServer called with null server");
            return;
        }

        try
        {
            _logger.Information("User deleting manual server: {Name}", server.Name);

            var success = await _manualServersService.DeleteServerAsync(server.Id);
            if (success)
            {
                // Remove from Servers collection
                Servers.Remove(server);

                // Remove from ServerCards collection for immediate UI update
                var cardToRemove = ServerCards.FirstOrDefault(c => c.ManualServer?.Id == server.Id);
                if (cardToRemove != null)
                {
                    ServerCards.Remove(cardToRemove);
                    _logger.Debug("Removed server card for deleted server: {Name}", server.Name);
                }

                SuccessMessage = $"Server '{server.Name}' deleted successfully.";
                _logger.Information("Deleted manual server: {Name}", server.Name);
            }
            else
            {
                ErrorMessage = "Failed to delete server.";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting manual server");
            ErrorMessage = $"Error deleting server: {ex.Message}";
        }
    }

    /// <summary>
    /// Shows the detail view for the specified manual server.
    /// </summary>
    /// <param name="server">The server to show details for.</param>
    [RelayCommand]
    private void ShowServerDetails(ManualServer? server)
    {
        if (server == null)
        {
            _logger.Warning("ShowServerDetails called with null server");
            ErrorMessage = "Invalid server selection";
            return;
        }

        _logger.Information("User viewing details for manual server: {ServerName} (ID: {ServerId})", server.Name, server.Id);

        // Navigate to ManualServerDetailView using NavigationService
        _navigationService.NavigateTo<ManualServerDetailViewModel>(server);
    }

    /// <summary>
    /// Launches the game for a manual server.
    /// </summary>
    [RelayCommand]
    private async Task PlayServerAsync(ManualServer? server)
    {
        if (server == null)
        {
            _logger.Warning("PlayServer called with null server");
            return;
        }

        try
        {
            ErrorMessage = null;
            SuccessMessage = null;

            _logger.Information("User launching game for manual server: {Name}", server.Name);

            // Check AC client configuration
            var (isValid, validationError) = _gameLaunchService.ValidateLaunchConfiguration();
            if (!isValid)
            {
                ErrorMessage = validationError ?? "AC client is not configured. Please check settings.";
                return;
            }

            // Get saved credentials for this server (if any)
            // Manual servers use negative world IDs to avoid conflicts with OldPortal.com servers
            var worldId = -server.Id;
            var credentials = await _credentialVaultService.GetCredentialsForWorldAsync(worldId);
            var credential = credentials.FirstOrDefault();

            // Convert manual server to connection info
            var connectionInfo = server.ToConnectionInfo();

            // Launch the game
            var launchResult = await _gameLaunchService.LaunchGameAsync(connectionInfo, credential);

            if (launchResult.Success)
            {
                SuccessMessage = $"Game launched successfully for '{server.Name}'!";
                _logger.Information("Game launched successfully for manual server: {Name}", server.Name);

                // Update last connected time
                await _manualServersService.UpdateLastConnectedAsync(server.Id);

                // Reload servers to show updated timestamp
                await LoadServersAsync();
            }
            else
            {
                ErrorMessage = launchResult.ErrorMessage ?? "Failed to launch game.";
                _logger.Error("Failed to launch game for manual server: {Error}", ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error launching game for manual server");
            ErrorMessage = $"An error occurred: {ex.Message}";
        }
    }

    /// <summary>
    /// Navigates back to the worlds browse view.
    /// </summary>
    [RelayCommand]
    private void Back()
    {
        _logger.Debug("User navigating back from manual servers view");
        _mainWindow.NavigateToWorldsCommand.Execute(null);
    }

    /// <summary>
    /// Rebuilds the ServerCards collection from the current Servers collection.
    /// Wraps each ManualServer in a ServerCardViewModel for display in the card grid.
    /// </summary>
    private void RebuildServerCards()
    {
        ServerCards.Clear();
        foreach (var server in Servers)
        {
            var card = new ServerCardViewModel(server, _navigationService, _favoritesService, _logger);
            card.DeleteRequested += OnServerDeleteRequested;
            ServerCards.Add(card);
        }
        _logger.Debug("Rebuilt {Count} server cards for manual servers", ServerCards.Count);
    }

    /// <summary>
    /// Disposes resources used by the ManualServersViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Stop the status check timer
        _statusCheckTimer?.Dispose();
        _statusCheckTimer = null;

        _disposed = true;
        _logger.Debug("ManualServersViewModel disposed");
    }
}
