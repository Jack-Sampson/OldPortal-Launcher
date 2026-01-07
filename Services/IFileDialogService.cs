using System.Threading.Tasks;

namespace OPLauncher.Services;

/// <summary>
/// Service for showing file dialogs to the user.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows an open file dialog and returns the selected file path, or null if cancelled.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="fileTypeFilters">Optional file type filters (e.g., "*.exe").</param>
    /// <param name="suggestedStartLocation">Optional suggested start location path.</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    Task<string?> ShowOpenFileDialogAsync(
        string title,
        string[]? fileTypeFilters = null,
        string? suggestedStartLocation = null);
}
