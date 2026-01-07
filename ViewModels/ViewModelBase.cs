// TODO: [LAUNCH-100] Phase 1 Week 2 - Enhanced ViewModelBase with Navigation Support
// Component: Launcher
// Module: UI Redesign - Navigation Architecture
// Description: Base class for all ViewModels with navigation lifecycle and common properties

using CommunityToolkit.Mvvm.ComponentModel;

namespace OPLauncher.ViewModels;

/// <summary>
/// Base class for all ViewModels in the launcher.
/// Provides navigation lifecycle methods and common properties for UI state management.
/// </summary>
public abstract partial class ViewModelBase : ObservableValidator
{
    /// <summary>
    /// Gets or sets whether the view model is busy performing an operation.
    /// Can be used to display loading indicators in the UI.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Gets or sets an error message to display in the UI.
    /// Set to null to clear the error.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Gets or sets a success message to display in the UI.
    /// Set to null to clear the message.
    /// </summary>
    [ObservableProperty]
    private string? _successMessage;

    /// <summary>
    /// Called when this view model is navigated to.
    /// Override this method to perform initialization logic when the view appears.
    /// </summary>
    /// <param name="parameter">Optional parameter passed from the navigation source.</param>
    public virtual void OnNavigatedTo(object? parameter)
    {
        // Base implementation does nothing
        // Override in derived classes to handle navigation
    }

    /// <summary>
    /// Called when this view model is navigated away from.
    /// Override this method to perform cleanup logic when the view disappears.
    /// </summary>
    public virtual void OnNavigatedFrom()
    {
        // Base implementation does nothing
        // Override in derived classes to handle cleanup
    }

    /// <summary>
    /// Clears all messages (error and success).
    /// </summary>
    protected void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }

    /// <summary>
    /// Sets an error message and clears any success message.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    protected void SetError(string message)
    {
        ErrorMessage = message;
        SuccessMessage = null;
    }

    /// <summary>
    /// Sets a success message and clears any error message.
    /// </summary>
    /// <param name="message">The success message to display.</param>
    protected void SetSuccess(string message)
    {
        SuccessMessage = message;
        ErrorMessage = null;
    }
}
