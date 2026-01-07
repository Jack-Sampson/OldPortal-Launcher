using OPLauncher.ViewModels;

namespace OPLauncher.Services;

/// <summary>
/// Factory interface for creating ViewModels with their dependencies.
/// Decouples navigation logic from ViewModel instantiation, improving testability.
/// </summary>
public interface IViewModelFactory
{
    /// <summary>
    /// Creates a new instance of WorldsBrowseViewModel.
    /// </summary>
    /// <param name="mainWindow">The main window view model for navigation.</param>
    /// <returns>A configured WorldsBrowseViewModel instance.</returns>
    WorldsBrowseViewModel CreateWorldsBrowseViewModel(MainWindowViewModel mainWindow);

    /// <summary>
    /// Creates a new instance of SettingsViewModel.
    /// </summary>
    /// <param name="mainWindow">The main window view model for navigation.</param>
    /// <returns>A configured SettingsViewModel instance.</returns>
    SettingsViewModel CreateSettingsViewModel(MainWindowViewModel mainWindow);

    /// <summary>
    /// Creates a new instance of ManualServersViewModel.
    /// </summary>
    /// <param name="mainWindow">The main window view model for navigation.</param>
    /// <returns>A configured ManualServersViewModel instance.</returns>
    ManualServersViewModel CreateManualServersViewModel(MainWindowViewModel mainWindow);

    /// <summary>
    /// Creates a new instance of FavoritesViewModel.
    /// </summary>
    /// <param name="mainWindow">The main window view model for navigation.</param>
    /// <returns>A configured FavoritesViewModel instance.</returns>
    FavoritesViewModel CreateFavoritesViewModel(MainWindowViewModel mainWindow);

    /// <summary>
    /// Creates a new instance of RecentViewModel.
    /// </summary>
    /// <param name="mainWindow">The main window view model for navigation.</param>
    /// <returns>A configured RecentViewModel instance.</returns>
    RecentViewModel CreateRecentViewModel(MainWindowViewModel mainWindow);
}
