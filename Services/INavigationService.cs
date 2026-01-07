// TODO: [LAUNCH-098] Phase 1 Week 2 - INavigationService Interface
// Component: Launcher
// Module: UI Redesign - Navigation Architecture
// Description: Interface for view navigation with parameter support

using System;
using OPLauncher.ViewModels;

namespace OPLauncher.Services;

/// <summary>
/// Interface for managing navigation between views in the application.
/// Supports forward navigation, back navigation, and parameter passing.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    event EventHandler<ViewModelBase>? Navigated;

    /// <summary>
    /// Gets the current view model being displayed.
    /// </summary>
    ViewModelBase? CurrentViewModel { get; }

    /// <summary>
    /// Gets whether the navigation service can navigate back.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Navigates to the specified view model type.
    /// </summary>
    /// <typeparam name="TViewModel">The type of view model to navigate to.</typeparam>
    /// <param name="parameter">Optional parameter to pass to the view model.</param>
    void NavigateTo<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;

    /// <summary>
    /// Navigates to the specified view model instance.
    /// </summary>
    /// <param name="viewModel">The view model instance to navigate to.</param>
    void NavigateTo(ViewModelBase viewModel);

    /// <summary>
    /// Navigates back to the previous view model if possible.
    /// </summary>
    void GoBack();

    /// <summary>
    /// Clears the navigation history.
    /// </summary>
    void ClearHistory();
}
