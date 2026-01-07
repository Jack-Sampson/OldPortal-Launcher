using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OPLauncher.Services;
using OPLauncher.ViewModels;
using OPLauncher.Views;
using OPLauncher.Utilities;
using System;

namespace OPLauncher;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Configures the dependency injection container with all application services.
    /// Services are registered as singletons to maintain state throughout the application lifetime.
    /// </summary>
    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register core services
        services.AddSingleton<LoggingService>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ThemeManager>();
        services.AddSingleton<DecalService>();
        services.AddSingleton<UserPreferencesManager>();
        services.AddSingleton<IFileDialogService, FileDialogService>();

        // Register navigation service
        services.AddSingleton<INavigationService, NavigationService>();

        // Register HttpClient as a singleton with configured base address and timeout
        services.AddSingleton(serviceProvider =>
        {
            var configService = serviceProvider.GetRequiredService<ConfigService>();
            var httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri(configService.Current.ApiBaseUrl),
                Timeout = TimeSpan.FromSeconds(30) // 30 second timeout for API calls
            };
            return httpClient;
        });

        // Register image caching service for performance optimization
        services.AddSingleton<ImageCacheService>();

        // Register API services (depend on HttpClient)
        services.AddSingleton<WorldsService>();
        services.AddSingleton<UpdateService>();

        // Register server monitoring service (UDP-based real-time status checking)
        services.AddSingleton<ServerMonitorService>();

        // Register centralized database service (required by ManualServers, Favorites, Recent)
        services.AddSingleton<DatabaseService>();

        // Register credential and game launch services
        services.AddSingleton<CredentialVaultService>();
        services.AddSingleton<MultiLaunchConfigService>();
        services.AddSingleton<GameLaunchService>();
        services.AddSingleton<LaunchSequencerService>();
        services.AddSingleton<BatchGroupService>();
        services.AddSingleton<ManualServersService>();

        // Register Favorites and Recent servers services
        services.AddSingleton<FavoritesService>();
        services.AddSingleton<RecentServersService>();

        // Register onboarding-related services
        services.AddSingleton<GameClientService>();
        services.AddSingleton<PatchService>();
        // AgentService removed - onboarding now uses marketing-only Addons step

        // Register ViewModel factory
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();

        // Register Main Shell ViewModel
        services.AddSingleton<MainShellViewModel>();

        // Register navigable ViewModels as Transient (new instance for each navigation)
        services.AddTransient<HomeViewModel>();
        services.AddTransient<NewsViewModel>();
        // WorldsBrowseViewModel is Singleton for performance - keeps data in memory between navigations
        services.AddSingleton<WorldsBrowseViewModel>();
        services.AddTransient<ManualServersViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<WorldDetailViewModel>();
        services.AddTransient<ManualServerDetailViewModel>();

        // Register onboarding ViewModel (Transient for re-run setup)
        services.AddTransient<OnboardingViewModel>();

        // Register help ViewModels (Transient for each help view)
        services.AddTransient<MultiClientHelpViewModel>();
        services.AddTransient<GeneralHelpViewModel>();

        // Keep MainWindowViewModel for backward compatibility / deep link handling
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Configure dependency injection container
            _serviceProvider = ConfigureServices();

            // Resolve core services for startup initialization
            var loggingService = _serviceProvider.GetRequiredService<LoggingService>();
            var configService = _serviceProvider.GetRequiredService<ConfigService>();
            var themeManager = _serviceProvider.GetRequiredService<ThemeManager>();

            // Initialize image cache service for performance optimization
            var imageCacheService = _serviceProvider.GetRequiredService<ImageCacheService>();
            Converters.ImageUriConverter.Initialize(imageCacheService);
            loggingService.Information("ImageCacheService initialized - disk caching enabled for remote images");

            // Apply saved theme on startup
            var savedTheme = configService.Current.Theme;
            themeManager.ApplyTheme(savedTheme);

            // Start server monitoring service for real-time UDP status checks
            var serverMonitorService = _serviceProvider.GetRequiredService<ServerMonitorService>();
            serverMonitorService.Start(checkIntervalSeconds: 15);
            loggingService.Information("ServerMonitorService started with 15s check interval");

            // Resolve MainShellViewModel from DI container (new navigation system)
            var mainShellViewModel = _serviceProvider.GetRequiredService<MainShellViewModel>();

            // Handle deep link if present
            if (Program.PendingDeepLink != null)
            {
                loggingService.Information("Processing pending deep link for server {ServerId}", Program.PendingDeepLink.ServerId);
                // Fire and forget - HandleDeepLink is async but we can't await here
                _ = mainShellViewModel.HandleDeepLink(Program.PendingDeepLink);
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainShellViewModel,
            };

            // Subscribe to deep links from other instances
            if (Program.SingleInstanceManager != null)
            {
                Program.SingleInstanceManager.DeepLinkReceived += (sender, deepLinkUri) =>
                {
                    // Handle deep link on UI thread
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        loggingService.Information("Received deep link from another instance: {DeepLink}", deepLinkUri);

                        // Parse the deep link
                        if (DeepLinkParser.TryParseLaunchUri(deepLinkUri, out Guid serverId))
                        {
                            var deepLink = new Models.DeepLinkInfo(serverId, deepLinkUri);
                            _ = mainShellViewModel.HandleDeepLink(deepLink); // Fire and forget - runs on UI thread
                        }

                        // Activate and bring window to foreground
                        if (desktop.MainWindow != null)
                        {
                            desktop.MainWindow.Activate();
                            desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                            desktop.MainWindow.Topmost = true;
                            desktop.MainWindow.Topmost = false; // Reset to allow other windows on top
                        }
                    });
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}