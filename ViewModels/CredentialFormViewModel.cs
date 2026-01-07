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
/// Reusable ViewModel for managing AC account credentials.
/// This component encapsulates all credential form logic (add, edit, delete, list)
/// and can be composed into parent ViewModels to eliminate duplication.
///
/// USAGE PATTERN:
/// Parent ViewModels (WorldDetailViewModel, ManualServerDetailViewModel) create an instance
/// of this ViewModel and expose it as a property. The parent view binds to the nested
/// CredentialForm property to render the credential management UI.
///
/// DESIGN RATIONALE:
/// - COMPOSITION OVER INHERITANCE: Avoids creating a complex base class hierarchy
/// - SINGLE RESPONSIBILITY: This ViewModel has one job - manage credentials
/// - REUSABILITY: Same component works for worlds, manual servers, or future use cases
/// - TESTABILITY: Credential logic can be tested independently from parent ViewModels
///
/// COMMUNICATION WITH PARENT:
/// - Parent passes worldId (int) in constructor - used as the vault storage key
/// - Parent subscribes to MessageChanged event to display success/error messages in their UI
/// - Parent can directly access SavedCredentials collection to get selected credential for launch
///
/// See also: WorldDetailViewModel, ManualServerDetailViewModel (consumers of this component)
/// </summary>
public partial class CredentialFormViewModel : ViewModelBase
{
    private readonly int _worldId;
    private readonly CredentialVaultService _credentialVaultService;
    private readonly LoggingService _logger;

    /// <summary>
    /// Collection of saved credentials for the associated world/server.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SavedCredential> _savedCredentials = new();

    /// <summary>
    /// The selected credential (typically the most recently used).
    /// </summary>
    [ObservableProperty]
    private SavedCredential? _selectedCredential;

    /// <summary>
    /// Whether to show the add credential form (expandable section).
    /// </summary>
    [ObservableProperty]
    private bool _showAddCredential;

    /// <summary>
    /// Whether credentials are being loaded from the vault.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingCredentials;

    /// <summary>
    /// Username for the new credential being added.
    /// </summary>
    [ObservableProperty]
    private string _newUsername = string.Empty;

    /// <summary>
    /// Password for the new credential being added.
    /// </summary>
    [ObservableProperty]
    private string _newPassword = string.Empty;

    /// <summary>
    /// Display name for the new credential being added (optional).
    /// </summary>
    [ObservableProperty]
    private string? _newDisplayName;

    /// <summary>
    /// Whether a credential is being saved.
    /// </summary>
    [ObservableProperty]
    private bool _isSavingCredential;

    /// <summary>
    /// Message to display to the user (success or error).
    /// Parent ViewModel should bind to this or subscribe to MessageChanged event.
    /// </summary>
    [ObservableProperty]
    private string? _message;

    /// <summary>
    /// Whether the current message is an error (true) or success (false).
    /// </summary>
    [ObservableProperty]
    private bool _isErrorMessage;

    /// <summary>
    /// Whether the password should be visible (true) or hidden with bullets (false).
    /// </summary>
    [ObservableProperty]
    private bool _isPasswordVisible;

    /// <summary>
    /// Whether we're editing an existing credential (true) or adding a new one (false).
    /// </summary>
    [ObservableProperty]
    private bool _isEditingMode;

    /// <summary>
    /// The credential being edited (null if adding new credential).
    /// </summary>
    private SavedCredential? _editingCredential;

    /// <summary>
    /// Event raised when a message is set (success or error).
    /// Parent ViewModel can subscribe to this to display messages in their own UI.
    /// </summary>
    public event EventHandler<MessageEventArgs>? MessageChanged;

    /// <summary>
    /// Initializes a new instance of the CredentialFormViewModel.
    /// </summary>
    /// <param name="worldId">The world/server ID for credential storage.</param>
    /// <param name="credentialVaultService">The credential vault service.</param>
    /// <param name="logger">The logging service.</param>
    public CredentialFormViewModel(
        int worldId,
        CredentialVaultService credentialVaultService,
        LoggingService logger)
    {
        _worldId = worldId;
        _credentialVaultService = credentialVaultService;
        _logger = logger;

        _logger.Debug("CredentialFormViewModel initialized for worldId: {WorldId}", worldId);

        // Load credentials asynchronously
        _ = LoadCredentialsAsync();
    }

    /// <summary>
    /// Shows the add credential form.
    /// </summary>
    [RelayCommand]
    private void AddCredential()
    {
        _logger.Debug("User initiated add credential for worldId: {WorldId}", _worldId);
        // Clear form fields
        NewUsername = string.Empty;
        NewPassword = string.Empty;
        NewDisplayName = null;
        IsPasswordVisible = false;
        IsEditingMode = false;
        _editingCredential = null;
        ClearMessage();
        ShowAddCredential = true;
    }

    /// <summary>
    /// Shows the edit credential form with the selected credential's data.
    /// </summary>
    [RelayCommand]
    private void EditCredential(SavedCredential? credential)
    {
        if (credential == null)
        {
            _logger.Warning("EditCredential called with null credential");
            return;
        }

        _logger.Debug("User initiated edit credential: {Username} for worldId: {WorldId}",
            credential.Username, _worldId);

        // Load credential data into form
        _editingCredential = credential;
        NewUsername = credential.Username;
        NewPassword = string.Empty; // Don't show existing password for security
        NewDisplayName = credential.DisplayName;
        IsPasswordVisible = false;
        IsEditingMode = true;
        ClearMessage();
        ShowAddCredential = true;
    }

    /// <summary>
    /// Toggles password visibility (show/hide).
    /// </summary>
    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
        _logger.Debug("Password visibility toggled: {IsVisible}", IsPasswordVisible);
    }

    /// <summary>
    /// Saves the credential to the vault (new or updated).
    /// </summary>
    [RelayCommand]
    private async Task SaveCredentialAsync()
    {
        try
        {
            ClearMessage();

            // Validate inputs
            if (string.IsNullOrWhiteSpace(NewUsername))
            {
                SetErrorMessage("Username is required.");
                return;
            }

            // Password is always required
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                SetErrorMessage(IsEditingMode
                    ? "Password is required. Re-enter your password to update this credential."
                    : "Password is required.");
                return;
            }

            IsSavingCredential = true;

            var username = NewUsername.Trim();
            var password = NewPassword;
            var displayName = string.IsNullOrWhiteSpace(NewDisplayName) ? null : NewDisplayName.Trim();

            if (IsEditingMode)
            {
                // EDIT MODE: Update existing credential
                _logger.Information("Updating credential for worldId {WorldId}: {Username}", _worldId, username);

                // Delete old credential if username changed
                if (_editingCredential != null && _editingCredential.Username != username)
                {
                    await _credentialVaultService.DeleteCredentialAsync(_worldId, _editingCredential.Username);
                }

                // Save updated credential
                var success = await _credentialVaultService.SaveCredentialAsync(_worldId, username, password, displayName);

                if (success)
                {
                    SetSuccessMessage($"Credential '{displayName ?? username}' updated successfully.");
                    _logger.Information("Credential updated successfully");
                }
                else
                {
                    SetErrorMessage("Failed to update credential. Please try again.");
                    _logger.Warning("Failed to update credential");
                }
            }
            else
            {
                // ADD MODE: Create new credential
                _logger.Information("Saving new credential for worldId {WorldId}: {Username}", _worldId, NewUsername);

                var success = await _credentialVaultService.SaveCredentialAsync(_worldId, username, password, displayName);

                if (success)
                {
                    SetSuccessMessage($"Credential '{displayName ?? username}' saved successfully.");
                    _logger.Information("Credential saved successfully");
                }
                else
                {
                    SetErrorMessage("Failed to save credential. Please try again.");
                    _logger.Warning("Failed to save credential");
                }
            }

            // Reload credentials to show the updated list
            await LoadCredentialsAsync();

            // Clear form fields
            NewUsername = string.Empty;
            NewPassword = string.Empty;
            NewDisplayName = null;
            IsPasswordVisible = false;
            _editingCredential = null;
            IsEditingMode = false;

            // Keep form open for 2 seconds so user can see success message
            await Task.Delay(2000);

            // Close form
            ShowAddCredential = false;
            ClearMessage();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving credential");
            SetErrorMessage($"Error saving credential: {ex.Message}");
        }
        finally
        {
            IsSavingCredential = false;
        }
    }

    /// <summary>
    /// Cancels adding/editing a credential and closes the form.
    /// </summary>
    [RelayCommand]
    private void CancelAddCredential()
    {
        _logger.Debug("User cancelled {Mode} credential", IsEditingMode ? "edit" : "add");
        ShowAddCredential = false;
        NewUsername = string.Empty;
        NewPassword = string.Empty;
        NewDisplayName = null;
        IsPasswordVisible = false;
        IsEditingMode = false;
        _editingCredential = null;
        ClearMessage();
    }

    /// <summary>
    /// Deletes a saved credential.
    /// </summary>
    /// <param name="credential">The credential to delete.</param>
    [RelayCommand]
    private async Task DeleteCredentialAsync(SavedCredential? credential)
    {
        if (credential == null)
        {
            _logger.Warning("DeleteCredential called with null credential");
            return;
        }

        try
        {
            _logger.Information("User deleting credential: {Username} for worldId {WorldId}",
                credential.Username, _worldId);

            var success = await _credentialVaultService.DeleteCredentialAsync(_worldId, credential.Username);

            if (success)
            {
                SavedCredentials.Remove(credential);
                SetSuccessMessage($"Credential '{credential.DisplayName ?? credential.Username}' deleted successfully.");
                _logger.Information("Credential deleted successfully");

                // Clear selection if this was the selected credential
                if (SelectedCredential == credential)
                {
                    SelectedCredential = null;
                }
            }
            else
            {
                SetErrorMessage("Failed to delete credential.");
                _logger.Warning("Failed to delete credential");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting credential");
            SetErrorMessage($"Error deleting credential: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads saved credentials for this world/server.
    /// Public so parent ViewModel can refresh credentials after game launch.
    /// </summary>
    public async Task LoadCredentialsAsync()
    {
        try
        {
            IsLoadingCredentials = true;
            _logger.Debug("Loading credentials for worldId {WorldId}", _worldId);

            var credentials = await _credentialVaultService.GetCredentialsForWorldAsync(_worldId);

            SavedCredentials.Clear();
            foreach (var credential in credentials)
            {
                SavedCredentials.Add(credential);
            }

            // Manually notify that SavedCredentials changed (collection modifications don't trigger PropertyChanged)
            OnPropertyChanged(nameof(SavedCredentials));

            _logger.Information("Loaded {Count} credential(s) for worldId {WorldId}", credentials.Count, _worldId);

            // Auto-select the most recently used credential
            if (SavedCredentials.Count > 0 && SelectedCredential == null)
            {
                SelectedCredential = SavedCredentials.First();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading credentials for worldId {WorldId}", _worldId);
            SetErrorMessage("Failed to load saved credentials.");
        }
        finally
        {
            IsLoadingCredentials = false;
        }
    }

    /// <summary>
    /// Clears the current message.
    /// </summary>
    private void ClearMessage()
    {
        Message = null;
        IsErrorMessage = false;
    }

    /// <summary>
    /// Sets an error message.
    /// </summary>
    private void SetErrorMessage(string message)
    {
        Message = message;
        IsErrorMessage = true;
        MessageChanged?.Invoke(this, new MessageEventArgs(message, true));
    }

    /// <summary>
    /// Sets a success message.
    /// </summary>
    private void SetSuccessMessage(string message)
    {
        Message = message;
        IsErrorMessage = false;
        MessageChanged?.Invoke(this, new MessageEventArgs(message, false));
    }
}

/// <summary>
/// Event arguments for message changes.
/// </summary>
public class MessageEventArgs : EventArgs
{
    public string Message { get; }
    public bool IsError { get; }

    public MessageEventArgs(string message, bool isError)
    {
        Message = message;
        IsError = isError;
    }
}
