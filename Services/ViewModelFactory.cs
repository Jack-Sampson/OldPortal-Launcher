using OPLauncher.ViewModels;

namespace OPLauncher.Services;

/// <summary>
/// Factory for creating ViewModels with their dependencies.
/// Centralizes ViewModel instantiation logic and enables easier testing of navigation flows.
/// </summary>
public class ViewModelFactory : IViewModelFactory
{
    private readonly WorldsService _worldsService;
    private readonly ConfigService _configService;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly ManualServersService _manualServersService;
    private readonly UpdateService _updateService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;
    private readonly FavoritesService _favoritesService;
    private readonly RecentServersService _recentServersService;
    private readonly ServerMonitorService _serverMonitorService;
    private readonly ThemeManager _themeManager;
    private readonly DecalService _decalService;
    private readonly UserPreferencesManager _userPreferencesManager;
    private readonly LoggingService _logger;

    /// <summary>
    /// Initializes a new instance of the ViewModelFactory.
    /// All dependencies are injected by the DI container.
    /// </summary>
    public ViewModelFactory(
        WorldsService worldsService,
        ConfigService configService,
        CredentialVaultService credentialVaultService,
        GameLaunchService gameLaunchService,
        ManualServersService manualServersService,
        UpdateService updateService,
        IFileDialogService fileDialogService,
        INavigationService navigationService,
        FavoritesService favoritesService,
        RecentServersService recentServersService,
        ServerMonitorService serverMonitorService,
        ThemeManager themeManager,
        DecalService decalService,
        UserPreferencesManager userPreferencesManager,
        LoggingService logger)
    {
        _worldsService = worldsService;
        _configService = configService;
        _credentialVaultService = credentialVaultService;
        _gameLaunchService = gameLaunchService;
        _manualServersService = manualServersService;
        _updateService = updateService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        _favoritesService = favoritesService;
        _recentServersService = recentServersService;
        _serverMonitorService = serverMonitorService;
        _themeManager = themeManager;
        _decalService = decalService;
        _userPreferencesManager = userPreferencesManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public WorldsBrowseViewModel CreateWorldsBrowseViewModel(MainWindowViewModel mainWindow)
    {
        return new WorldsBrowseViewModel(
            _worldsService,
            _configService,
            _credentialVaultService,
            _gameLaunchService,
            _navigationService,
            _favoritesService,
            _recentServersService,
            _serverMonitorService,
            _logger,
            mainWindow);
    }

    /// <inheritdoc />
    public SettingsViewModel CreateSettingsViewModel(MainWindowViewModel mainWindow)
    {
        return new SettingsViewModel(
            _configService,
            _updateService,
            _worldsService,
            _logger,
            _fileDialogService,
            _themeManager,
            _decalService,
            _userPreferencesManager,
            _navigationService,
            mainWindow);
    }

    /// <inheritdoc />
    public ManualServersViewModel CreateManualServersViewModel(MainWindowViewModel mainWindow)
    {
        return new ManualServersViewModel(
            _manualServersService,
            _gameLaunchService,
            _credentialVaultService,
            _configService,
            _navigationService,
            _favoritesService,
            _logger,
            mainWindow);
    }

    /// <inheritdoc />
    public FavoritesViewModel CreateFavoritesViewModel(MainWindowViewModel mainWindow)
    {
        return new FavoritesViewModel(
            _favoritesService,
            _worldsService,
            _manualServersService,
            _navigationService,
            _gameLaunchService,
            _credentialVaultService,
            _configService,
            _logger,
            mainWindow);
    }

    /// <inheritdoc />
    public RecentViewModel CreateRecentViewModel(MainWindowViewModel mainWindow)
    {
        return new RecentViewModel(
            _recentServersService,
            _favoritesService,
            _worldsService,
            _manualServersService,
            _navigationService,
            _gameLaunchService,
            _credentialVaultService,
            _configService,
            _logger,
            mainWindow);
    }
}
