using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.DTOs;
using OPLauncher.Models;
using OPLauncher.Services;

namespace OPLauncher.ViewModels;

/// <summary>
/// Represents a single credential entry in the multi-launch dialog.
/// </summary>
public partial class LaunchEntryViewModel : ObservableObject
{
    /// <summary>
    /// Gets or sets the saved credential for this entry.
    /// </summary>
    public SavedCredential Credential { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether this entry is selected for launch.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>
    /// Gets or sets the launch order (1-based).
    /// </summary>
    [ObservableProperty]
    private int _order = 1;

    /// <summary>
    /// Gets or sets the delay in seconds to wait after launching this client.
    /// </summary>
    [ObservableProperty]
    private int _delaySeconds = 3;

    /// <summary>
    /// Gets the display name for this credential.
    /// </summary>
    public string DisplayName => Credential?.GetFullDisplayText() ?? "Unknown";
}

/// <summary>
/// ViewModel for the multi-launch dialog that allows launching multiple game clients sequentially.
/// </summary>
public partial class MultiLaunchDialogViewModel : ViewModelBase
{
    private readonly WorldDto _world;
    private readonly WorldConnectionDto _connection;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly MultiLaunchConfigService _multiLaunchConfigService;
    private readonly LaunchSequencerService _launchSequencerService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly ConfigService _configService;
    private readonly INavigationService _navigationService;
    private readonly LoggingService _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Gets the world name for display.
    /// </summary>
    public string WorldName => _world.Name;

    /// <summary>
    /// Gets the collection of launch entries (credentials).
    /// </summary>
    public ObservableCollection<LaunchEntryViewModel> Entries { get; }

    /// <summary>
    /// Gets or sets whether a launch sequence is currently in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isLaunching;

    /// <summary>
    /// Gets or sets the current status message.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Gets or sets the number of clients launched so far.
    /// </summary>
    [ObservableProperty]
    private int _launchedCount;

    /// <summary>
    /// Gets or sets the total number of selected entries.
    /// </summary>
    [ObservableProperty]
    private int _totalSelected;

    /// <summary>
    /// Gets or sets whether to launch sequentially (with delays) or simultaneously.
    /// </summary>
    [ObservableProperty]
    private bool _launchSequentially = true;

    /// <summary>
    /// Gets or sets the default delay in seconds between launches.
    /// </summary>
    [ObservableProperty]
    private int _defaultDelay = 3;

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    [ObservableProperty]
    private int _progressPercentage;

    /// <summary>
    /// Gets or sets the result message shown after launch completes.
    /// </summary>
    [ObservableProperty]
    private string? _resultMessage;

    /// <summary>
    /// Gets or sets whether the dialog should close.
    /// </summary>
    [ObservableProperty]
    private bool _shouldClose;

    /// <summary>
    /// Initializes a new instance of the MultiLaunchDialogViewModel.
    /// </summary>
    public MultiLaunchDialogViewModel(
        WorldDto world,
        WorldConnectionDto connection,
        CredentialVaultService credentialVaultService,
        MultiLaunchConfigService multiLaunchConfigService,
        LaunchSequencerService launchSequencerService,
        GameLaunchService gameLaunchService,
        ConfigService configService,
        INavigationService navigationService,
        LoggingService logger)
    {
        _world = world;
        _connection = connection;
        _credentialVaultService = credentialVaultService;
        _multiLaunchConfigService = multiLaunchConfigService;
        _launchSequencerService = launchSequencerService;
        _gameLaunchService = gameLaunchService;
        _configService = configService;
        _navigationService = navigationService;
        _logger = logger;

        Entries = new ObservableCollection<LaunchEntryViewModel>();

        // Load default delay from config
        _defaultDelay = _configService.Current.DefaultLaunchDelay;

        // Subscribe to entry selection changes
        Entries.CollectionChanged += (s, e) => UpdateSelectedCount();

        _logger.Debug("MultiLaunchDialogViewModel initialized for world: {World}", world.Name);

        // Load credentials
        LoadCredentials();
    }

    /// <summary>
    /// Loads saved credentials for the world and creates launch entries.
    /// Applies saved configuration if available.
    /// </summary>
    private async void LoadCredentials()
    {
        try
        {
            var credentials = await _credentialVaultService.GetCredentialsForWorldAsync(_world.Id);

            if (credentials.Count == 0)
            {
                _logger.Warning("No saved credentials found for world {WorldId}", _world.Id);
                StatusMessage = "No saved credentials found for this world.";
                return;
            }

            _logger.Information("Loaded {Count} credentials for world {WorldId}", credentials.Count, _world.Id);

            // Try to load saved multi-launch configuration
            var savedConfig = await _multiLaunchConfigService.GetConfigurationAsync(_world.Id);

            List<LaunchEntryViewModel> entriesToAdd = new();

            if (savedConfig != null && savedConfig.Entries.Count > 0)
            {
                _logger.Information("Applying saved multi-launch configuration with {Count} entries", savedConfig.Entries.Count);

                // Build entries from saved config first
                foreach (var savedEntry in savedConfig.Entries.OrderBy(e => e.Order))
                {
                    var credential = credentials.FirstOrDefault(c => c.Id == savedEntry.CredentialId);
                    if (credential != null)
                    {
                        var entry = new LaunchEntryViewModel
                        {
                            Credential = credential,
                            IsSelected = true,
                            Order = savedEntry.Order,
                            DelaySeconds = savedEntry.DelaySeconds
                        };

                        entriesToAdd.Add(entry);
                    }
                }

                // Append any new credentials that weren't in the saved config
                var nextOrder = savedConfig.Entries.Max(e => e.Order) + 1;
                foreach (var credential in credentials)
                {
                    if (!entriesToAdd.Any(e => e.Credential.Id == credential.Id))
                    {
                        _logger.Debug("New credential {CredentialId} not in saved config, appending with order {Order}", credential.Id, nextOrder);
                        var entry = new LaunchEntryViewModel
                        {
                            Credential = credential,
                            IsSelected = true,
                            Order = nextOrder++,
                            DelaySeconds = DefaultDelay
                        };

                        entriesToAdd.Add(entry);
                    }
                }
            }
            else
            {
                _logger.Debug("No saved configuration found, using default order (by LastUsed)");

                // No saved config - use default ordering by LastUsed
                int order = 1;
                foreach (var credential in credentials.OrderBy(c => c.LastUsed).Reverse())
                {
                    var entry = new LaunchEntryViewModel
                    {
                        Credential = credential,
                        IsSelected = true,
                        Order = order++,
                        DelaySeconds = DefaultDelay
                    };

                    entriesToAdd.Add(entry);
                }
            }

            // Add all entries and subscribe to property changes
            foreach (var entry in entriesToAdd)
            {
                // Subscribe to property changes
                entry.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(LaunchEntryViewModel.IsSelected))
                    {
                        UpdateSelectedCount();
                    }
                };

                Entries.Add(entry);
            }

            UpdateSelectedCount();
            _logger.Debug("Created {Count} launch entries", Entries.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading credentials for multi-launch dialog");
            StatusMessage = "Error loading credentials. Please try again.";
        }
    }

    /// <summary>
    /// Updates the count of selected entries.
    /// </summary>
    private void UpdateSelectedCount()
    {
        TotalSelected = Entries.Count(e => e.IsSelected);
        LaunchSelectedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Reorders the entries collection based on the Order property.
    /// </summary>
    private void ReorderEntries()
    {
        _logger.Debug("Reordering entries based on Order property");

        // Get all entries sorted by Order
        var sortedEntries = Entries.OrderBy(e => e.Order).ToList();

        // Clear and re-add in sorted order
        Entries.Clear();
        foreach (var entry in sortedEntries)
        {
            Entries.Add(entry);
        }
    }

    /// <summary>
    /// Moves an entry up in the order.
    /// </summary>
    [RelayCommand]
    private void MoveUp(LaunchEntryViewModel entry)
    {
        if (entry == null) return;

        var currentIndex = Entries.IndexOf(entry);
        if (currentIndex <= 0) return; // Already at top

        _logger.Debug("Moving entry {DisplayName} up from index {Index}", entry.DisplayName, currentIndex);

        // Swap with the item above
        var itemAbove = Entries[currentIndex - 1];

        // Swap Order values
        var tempOrder = entry.Order;
        entry.Order = itemAbove.Order;
        itemAbove.Order = tempOrder;

        // Swap positions in collection
        Entries.RemoveAt(currentIndex);
        Entries.Insert(currentIndex - 1, entry);
    }

    /// <summary>
    /// Moves an entry down in the order.
    /// </summary>
    [RelayCommand]
    private void MoveDown(LaunchEntryViewModel entry)
    {
        if (entry == null) return;

        var currentIndex = Entries.IndexOf(entry);
        if (currentIndex < 0 || currentIndex >= Entries.Count - 1) return; // Already at bottom

        _logger.Debug("Moving entry {DisplayName} down from index {Index}", entry.DisplayName, currentIndex);

        // Swap with the item below
        var itemBelow = Entries[currentIndex + 1];

        // Swap Order values
        var tempOrder = entry.Order;
        entry.Order = itemBelow.Order;
        itemBelow.Order = tempOrder;

        // Swap positions in collection
        Entries.RemoveAt(currentIndex);
        Entries.Insert(currentIndex + 1, entry);
    }

    /// <summary>
    /// Selects all entries.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        _logger.Debug("Selecting all entries");
        foreach (var entry in Entries)
        {
            entry.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Deselects all entries.
    /// </summary>
    [RelayCommand]
    private void SelectNone()
    {
        _logger.Debug("Deselecting all entries");
        foreach (var entry in Entries)
        {
            entry.IsSelected = false;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Determines whether the launch command can execute.
    /// </summary>
    private bool CanLaunchSelected()
    {
        return !IsLaunching && TotalSelected > 0;
    }

    /// <summary>
    /// Launches the selected game clients.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLaunchSelected))]
    private async Task LaunchSelectedAsync()
    {
        try
        {
            IsLaunching = true;
            LaunchedCount = 0;
            ProgressPercentage = 0;
            ResultMessage = null;
            _cancellationTokenSource = new CancellationTokenSource();

            // Suppress individual launch progress events during multi-launch
            _gameLaunchService.SuppressProgressEvents = true;

            var selectedEntries = Entries.Where(e => e.IsSelected).OrderBy(e => e.Order).ToList();

            _logger.Information("========================================");
            _logger.Information("=== MULTI-CLIENT LAUNCH INITIATED ===");
            _logger.Information("World: {WorldName} (ID: {WorldId})", _world.Name, _world.Id);
            _logger.Information("Selected clients: {Count}", selectedEntries.Count);
            _logger.Information("Launch mode: {Mode}", LaunchSequentially ? "Sequential" : "Simultaneous");
            _logger.Information("========================================");

            // Build launch tasks
            var launchTasks = selectedEntries.Select(entry => new LaunchTask
            {
                Connection = _connection,
                Credential = entry.Credential,
                Order = entry.Order,
                DelaySeconds = LaunchSequentially ? entry.DelaySeconds : 0,
                Notes = entry.Credential.GetDisplayText()
            }).ToList();

            // Subscribe to sequencer events
            _launchSequencerService.ProgressUpdated += OnLaunchProgress;
            _launchSequencerService.LaunchCompleted += OnLaunchCompleted;
            _launchSequencerService.SequenceCompleted += OnSequenceCompleted;

            try
            {
                StatusMessage = $"Launching {selectedEntries.Count} clients...";

                // Launch the sequence
                var result = await _launchSequencerService.LaunchSequenceAsync(
                    launchTasks,
                    abortOnFailure: false,
                    _cancellationTokenSource.Token);

                // Handle result
                if (result.WasCancelled)
                {
                    ResultMessage = "Launch cancelled by user.";
                    _logger.Information("Multi-launch cancelled by user");
                }
                else if (result.Success)
                {
                    ResultMessage = $"✓ Successfully launched {result.SuccessCount} of {result.TotalTasks} clients!";
                    _logger.Information("Multi-launch completed successfully: {Summary}", result.Summary);

                    // Auto-close after success (with delay to show message)
                    await Task.Delay(2000);
                    ShouldClose = true;
                }
                else
                {
                    ResultMessage = $"⚠ Launched {result.SuccessCount} of {result.TotalTasks} clients. {result.FailureCount} failed.";
                    _logger.Warning("Multi-launch completed with errors: {Summary}", result.Summary);

                    if (result.Errors.Count > 0)
                    {
                        StatusMessage = string.Join("; ", result.Errors.Take(3));
                    }
                }
            }
            finally
            {
                // Unsubscribe from events
                _launchSequencerService.ProgressUpdated -= OnLaunchProgress;
                _launchSequencerService.LaunchCompleted -= OnLaunchCompleted;
                _launchSequencerService.SequenceCompleted -= OnSequenceCompleted;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during multi-launch");
            ResultMessage = $"✗ Error: {ex.Message}";
            StatusMessage = "Multi-launch failed. Check logs for details.";
        }
        finally
        {
            // Re-enable progress events
            _gameLaunchService.SuppressProgressEvents = false;

            IsLaunching = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Handles launch progress updates.
    /// </summary>
    private void OnLaunchProgress(object? sender, LaunchProgressEventArgs e)
    {
        LaunchedCount = e.LaunchedCount;
        StatusMessage = e.StatusMessage;

        // Update progress percentage
        if (e.TotalClients > 0)
        {
            ProgressPercentage = (int)((double)e.LaunchedCount / e.TotalClients * 100);
        }
    }

    /// <summary>
    /// Handles individual launch completion.
    /// </summary>
    private void OnLaunchCompleted(object? sender, LaunchCompletedEventArgs e)
    {
        if (e.Success)
        {
            _logger.Debug("Client {TaskNumber}/{TotalTasks} launched successfully",
                e.TaskNumber, e.TotalTasks);
        }
        else
        {
            _logger.Warning("Client {TaskNumber}/{TotalTasks} failed: {Error}",
                e.TaskNumber, e.TotalTasks, e.ErrorMessage);
        }
    }

    /// <summary>
    /// Handles sequence completion.
    /// </summary>
    private void OnSequenceCompleted(object? sender, SequenceCompletedEventArgs e)
    {
        ProgressPercentage = 100;
        _logger.Information("Launch sequence completed: {Success}/{Total} successful",
            e.SuccessCount, e.TotalTasks);
    }

    /// <summary>
    /// Saves the current multi-launch configuration (order and delays).
    /// </summary>
    public async Task SaveConfigurationAsync()
    {
        try
        {
            var config = new MultiLaunchConfiguration
            {
                WorldId = _world.Id,
                Entries = new List<LaunchEntryConfig>()
            };

            foreach (var entry in Entries)
            {
                config.Entries.Add(new LaunchEntryConfig
                {
                    CredentialId = entry.Credential.Id,
                    Order = entry.Order,
                    DelaySeconds = entry.DelaySeconds
                });
            }

            await _multiLaunchConfigService.SaveConfigurationAsync(config);
            _logger.Information("Saved multi-launch configuration for world {WorldId} with {Count} entries",
                _world.Id, config.Entries.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save multi-launch configuration");
        }
    }

    /// <summary>
    /// Cancels the current launch sequence.
    /// </summary>
    [RelayCommand]
    private async Task Cancel()
    {
        if (IsLaunching && _cancellationTokenSource != null)
        {
            _logger.Information("User cancelled multi-launch");
            StatusMessage = "Cancelling...";
            _cancellationTokenSource.Cancel();
        }
        else
        {
            // Save configuration before closing
            await SaveConfigurationAsync();

            // Just close the dialog
            ShouldClose = true;
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
            _logger.Information("Opening multi-client help documentation from MultiLaunchDialog");
            _navigationService.NavigateTo<MultiClientHelpViewModel>();

            // Close the dialog when navigating to help
            ShouldClose = true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening multi-client help");
            StatusMessage = "Failed to open help documentation.";
        }
    }

    /// <summary>
    /// Called when IsLaunching changes.
    /// </summary>
    partial void OnIsLaunchingChanged(bool value)
    {
        LaunchSelectedCommand.NotifyCanExecuteChanged();
    }
}
