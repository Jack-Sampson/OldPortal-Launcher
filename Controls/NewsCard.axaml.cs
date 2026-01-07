// TODO: [LAUNCH-111] Phase 2 Week 4 - NewsCard Control Code-Behind
// Component: Launcher
// Module: UI Redesign - Home View & News Feed
// Description: Code-behind for NewsCard user control with click handling to open URLs

using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using OPLauncher.DTOs;
using Serilog;

namespace OPLauncher.Controls;

/// <summary>
/// News card control for displaying news items in the news feed.
/// Clicking the card opens the news article URL in the default browser.
/// </summary>
public partial class NewsCard : UserControl
{
    public NewsCard()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles card click to open the news article URL in the default browser.
    /// </summary>
    private void OnCardClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not NewsDto newsItem)
            return;

        if (string.IsNullOrWhiteSpace(newsItem.Url))
            return;

        try
        {
            // Open URL in default browser
            // Use ProcessStartInfo with UseShellExecute = true for cross-platform compatibility
            var psi = new ProcessStartInfo
            {
                FileName = newsItem.Url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open news URL: {Url}", newsItem.Url);
            Debug.WriteLine($"Failed to open news URL: {newsItem.Url} - {ex.Message}");
        }
    }
}
