using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace OPLauncher.Services;

/// <summary>
/// Implementation of IFileDialogService using Avalonia's Storage Provider API.
/// </summary>
public class FileDialogService : IFileDialogService
{
    private readonly LoggingService _logger;

    public FileDialogService(LoggingService logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Shows an open file dialog and returns the selected file path, or null if cancelled.
    /// </summary>
    public async Task<string?> ShowOpenFileDialogAsync(
        string title,
        string[]? fileTypeFilters = null,
        string? suggestedStartLocation = null)
    {
        try
        {
            // Get the main window from the application
            var topLevel = GetTopLevel();
            if (topLevel == null)
            {
                _logger.Warning("Could not get TopLevel for file dialog");
                return null;
            }

            // Build file type filters
            var filePickerFileTypes = new List<FilePickerFileType>();

            if (fileTypeFilters != null && fileTypeFilters.Length > 0)
            {
                // Create filter for each extension
                foreach (var filter in fileTypeFilters)
                {
                    var extension = filter.Replace("*", "").Replace(".", "").Trim();
                    filePickerFileTypes.Add(new FilePickerFileType($"{extension.ToUpper()} Files")
                    {
                        Patterns = new[] { filter }
                    });
                }
            }

            // Add "All Files" option
            filePickerFileTypes.Add(new FilePickerFileType("All Files")
            {
                Patterns = new[] { "*.*" }
            });

            // Build options
            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = filePickerFileTypes
            };

            // Set suggested start location if provided
            if (!string.IsNullOrWhiteSpace(suggestedStartLocation))
            {
                try
                {
                    var startPath = Path.GetDirectoryName(suggestedStartLocation);
                    if (!string.IsNullOrEmpty(startPath) && Directory.Exists(startPath))
                    {
                        var storageFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(startPath);
                        if (storageFolder != null)
                        {
                            options.SuggestedStartLocation = storageFolder;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Could not set suggested start location for file dialog");
                }
            }

            // Show the file picker
            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);

            if (result != null && result.Count > 0)
            {
                var selectedFile = result[0];
                var path = selectedFile.TryGetLocalPath();

                _logger.Debug("File selected from dialog: {Path}", path ?? "null");
                return path;
            }

            _logger.Debug("File dialog cancelled by user");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error showing open file dialog");
            return null;
        }
    }

    /// <summary>
    /// Gets the TopLevel (Window) from the current application.
    /// </summary>
    private TopLevel? GetTopLevel()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting TopLevel");
            return null;
        }
    }
}
