using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Models;
using OPLauncher.Services;

namespace OPLauncher.ViewModels;

/// <summary>
/// ViewModel for the manual server detail screen.
/// Displays detailed information about a manual server and manages credentials for launching.
///
/// INLINE CREDENTIAL MANAGEMENT PATTERN:
/// This ViewModel uses the same inline credential management pattern as WorldDetailViewModel.
/// See WorldDetailViewModel class documentation for comprehensive pattern explanation.
///
/// KEY DIFFERENCES FROM WORLDDETAILVIEWMODEL:
/// 1. MANUAL SERVERS: Manages credentials for user-added servers (localhost, private servers)
///    rather than OldPortal-listed public worlds
///
/// 2. SERVER OBJECT TYPE: Works with ManualServer model instead of WorldDto, but credential
///    management logic is identical (uses CredentialVaultService with server.Id as key)
///
/// 3. LOCAL-ONLY STORAGE: Manual servers are stored in local LiteDB only (not synced with API)
///    This makes the launcher fully functional for developers running localhost AC servers
///
/// 4. SAME UX PATTERN: Despite different data sources, the user experience is identical:
///    - Expandable inline credential form
///    - List of saved credentials with Play/Edit/Delete buttons
///    - Per-credential launch workflow
///    - Encrypted password storage via DPAPI
///
/// USE CASES:
/// - Developers running ACE emulator on localhost for testing
/// - Private servers not listed on OldPortal.com
/// - LAN servers for local multiplayer events
/// - Test servers with custom configurations
///
/// CREDENTIAL STORAGE:
/// Credentials are stored using the manual server's Id (string-based, e.g., "localhost-9000")
/// as the key in CredentialVaultService. This ensures credentials don't conflict with
/// public world credentials (which use integer WorldId).
///
/// UI LAYOUT (ManualServerDetailView.axaml):
/// Identical to WorldDetailView.axaml credential management section.
///
/// See also: WorldDetailViewModel (reference implementation of inline credential pattern)
/// </summary>
public partial class ManualServerDetailViewModel : ViewModelBase, IDisposable
{
    private readonly ManualServersService _manualServersService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly ConfigService _configService;
    private readonly INavigationService _navigationService;
    private readonly LoggingService _logger;
    private readonly MainWindowViewModel _mainWindow;
    private bool _disposed;

    /// <summary>
    /// The manual server being displayed.
    /// </summary>
    [ObservableProperty]
    private ManualServer _server;

    /// <summary>
    /// The credential form component that handles all credential management.
    /// </summary>
    public CredentialFormViewModel? CredentialForm { get; private set; }

    /// <summary>
    /// Collection of saved credentials for this server (forwarded from CredentialForm).
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
    /// Whether to show the add credential form (forwarded from CredentialForm).
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
    /// Initializes a new instance of the ManualServerDetailViewModel.
    /// Server data will be provided via OnNavigatedTo() when navigating to this view.
    /// </summary>
    /// <param name="manualServersService">The manual servers service.</param>
    /// <param name="credentialVaultService">The credential vault service.</param>
    /// <param name="gameLaunchService">The game launch service.</param>
    /// <param name="configService">The configuration service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="logger">The logging service.</param>
    /// <param name="mainWindow">The main window view model for navigation.</param>
    public ManualServerDetailViewModel(
        ManualServersService manualServersService,
        CredentialVaultService credentialVaultService,
        GameLaunchService gameLaunchService,
        ConfigService configService,
        INavigationService navigationService,
        LoggingService logger,
        MainWindowViewModel mainWindow)
    {
        _manualServersService = manualServersService;
        _credentialVaultService = credentialVaultService;
        _gameLaunchService = gameLaunchService;
        _configService = configService;
        _navigationService = navigationService;
        _logger = logger;
        _mainWindow = mainWindow;
        _server = null!; // Will be set in OnNavigatedTo
        CredentialForm = null; // Will be set in OnNavigatedTo

        _logger.Debug("ManualServerDetailViewModel constructed");
    }

    /// <summary>
    /// Called when navigated to this view model. Receives the ManualServer as parameter.
    /// </summary>
    public override void OnNavigatedTo(object? parameter)
    {
        if (parameter is not ManualServer server)
        {
            _logger.Error("ManualServerDetailViewModel navigated to without ManualServer parameter");
            return;
        }

        Server = server;
        _logger.Debug("ManualServerDetailViewModel initialized for server: {ServerName} (ID: {ServerId})", server.Name, server.Id);

        // Create credential form component (using negative world ID for manual servers)
        CredentialForm = new CredentialFormViewModel(GetCredentialWorldId(), _credentialVaultService, _logger);

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
                OnPropertyChanged(nameof(SavedCredentials));
            else if (e.PropertyName == nameof(CredentialFormViewModel.SelectedCredential))
                OnPropertyChanged(nameof(SelectedCredential));
        };

        // Notify that Server property changed
        OnPropertyChanged(nameof(Server));
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
    /// Gets the world ID used for credential storage.
    /// Manual servers use negative IDs to avoid conflicts with OldPortal.com servers.
    /// </summary>
    private int GetCredentialWorldId() => -Server.Id;

    /// <summary>
    /// Launches the game with the specified credential.
    /// </summary>
    /// <param name="credential">The credential to use for launching.</param>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync(SavedCredential? credential = null)
    {
        try
        {
            ErrorMessage = null;
            SuccessMessage = null;
            IsLaunching = true;
            StatusMessage = "Connecting to server...";

            _logger.Information("User initiating game launch for manual server: {ServerName} (ID: {ServerId})",
                Server.Name, Server.Id);

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

            StatusMessage = "Launching AC client...";

            // Convert manual server to connection info
            var connectionInfo = Server.ToConnectionInfo();

            // Launch the game
            var launchResult = await _gameLaunchService.LaunchGameAsync(connectionInfo, credentialToUse);

            if (launchResult.Success)
            {
                SuccessMessage = $"Game launched successfully! Process ID: {launchResult.ProcessId}";
                _logger.Information("Game launched successfully for manual server: {ServerName}", Server.Name);
                StatusMessage = "Game launched successfully!";

                // Update last connected time
                await _manualServersService.UpdateLastConnectedAsync(Server.Id);

                // Update selected credential (it's now the most recently used)
                if (credentialToUse != null && CredentialForm != null)
                {
                    await CredentialForm.LoadCredentialsAsync(); // Refresh to show updated timestamps
                }
            }
            else
            {
                ErrorMessage = launchResult.ErrorMessage ?? "Failed to launch game.";
                _logger.Error("Game launch failed for manual server {ServerId}: {Error}", Server.Id, ErrorMessage);
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
    /// </summary>
    private bool CanPlay()
    {
        return !IsLaunching;
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
    /// Navigates back to the manual servers view.
    /// </summary>
    [RelayCommand]
    private void Back()
    {
        _logger.Debug("User navigating back from manual server detail view");
        _navigationService.GoBack();
    }

    /// <summary>
    /// Called when IsLaunching changes to update command availability.
    /// </summary>
    partial void OnIsLaunchingChanged(bool value)
    {
        PlayCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Disposes resources used by the ManualServerDetailViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.Debug("ManualServerDetailViewModel disposed");
    }
}
