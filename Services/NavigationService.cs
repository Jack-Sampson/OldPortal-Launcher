// TODO: [LAUNCH-099] Phase 1 Week 2 - NavigationService Implementation
// Component: Launcher
// Module: UI Redesign - Navigation Architecture
// Description: Service for managing view navigation with history stack

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using OPLauncher.ViewModels;

namespace OPLauncher.Services;

/// <summary>
/// Service responsible for managing navigation between views.
/// Maintains a navigation history stack for back navigation support.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LoggingService _logger;
    private readonly Stack<ViewModelBase> _navigationStack = new();
    private ViewModelBase? _currentViewModel;

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    public event EventHandler<ViewModelBase>? Navigated;

    /// <summary>
    /// Gets the current view model being displayed.
    /// </summary>
    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (_currentViewModel != value)
            {
                _currentViewModel = value;
                if (value != null)
                {
                    Navigated?.Invoke(this, value);
                }
            }
        }
    }

    /// <summary>
    /// Gets whether the navigation service can navigate back.
    /// </summary>
    public bool CanGoBack => _navigationStack.Count > 0;

    /// <summary>
    /// Initializes a new instance of the NavigationService.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving view model dependencies.</param>
    /// <param name="logger">Logging service for diagnostic output.</param>
    public NavigationService(IServiceProvider serviceProvider, LoggingService logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _logger.Debug("NavigationService initialized");
    }

    /// <summary>
    /// Navigates to the specified view model type.
    /// </summary>
    /// <typeparam name="TViewModel">The type of view model to navigate to.</typeparam>
    /// <param name="parameter">Optional parameter to pass to the view model.</param>
    public void NavigateTo<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase
    {
        try
        {
            _logger.Information("Navigating to {ViewModelType}", typeof(TViewModel).Name);

            // Create view model instance using DI
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();

            // Push current view model to stack before navigating
            if (CurrentViewModel != null)
            {
                _logger.Debug("Pushing {PreviousViewModel} to navigation stack", CurrentViewModel.GetType().Name);

                // Call OnNavigatedFrom on current view model
                CurrentViewModel.OnNavigatedFrom();

                _navigationStack.Push(CurrentViewModel);
            }

            // Set new view model as current
            CurrentViewModel = viewModel;

            // Call OnNavigatedTo on new view model with parameter
            viewModel.OnNavigatedTo(parameter);

            _logger.Information("Navigation to {ViewModelType} completed", typeof(TViewModel).Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error navigating to {ViewModelType}", typeof(TViewModel).Name);
            throw;
        }
    }

    /// <summary>
    /// Navigates to the specified view model instance.
    /// </summary>
    /// <param name="viewModel">The view model instance to navigate to.</param>
    public void NavigateTo(ViewModelBase viewModel)
    {
        try
        {
            _logger.Information("Navigating to {ViewModelType} (instance)", viewModel.GetType().Name);

            // Push current view model to stack before navigating
            if (CurrentViewModel != null)
            {
                _logger.Debug("Pushing {PreviousViewModel} to navigation stack", CurrentViewModel.GetType().Name);

                // Call OnNavigatedFrom on current view model
                CurrentViewModel.OnNavigatedFrom();

                _navigationStack.Push(CurrentViewModel);
            }

            // Set new view model as current
            CurrentViewModel = viewModel;

            // Call OnNavigatedTo on new view model
            viewModel.OnNavigatedTo(null);

            _logger.Information("Navigation to {ViewModelType} completed", viewModel.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error navigating to {ViewModelType}", viewModel.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Navigates back to the previous view model if possible.
    /// </summary>
    public void GoBack()
    {
        if (!CanGoBack)
        {
            _logger.Warning("Cannot navigate back - navigation stack is empty");
            return;
        }

        try
        {
            _logger.Information("Navigating back");

            // Call OnNavigatedFrom on current view model
            if (CurrentViewModel != null)
            {
                CurrentViewModel.OnNavigatedFrom();
            }

            // Pop previous view model from stack
            var previousViewModel = _navigationStack.Pop();
            _logger.Debug("Popped {ViewModelType} from navigation stack", previousViewModel.GetType().Name);

            // Set previous view model as current
            CurrentViewModel = previousViewModel;

            // Call OnNavigatedTo on previous view model
            previousViewModel.OnNavigatedTo(null);

            _logger.Information("Back navigation to {ViewModelType} completed", previousViewModel.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error navigating back");
            throw;
        }
    }

    /// <summary>
    /// Clears the navigation history.
    /// </summary>
    public void ClearHistory()
    {
        _logger.Information("Clearing navigation history ({Count} items)", _navigationStack.Count);
        _navigationStack.Clear();
    }
}
