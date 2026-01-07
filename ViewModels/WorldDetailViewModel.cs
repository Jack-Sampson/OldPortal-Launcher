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
/// ViewModel for the world detail screen.
/// Displays detailed information about a specific world and allows game launching.
///
/// INLINE CREDENTIAL MANAGEMENT PATTERN:
/// This ViewModel manages AC account credentials directly within the world detail view instead of
/// using separate dialogs or a centralized credential picker. This design decision provides:
///
/// 1. CONTEXTUAL CREDENTIAL MANAGEMENT: Users see and manage credentials in the context of the
///    specific world they're viewing. Each world has its own credential list, making it clear
///    which characters belong to which server.
///
/// 2. SIMPLIFIED USER FLOW: Add Credential -> Enter Details -> Save -> Launch Game all happens
///    in one view without modal dialogs or navigation. The "Add Credential" button expands an
///    inline form (ShowAddCredential property) rather than opening a separate window.
///
/// 3. CHARACTER-CENTRIC MODEL: Players think in terms of "My characters on this server" rather than
///    "My global account list". Multiple characters per server are displayed as a list with friendly
///    display names (e.g., "Tank Main", "Quest Alt") instead of just usernames.
///
/// 4. DIRECT LAUNCH WORKFLOW: Each saved credential has its own "Play" button, allowing instant
///    launch without additional selection steps. Last-used timestamps help players find their
///    most recent characters.
///
/// CREDENTIAL STORAGE:
/// - Credentials are stored per-world in CredentialVaultService using LiteDB
/// - Passwords are encrypted using DPAPI (Windows Data Protection API) with CurrentUser scope
/// - Display names are optional friendly labels (e.g., "My Level 275 Mage")
/// - LastUsed timestamp tracks which character was played most recently
///
/// UI LAYOUT (WorldDetailView.axaml):
/// - Expandable "Add Credential" form controlled by ShowAddCredential property
/// - List of saved credentials with Edit/Delete/Play buttons per credential
/// - Empty state message when no credentials exist
/// - Form validation with inline error messages
///
/// See also: ManualServerDetailViewModel (uses identical pattern for manual servers)
/// </summary>
public partial class WorldDetailViewModel : ViewModelBase, IDisposable
{
    private readonly WorldsService _worldsService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly MultiLaunchConfigService _multiLaunchConfigService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly LaunchSequencerService _launchSequencerService;
    private readonly BatchGroupService _batchGroupService;
    private readonly ConfigService _configService;
    private readonly INavigationService _navigationService;
    private readonly LoggingService _logger;
    private readonly MainWindowViewModel _mainWindow;
    private bool _disposed;

    /// <summary>
    /// The world being displayed.
    /// </summary>
    [ObservableProperty]
    private WorldDto _world;

    /// <summary>
    /// The credential form component that handles all credential management.
    /// </summary>
    public CredentialFormViewModel? CredentialForm { get; private set; }

    /// <summary>
    /// Collection of saved credentials for this world (forwarded from CredentialForm).
    /// </summary>
    public ObservableCollection<SavedCredential> SavedCredentials => CredentialForm?.SavedCredentials ?? new ObservableCollection<SavedCredential>();

    /// <summary>
    /// The selected credential to use for launching (forwarded from CredentialForm).
    /// </summary>
    public SavedCredential? SelectedCredential
    {
        get => CredentialForm?.SelectedCredential;
        set { if (CredentialForm != null) CredentialForm.SelectedCredential = value; }
    }

    /// <summary>
    /// Whether to show the add/edit credential form (forwarded from CredentialForm).
    /// </summary>
    public bool ShowAddCredential
    {
        get => CredentialForm?.ShowAddCredential ?? false;
        set { if (CredentialForm != null) CredentialForm.ShowAddCredential = value; }
    }

    /// <summary>
    /// Whether a game launch is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isLaunching;

    /// <summary>
    /// Whether credentials are being loaded (forwarded from CredentialForm).
    /// </summary>
    public bool IsLoadingCredentials => CredentialForm?.IsLoadingCredentials ?? false;

    /// <summary>
    /// Whether a credential is being saved (forwarded from CredentialForm).
    /// </summary>
    public bool IsSavingCredential => CredentialForm?.IsSavingCredential ?? false;

    /// <summary>
    /// Username for the new credential being added (forwarded from CredentialForm).
    /// </summary>
    public string NewUsername
    {
        get => CredentialForm?.NewUsername ?? string.Empty;
        set { if (CredentialForm != null) CredentialForm.NewUsername = value; }
    }

    /// <summary>
    /// Password for the new credential being added (forwarded from CredentialForm).
    /// </summary>
    public string NewPassword
    {
        get => CredentialForm?.NewPassword ?? string.Empty;
        set { if (CredentialForm != null) CredentialForm.NewPassword = value; }
    }

    /// <summary>
    /// Display name for the new credential being added (forwarded from CredentialForm).
    /// </summary>
    public string? NewDisplayName
    {
        get => CredentialForm?.NewDisplayName;
        set { if (CredentialForm != null) CredentialForm.NewDisplayName = value; }
    }

    /// <summary>
    /// Whether the password should be visible or hidden (forwarded from CredentialForm).
    /// </summary>
    public bool IsPasswordVisible
    {
        get => CredentialForm?.IsPasswordVisible ?? false;
        set { if (CredentialForm != null) CredentialForm.IsPasswordVisible = value; }
    }

    /// <summary>
    /// Whether we're editing an existing credential (forwarded from CredentialForm).
    /// </summary>
    public bool IsEditingMode => CredentialForm?.IsEditingMode ?? false;

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
    /// Status message for launch progress.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Whether connection information is available for this world.
    /// Used to determine if the Play button should be enabled in offline mode.
    /// </summary>
    [ObservableProperty]
    private bool _hasConnectionInfo;

    /// <summary>
    /// The connection information for this world (host, port, server type).
    /// </summary>
    [ObservableProperty]
    private WorldConnectionDto? _connectionInfo;

    /// <summary>
    /// Whether connection info is currently being checked.
    /// </summary>
    [ObservableProperty]
    private bool _isCheckingConnection;

    /// <summary>
    /// Whether to show the multi-launch button.
    /// Only visible when multi-client is enabled and there are 2+ saved credentials.
    /// </summary>
    public bool ShowMultiLaunchButton =>
        _configService.Current.EnableMultiClient &&
        SavedCredentials.Count >= 2 &&
        !IsLaunching;

    /// <summary>
    /// Collection of batch groups for this server.
    /// </summary>
    public ObservableCollection<BatchGroup> BatchGroups { get; }

    /// <summary>
    /// The currently selected batch group.
    /// </summary>
    [ObservableProperty]
    private BatchGroup? _selectedBatch;

    /// <summary>
    /// Whether batch management UI is visible (expanded).
    /// </summary>
    [ObservableProperty]
    private bool _showBatchManagement;

    /// <summary>
    /// Status message for batch operations.
    /// </summary>
    [ObservableProperty]
    private string? _batchStatusMessage;

    /// <summary>
    /// Whether to show the batch management section at all.
    /// Only visible when multi-client is enabled and there are 2+ saved credentials.
    /// </summary>
    public bool ShowBatchSection =>
        _configService.Current.EnableMultiClient &&
        SavedCredentials.Count >= 2;

    /// <summary>
    /// Gets the favorite batch for this server, if any.
    /// </summary>
    public BatchGroup? FavoriteBatch => BatchGroups.FirstOrDefault(b => b.IsFavorite);

    /// <summary>
    /// Gets whether the quick launch favorite button should be shown.
    /// </summary>
    public bool ShowQuickLaunchFavorite => FavoriteBatch != null && !IsLaunching;

    /// <summary>
    /// Initializes a new instance of the WorldDetailViewModel.
    /// World data will be provided via OnNavigatedTo() when navigating to this view.
    /// </summary>
    /// <param name="worldsService">The worlds service.</param>
    /// <param name="credentialVaultService">The credential vault service.</param>
    /// <param name="gameLaunchService">The game launch service.</param>
    /// <param name="launchSequencerService">The launch sequencer service for multi-client launches.</param>
    /// <param name="batchGroupService">The batch group service for batch management.</param>
    /// <param name="configService">The configuration service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="logger">The logging service.</param>
    /// <param name="mainWindow">The main window view model for navigation.</param>
    public WorldDetailViewModel(
        WorldsService worldsService,
        CredentialVaultService credentialVaultService,
        MultiLaunchConfigService multiLaunchConfigService,
        GameLaunchService gameLaunchService,
        LaunchSequencerService launchSequencerService,
        BatchGroupService batchGroupService,
        ConfigService configService,
        INavigationService navigationService,
        LoggingService logger,
        MainWindowViewModel mainWindow)
    {
        _worldsService = worldsService;
        _credentialVaultService = credentialVaultService;
        _multiLaunchConfigService = multiLaunchConfigService;
        _gameLaunchService = gameLaunchService;
        _launchSequencerService = launchSequencerService;
        _batchGroupService = batchGroupService;
        _configService = configService;
        _navigationService = navigationService;
        _logger = logger;
        _mainWindow = mainWindow;
        _world = null!; // Will be set in OnNavigatedTo
        CredentialForm = null; // Will be set in OnNavigatedTo

        BatchGroups = new ObservableCollection<BatchGroup>();

        _logger.Debug("WorldDetailViewModel constructed");
    }

    /// <summary>
    /// Called when navigated to this view model. Receives the WorldDto as parameter.
    /// </summary>
    public override void OnNavigatedTo(object? parameter)
    {
        if (parameter is not WorldDto world)
        {
            _logger.Error("WorldDetailViewModel navigated to without WorldDto parameter");
            return;
        }

        World = world;
        _logger.Debug("WorldDetailViewModel initialized for world: {WorldName} (ID: {WorldId})", world.Name, world.WorldId);

        // Create credential form component
        CredentialForm = new CredentialFormViewModel(world.Id, _credentialVaultService, _logger);

        // Subscribe to credential form messages
        CredentialForm.MessageChanged += OnCredentialMessageChanged;

        // Forward property change notifications from CredentialForm
        CredentialForm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CredentialFormViewModel.NewUsername))
                OnPropertyChanged(nameof(NewUsername));
            else if (e.PropertyName == nameof(CredentialFormViewModel.NewPassword))
                OnPropertyChanged(nameof(NewPassword));
            else if (e.PropertyName == nameof(CredentialFormViewModel.NewDisplayName))
                OnPropertyChanged(nameof(NewDisplayName));
            else if (e.PropertyName == nameof(CredentialFormViewModel.IsSavingCredential))
                OnPropertyChanged(nameof(IsSavingCredential));
            else if (e.PropertyName == nameof(CredentialFormViewModel.IsLoadingCredentials))
                OnPropertyChanged(nameof(IsLoadingCredentials));
            else if (e.PropertyName == nameof(CredentialFormViewModel.ShowAddCredential))
                OnPropertyChanged(nameof(ShowAddCredential));
            else if (e.PropertyName == nameof(CredentialFormViewModel.SavedCredentials))
            {
                OnPropertyChanged(nameof(SavedCredentials));
                OnPropertyChanged(nameof(ShowMultiLaunchButton));
            }
            else if (e.PropertyName == nameof(CredentialFormViewModel.SelectedCredential))
                OnPropertyChanged(nameof(SelectedCredential));
            else if (e.PropertyName == nameof(CredentialFormViewModel.IsPasswordVisible))
                OnPropertyChanged(nameof(IsPasswordVisible));
            else if (e.PropertyName == nameof(CredentialFormViewModel.IsEditingMode))
                OnPropertyChanged(nameof(IsEditingMode));
        };

        // Notify that World property changed
        OnPropertyChanged(nameof(World));

        // Load batch groups for this server
        _ = LoadBatchGroupsAsync();

        // Check if connection info is available (for offline mode support)
        _ = CheckConnectionInfoAvailabilityAsync();
    }

    /// <summary>
    /// Handles messages from the credential form component.
    /// </summary>
    private void OnCredentialMessageChanged(object? sender, MessageEventArgs e)
    {
        if (e.IsError)
        {
            ErrorMessage = e.Message;
            SuccessMessage = null;
        }
        else
        {
            SuccessMessage = e.Message;
            ErrorMessage = null;
        }
    }

    /// <summary>
    /// Launches the game with the specified credential, or the selected/default credential if none specified.
    /// </summary>
    /// <param name="credential">The credential to use for launching. If null, uses SelectedCredential or most recent.</param>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync(SavedCredential? credential = null)
    {
        try
        {
            ErrorMessage = null;
            SuccessMessage = null;
            IsLaunching = true;
            StatusMessage = "Connecting to server...";

            _logger.Information("User initiating game launch for world: {WorldName} (ID: {WorldId})",
                World.Name, World.WorldId);

            // Check AC client configuration
            var (isValid, validationError) = _gameLaunchService.ValidateLaunchConfiguration();
            if (!isValid)
            {
                ErrorMessage = validationError ?? "AC client is not configured. Please check settings.";
                _logger.Warning("Launch aborted due to invalid configuration: {Error}", ErrorMessage);
                return;
            }

            // Determine which credential to use
            SavedCredential? credentialToUse = credential ?? SelectedCredential;

            // If no credential specified and no credential selected, use most recently used one
            if (credentialToUse == null && SavedCredentials.Count > 0)
            {
                credentialToUse = SavedCredentials.OrderByDescending(c => c.LastUsed).FirstOrDefault();
                _logger.Information("Auto-selected most recent credential: {Username}", credentialToUse?.Username);
            }

            StatusMessage = "Retrieving connection information...";

            // Get connection info from API
            var connectionInfo = await _worldsService.GetConnectionInfoAsync(World.ServerId);

            if (connectionInfo == null)
            {
                ErrorMessage = "Failed to retrieve connection information. Please try again.";
                _logger.Error("Failed to get connection info for world {WorldId}", World.ServerId);
                return;
            }

            StatusMessage = "Launching AC client...";

            // Launch the game
            var launchResult = await _gameLaunchService.LaunchGameAsync(connectionInfo, credentialToUse);

            if (launchResult.Success)
            {
                SuccessMessage = $"Game launched successfully! Process ID: {launchResult.ProcessId}";
                _logger.Information("Game launched successfully for world: {WorldName}", World.Name);
                StatusMessage = "Game launched successfully!";

                // Update selected credential (it's now the most recently used)
                if (credentialToUse != null && CredentialForm != null)
                {
                    await CredentialForm.LoadCredentialsAsync(); // Refresh to show updated timestamps
                }
            }
            else
            {
                ErrorMessage = launchResult.ErrorMessage ?? "Failed to launch game.";
                _logger.Error("Game launch failed for world {WorldId}: {Error}", World.WorldId, ErrorMessage);
                StatusMessage = null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error during game launch");
            ErrorMessage = $"An unexpected error occurred: {ex.Message}";
            StatusMessage = null;
        }
        finally
        {
            IsLaunching = false;
        }
    }

    /// <summary>
    /// Determines whether the play command can execute.
    /// Disabled if: launching is in progress, connection info is being checked, or no connection info available.
    /// </summary>
    private bool CanPlay()
    {
        // Can't play if already launching
        if (IsLaunching)
            return false;

        // Can't play if still checking connection availability
        if (IsCheckingConnection)
            return false;

        // Can't play if no connection info available (offline mode without cache)
        if (!HasConnectionInfo)
        {
            StatusMessage = "Cannot play - server connection details not yet configured for this world.";
            return false;
        }

        // Clear status message if play is available
        if (StatusMessage == "Cannot play - server connection details not yet configured for this world.")
            StatusMessage = null;

        return true;
    }

    /// <summary>
    /// Shows the add credential form (delegated to CredentialForm).
    /// </summary>
    [RelayCommand]
    private void AddCredential()
    {
        CredentialForm?.AddCredentialCommand.Execute(null);
    }

    /// <summary>
    /// Saves the new credential to the vault (delegated to CredentialForm).
    /// </summary>
    [RelayCommand]
    private async Task SaveCredentialAsync()
    {
        if (CredentialForm != null)
            await CredentialForm.SaveCredentialCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Cancels adding a new credential and closes the form (delegated to CredentialForm).
    /// </summary>
    [RelayCommand]
    private void CancelAddCredential()
    {
        CredentialForm?.CancelAddCredentialCommand.Execute(null);
    }

    /// <summary>
    /// Toggles password visibility (delegated to CredentialForm).
    /// </summary>
    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        CredentialForm?.TogglePasswordVisibilityCommand.Execute(null);
    }

    /// <summary>
    /// Opens the multi-launch dialog for launching multiple game clients.
    /// </summary>
    [RelayCommand]
    private async Task OpenMultiLaunchDialogAsync()
    {
        try
        {
            if (ConnectionInfo == null)
            {
                ErrorMessage = "Connection information not available. Please try refreshing the world list.";
                _logger.Warning("Cannot open multi-launch dialog: ConnectionInfo is null for world {WorldId}", World.Id);
                return;
            }

            _logger.Information("Opening multi-launch dialog for world {World}", World.Name);

            // Create the dialog ViewModel
            var dialogViewModel = new MultiLaunchDialogViewModel(
                World,
                ConnectionInfo,
                _credentialVaultService,
                _multiLaunchConfigService,
                _launchSequencerService,
                _gameLaunchService,
                _configService,
                _navigationService,
                _logger);

            // Create and show the dialog
            var dialog = new Views.MultiLaunchDialog
            {
                DataContext = dialogViewModel
            };

            // Save configuration when dialog closes
            dialog.Closing += async (s, e) =>
            {
                await dialogViewModel.SaveConfigurationAsync();
            };

            // Get the main window to set as owner
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow != null)
            {
                await dialog.ShowDialog(mainWindow);
            }
            else
            {
                _logger.Warning("Could not find main window for dialog owner");
                await dialog.ShowDialog((Avalonia.Controls.Window)null!);
            }

            _logger.Debug("Multi-launch dialog closed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening multi-launch dialog");
            ErrorMessage = $"Failed to open multi-launch dialog: {ex.Message}";
        }
    }

    /// <summary>
    /// Edits the selected credential (delegated to CredentialForm).
    /// </summary>
    /// <param name="credential">The credential to edit.</param>
    [RelayCommand]
    private void EditCredential(SavedCredential? credential)
    {
        CredentialForm?.EditCredentialCommand.Execute(credential);
    }

    /// <summary>
    /// Deletes a saved credential (delegated to CredentialForm).
    /// </summary>
    /// <param name="credential">The credential to delete.</param>
    [RelayCommand]
    private async Task DeleteCredentialAsync(SavedCredential? credential)
    {
        if (CredentialForm != null)
            await CredentialForm.DeleteCredentialCommand.ExecuteAsync(credential);
    }

    /// <summary>
    /// Opens the world's page on the OldPortal website in the default browser.
    /// Uses slug-based URL when available (e.g., /servers/asheron4funcom),
    /// falls back to GUID-based URL if slug is not available.
    /// </summary>
    [RelayCommand]
    private void ViewOnWeb()
    {
        try
        {
            // Prefer slug-based URL, fallback to GUID if slug not available
            var baseUrl = _configService.Current.ApiBaseUrl.Replace("/api/v1", "").Replace("/api", "");
            var url = !string.IsNullOrWhiteSpace(World.Slug)
                ? $"{baseUrl}/servers/{World.Slug}"
                : $"{baseUrl}/worlds/{World.WorldId}";

            _logger.Information("Opening world URL: {Url} (using {UrlType})",
                url, !string.IsNullOrWhiteSpace(World.Slug) ? "slug" : "GUID");

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open world URL");
            ErrorMessage = "Failed to open browser.";
        }
    }

    /// <summary>
    /// Navigates back to the worlds browse view.
    /// </summary>
    [RelayCommand]
    private void Back()
    {
        _logger.Debug("User navigating back from world detail view");
        _navigationService.GoBack();
    }

    /// <summary>
    /// Shares the server by copying its URL to the clipboard.
    /// Uses slug-based URL when available (e.g., /servers/asheron4funcom),
    /// falls back to GUID-based URL if slug is not available.
    /// </summary>
    [RelayCommand]
    private async Task ShareServerAsync()
    {
        try
        {
            // Prefer slug-based URL, fallback to GUID if slug not available
            var baseUrl = _configService.Current.ApiBaseUrl.Replace("/api/v1", "").Replace("/api", "");
            var url = !string.IsNullOrWhiteSpace(World.Slug)
                ? $"{baseUrl}/servers/{World.Slug}"
                : $"{baseUrl}/worlds/{World.WorldId}";

            // Get the clipboard from the application's TopLevel
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(url);
                SuccessMessage = $"Server link copied to clipboard: {url}";
                _logger.Information("Copied server link to clipboard: {Url} (using {UrlType})",
                    url, !string.IsNullOrWhiteSpace(World.Slug) ? "slug" : "GUID");
            }
            else
            {
                ErrorMessage = "Could not access clipboard";
                _logger.Warning("Failed to access clipboard for server sharing");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to copy link to clipboard";
            _logger.Error(ex, "Error copying server link to clipboard");
        }
    }

    /// <summary>
    /// Refreshes the world details and credentials.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            _logger.Information("User refreshing world details for: {WorldName}", World.Name);

            // Reload world details from API
            var updatedWorld = await _worldsService.GetWorldByIdAsync(World.ServerId);
            if (updatedWorld != null)
            {
                World = updatedWorld;
                SuccessMessage = "World details refreshed.";
            }
            else
            {
                ErrorMessage = "Failed to refresh world details.";
            }

            // Reload credentials
            if (CredentialForm != null)
                await CredentialForm.LoadCredentialsAsync();

            // Re-check connection info availability (important for offline mode)
            await CheckConnectionInfoAvailabilityAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error refreshing world details");
            ErrorMessage = $"Error refreshing: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks if connection info is available for this world.
    /// This enables offline mode - if we have cached connection info, the Play button can be enabled.
    /// </summary>
    private async Task CheckConnectionInfoAvailabilityAsync()
    {
        try
        {
            IsCheckingConnection = true;
            _logger.Debug("Checking connection info availability for server {ServerId}", World.ServerId);

            // Try to get connection info (will use cache if API is offline)
            var connectionInfo = await _worldsService.GetConnectionInfoAsync(World.ServerId);

            ConnectionInfo = connectionInfo;
            HasConnectionInfo = connectionInfo != null;

            if (HasConnectionInfo)
            {
                _logger.Information("Connection info available for server {ServerId} (API online: {IsOnline})",
                    World.ServerId, _worldsService.IsOnline);
            }
            else
            {
                _logger.Warning("No connection info available for server {ServerId} (API online: {IsOnline})",
                    World.ServerId, _worldsService.IsOnline);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking connection info availability for server {ServerId}", World.ServerId);
            ConnectionInfo = null;
            HasConnectionInfo = false;
        }
        finally
        {
            IsCheckingConnection = false;
        }
    }

    /// <summary>
    /// Called when IsLaunching changes to update command availability.
    /// </summary>
    partial void OnIsLaunchingChanged(bool value)
    {
        PlayCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowMultiLaunchButton));
    }

    /// <summary>
    /// Called when HasConnectionInfo changes to update command availability.
    /// </summary>
    partial void OnHasConnectionInfoChanged(bool value)
    {
        PlayCommand.NotifyCanExecuteChanged();
        _logger.Debug("HasConnectionInfo changed to {Value}, PlayCommand enabled: {CanExecute}",
            value, PlayCommand.CanExecute(null));
    }

    /// <summary>
    /// Called when IsCheckingConnection changes to update command availability.
    /// </summary>
    partial void OnIsCheckingConnectionChanged(bool value)
    {
        PlayCommand.NotifyCanExecuteChanged();
    }

    #region Batch Group Management

    /// <summary>
    /// Loads batch groups for this server from the database.
    /// </summary>
    private async Task LoadBatchGroupsAsync()
    {
        try
        {
            _logger.Debug("Loading batch groups for world {WorldId}", World.Id);

            var batches = await _batchGroupService.GetBatchGroupsForServerAsync(World.Id);

            BatchGroups.Clear();
            foreach (var batch in batches)
            {
                BatchGroups.Add(batch);
            }

            _logger.Information("Loaded {Count} batch groups for world {WorldId}", batches.Count, World.Id);

            // Auto-select the last used batch
            if (batches.Count > 0 && batches[0].LastUsedDate.HasValue)
            {
                SelectedBatch = batches[0];
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading batch groups for world {WorldId}", World.Id);
            BatchStatusMessage = "Failed to load batch groups.";
        }
    }

    /// <summary>
    /// Toggles the batch management UI visibility.
    /// </summary>
    [RelayCommand]
    private void ToggleBatchManagement()
    {
        ShowBatchManagement = !ShowBatchManagement;
        _logger.Debug("Batch management visibility toggled to: {Visible}", ShowBatchManagement);
    }

    /// <summary>
    /// Selects a batch group.
    /// </summary>
    [RelayCommand]
    private void SelectBatch(BatchGroup? batch)
    {
        SelectedBatch = batch;
        _logger.Debug("Selected batch: {Name}", batch?.Name ?? "None");
    }

    /// <summary>
    /// Creates a new batch group.
    /// </summary>
    [RelayCommand]
    private void NewBatch()
    {
        try
        {
            var newBatch = new BatchGroup
            {
                Name = "New Batch",
                WorldId = World.Id,
                Entries = new List<BatchEntry>()
            };

            BatchGroups.Add(newBatch);
            SelectedBatch = newBatch;

            _logger.Information("Created new batch group for world {WorldId}", World.Id);
            BatchStatusMessage = "New batch created. Add accounts and save.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating new batch");
            BatchStatusMessage = "Failed to create new batch.";
        }
    }

    /// <summary>
    /// Saves the currently selected batch group.
    /// </summary>
    [RelayCommand]
    private async Task SaveBatchAsync()
    {
        if (SelectedBatch == null)
        {
            BatchStatusMessage = "No batch selected to save.";
            return;
        }

        try
        {
            _logger.Information("Saving batch group {Id}: {Name}", SelectedBatch.Id, SelectedBatch.Name);

            var success = await _batchGroupService.SaveBatchGroupAsync(SelectedBatch);

            if (success)
            {
                BatchStatusMessage = $"✓ Batch '{SelectedBatch.Name}' saved successfully!";
                _logger.Information("Batch group saved successfully");
            }
            else
            {
                var errors = await _batchGroupService.GetValidationErrorsAsync(SelectedBatch);
                BatchStatusMessage = $"✗ Failed to save: {string.Join(", ", errors)}";
                _logger.Warning("Batch group validation failed: {Errors}", string.Join(", ", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving batch group");
            BatchStatusMessage = $"✗ Error saving batch: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes the currently selected batch group.
    /// </summary>
    [RelayCommand]
    private async Task DeleteBatchAsync()
    {
        if (SelectedBatch == null)
        {
            BatchStatusMessage = "No batch selected to delete.";
            return;
        }

        try
        {
            var batchName = SelectedBatch.Name;
            var batchId = SelectedBatch.Id;

            _logger.Information("Deleting batch group {Id}: {Name}", batchId, batchName);

            var success = await _batchGroupService.DeleteBatchGroupAsync(batchId);

            if (success)
            {
                BatchGroups.Remove(SelectedBatch);
                SelectedBatch = null;
                BatchStatusMessage = $"✓ Batch '{batchName}' deleted.";
                _logger.Information("Batch group deleted successfully");
            }
            else
            {
                BatchStatusMessage = "Failed to delete batch.";
                _logger.Warning("Batch group deletion failed");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting batch group");
            BatchStatusMessage = $"✗ Error deleting batch: {ex.Message}";
        }
    }

    /// <summary>
    /// Launches the currently selected batch group.
    /// </summary>
    [RelayCommand]
    private async Task LaunchBatchAsync()
    {
        if (SelectedBatch == null)
        {
            BatchStatusMessage = "No batch selected to launch.";
            return;
        }

        if (ConnectionInfo == null)
        {
            BatchStatusMessage = "Connection information not available.";
            return;
        }

        try
        {
            _logger.Information("Launching batch group {Id}: {Name} ({Count} accounts)",
                SelectedBatch.Id, SelectedBatch.Name, SelectedBatch.Entries.Count);

            // Validate batch before launch
            var errors = await _batchGroupService.GetValidationErrorsAsync(SelectedBatch);
            if (errors.Count > 0)
            {
                BatchStatusMessage = $"✗ Cannot launch: {string.Join(", ", errors)}";
                _logger.Warning("Batch validation failed: {Errors}", string.Join(", ", errors));
                return;
            }

            IsLaunching = true;
            BatchStatusMessage = $"Launching {SelectedBatch.Entries.Count} clients...";

            // Build launch tasks from batch entries
            var credentials = await _credentialVaultService.GetCredentialsForWorldAsync(World.Id);
            var launchTasks = new List<LaunchTask>();

            foreach (var entry in SelectedBatch.Entries.OrderBy(e => e.LaunchOrder))
            {
                var credential = credentials.FirstOrDefault(c =>
                    c.Username.Equals(entry.CredentialUsername, StringComparison.OrdinalIgnoreCase));

                if (credential == null)
                {
                    _logger.Warning("Credential not found for batch entry: {Username}", entry.CredentialUsername);
                    continue;
                }

                launchTasks.Add(new LaunchTask
                {
                    Connection = ConnectionInfo,
                    Credential = credential,
                    Order = entry.LaunchOrder,
                    DelaySeconds = entry.DelaySeconds,
                    Notes = entry.Notes
                });
            }

            // Launch the sequence
            var result = await _launchSequencerService.LaunchSequenceAsync(
                launchTasks,
                abortOnFailure: false);

            // Update batch last used date
            await _batchGroupService.MarkAsUsedAsync(SelectedBatch.Id);
            SelectedBatch.MarkAsUsed();

            // Record launch history
            await _batchGroupService.RecordLaunchHistoryAsync(SelectedBatch, result);

            if (result.Success)
            {
                BatchStatusMessage = $"✓ Launched {result.SuccessCount} of {result.TotalTasks} clients successfully!";
                _logger.Information("Batch launch completed successfully");
            }
            else
            {
                BatchStatusMessage = $"⚠ Launched {result.SuccessCount}/{result.TotalTasks}. {result.FailureCount} failed.";
                _logger.Warning("Batch launch completed with errors");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error launching batch");
            BatchStatusMessage = $"✗ Error launching batch: {ex.Message}";
        }
        finally
        {
            IsLaunching = false;
        }
    }

    /// <summary>
    /// Quick launches the favorite batch without needing to expand the batch section.
    /// </summary>
    [RelayCommand]
    private async Task QuickLaunchFavoriteAsync()
    {
        if (FavoriteBatch == null)
        {
            BatchStatusMessage = "No favorite batch set for quick launch.";
            return;
        }

        try
        {
            _logger.Information("Quick launching favorite batch: {Name}", FavoriteBatch.Name);

            // Temporarily select the favorite batch
            SelectedBatch = FavoriteBatch;

            // Launch it
            await LaunchBatchAsync();

            _logger.Information("Quick launch of favorite batch completed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error quick launching favorite batch");
            BatchStatusMessage = $"✗ Error launching favorite: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the multi-client help documentation view.
    /// </summary>
    [RelayCommand]
    private void OpenMultiClientHelp()
    {
        try
        {
            _logger.Information("Opening multi-client help documentation from WorldDetailView");
            _navigationService.NavigateTo<MultiClientHelpViewModel>();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening multi-client help");
            BatchStatusMessage = "Failed to open help documentation.";
        }
    }

    #endregion

    /// <summary>
    /// Disposes resources used by the WorldDetailViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.Debug("WorldDetailViewModel disposed");
    }
}
