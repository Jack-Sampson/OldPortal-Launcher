using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform;

namespace OPLauncher.Views;

public partial class MainWindow : Window
{
    private TrayIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();

        // Handle window closing to minimize to tray instead
        Closing += MainWindow_Closing;

        // Handle window closed to clean up tray icon
        Closed += MainWindow_Closed;
    }

    /// <summary>
    /// Initialize the system tray icon programmatically
    /// </summary>
    private void InitializeTrayIcon()
    {
        // Create tray icon
        _trayIcon = new TrayIcon();

        // Load the icon - Windows will automatically scale to appropriate tray size
        try
        {
            using var iconStream = AssetLoader.Open(new Uri("avares://OldPortal.Launcher/Assets/oldportal-icon (128x128).ico"));
            _trayIcon.Icon = new WindowIcon(iconStream);
        }
        catch
        {
            // Icon loading failed, continue without icon
        }

        _trayIcon.ToolTipText = "Old Portal Launcher";

        // Create context menu
        var menu = new NativeMenu();

        var openItem = new NativeMenuItem("Open");
        openItem.Click += OpenMenuItem_Click;
        menu.Add(openItem);

        menu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += ExitMenuItem_Click;
        menu.Add(exitItem);

        _trayIcon.Menu = menu;

        // Handle tray icon click (double-click on Windows)
        _trayIcon.Clicked += TrayIcon_Clicked;

        // Show the tray icon
        _trayIcon.IsVisible = true;
    }

    /// <summary>
    /// Handle tray icon click (double-click on Windows) to restore window
    /// </summary>
    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        ShowWindow();
    }

    /// <summary>
    /// Handle "Open" menu item click to restore window
    /// </summary>
    private void OpenMenuItem_Click(object? sender, EventArgs e)
    {
        ShowWindow();
    }

    /// <summary>
    /// Handle "Exit" menu item click to close application
    /// </summary>
    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Handle window closing event - clean up tray icon
    /// </summary>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Clean up tray icon when closing
        CleanupTrayIcon();
    }

    /// <summary>
    /// Handle window closed event - ensure tray icon is cleaned up
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        // Ensure tray icon is cleaned up when window is closed
        CleanupTrayIcon();
    }

    /// <summary>
    /// Properly dispose of the tray icon to prevent ghost icons
    /// </summary>
    private void CleanupTrayIcon()
    {
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    /// <summary>
    /// Show and restore the main window
    /// </summary>
    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// Hide the main window (minimize to tray)
    /// </summary>
    private void HideWindow()
    {
        Hide();
    }
}