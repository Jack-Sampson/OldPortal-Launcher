using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using OPLauncher.Services;

namespace OPLauncher.ViewModels;

/// <summary>
/// ViewModel for the General Help view.
/// Provides comprehensive help documentation for all launcher features.
/// Supports deep-linking to specific help sections via navigation parameters.
/// </summary>
public partial class GeneralHelpViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly LoggingService _logger;

    public GeneralHelpViewModel(
        INavigationService navigationService,
        LoggingService logger)
    {
        _navigationService = navigationService;
        _logger = logger;
    }

    /// <summary>
    /// Called when navigated to this view.
    /// Supports deep-linking to specific sections via string parameter (e.g., "getting-started").
    /// </summary>
    public override void OnNavigatedTo(object? parameter)
    {
        if (parameter is string section)
        {
            _logger.Information("Navigated to help section: {Section}", section);
            // Section scrolling would be handled by the view if needed
        }
        else
        {
            _logger.Information("Navigated to General Help");
        }
    }

    /// <summary>
    /// Opens an external URL in the default browser.
    /// Used for Discord invites, GitHub links, website links, etc.
    /// </summary>
    [RelayCommand]
    private void OpenExternalLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            _logger.Information("Opened external link: {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open external link: {Url}", url);
        }
    }

    /// <summary>
    /// Navigates to the dedicated Multi-Client Help view.
    /// </summary>
    [RelayCommand]
    private void OpenMultiClientHelp()
    {
        try
        {
            _navigationService.NavigateTo<MultiClientHelpViewModel>();
            _logger.Information("Navigated to Multi-Client Help");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to navigate to Multi-Client Help");
        }
    }
}
