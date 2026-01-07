// TODO: [LAUNCH-096] Phase 1 Week 1 - Updated ThemeManager for New Design System
// Component: OPLauncher
// Module: UI Redesign - Design System
// Description: Service for managing Light/Dark theme switching with persistence

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using OPLauncher.Models;
using System;
using System.Linq;

namespace OPLauncher.Services;

/// <summary>
/// Manages application theme switching between Dark and Light themes.
/// Persists theme preference using ConfigService.
/// </summary>
public class ThemeManager
{
    private readonly LoggingService _logger;
    private readonly ConfigService _configService;
    private AppTheme _currentTheme;

    /// <summary>
    /// Event raised when the theme changes.
    /// </summary>
    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeManager(LoggingService logger, ConfigService configService)
    {
        _logger = logger;
        _configService = configService;

        // Load saved theme preference or default to Dark
        _currentTheme = LoadThemePreference();
        _logger.Information("ThemeManager initialized with theme: {Theme}", _currentTheme);
    }

    /// <summary>
    /// Gets the currently active theme
    /// </summary>
    public AppTheme CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ThemeChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Initializes the theme system by applying the current theme.
    /// Should be called during application startup.
    /// </summary>
    public void Initialize()
    {
        ApplyTheme(CurrentTheme);
        _logger.Debug("Theme system initialized");
    }

    /// <summary>
    /// Switches to the specified theme.
    /// </summary>
    /// <param name="theme">The theme to switch to.</param>
    public void SetTheme(AppTheme theme)
    {
        if (theme == CurrentTheme)
        {
            _logger.Debug("Theme {Theme} is already active, skipping", theme);
            return;
        }

        _logger.Information("Switching theme from {OldTheme} to {NewTheme}", CurrentTheme, theme);

        ApplyTheme(theme);
        CurrentTheme = theme;
        SaveThemePreference(theme);
    }

    /// <summary>
    /// Toggles between Light and Dark themes.
    /// </summary>
    public void ToggleTheme()
    {
        var newTheme = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        SetTheme(newTheme);
    }

    /// <summary>
    /// Applies the specified theme to the application
    /// </summary>
    /// <param name="theme">Theme to apply</param>
    public void ApplyTheme(AppTheme theme)
    {
        try
        {
            _logger.Information("Applying theme: {ThemeName}", theme.ToString());

            var app = Application.Current;
            if (app == null)
            {
                _logger.Error("Cannot apply theme: Application.Current is null");
                return;
            }

            // Get the theme resource dictionary URI
            var themeUri = GetThemeUri(theme);

            // Remove all existing theme resource dictionaries from MergedDictionaries
            // Look for ResourceInclude that reference our theme files
            var existingThemes = app.Resources.MergedDictionaries
                .OfType<ResourceInclude>()
                .Where(r => r.Source?.AbsolutePath?.Contains("/Themes/") == true)
                .ToList();

            foreach (var existingTheme in existingThemes)
            {
                app.Resources.MergedDictionaries.Remove(existingTheme);
            }

            // Add the new theme as a ResourceInclude
            // Use the base URI for the assembly, then set the specific theme as Source
            var newTheme = new ResourceInclude(new Uri("avares://OPLauncher"))
            {
                Source = themeUri
            };

            app.Resources.MergedDictionaries.Insert(0, newTheme); // Insert at beginning to ensure priority

            // Update Avalonia's RequestedThemeVariant to match our custom theme
            // This ensures FluentTheme's System* colors (SystemBaseHighColor, etc.) match the theme
            var themeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Dark
            };

            app.RequestedThemeVariant = themeVariant;
            _logger.Information("Updated RequestedThemeVariant to: {Variant}", themeVariant);

            _logger.Information("Theme applied successfully: {ThemeName} ({Source})", theme, themeUri);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to apply theme: {ThemeName}", theme.ToString());
            throw;
        }
    }

    /// <summary>
    /// Gets the URI for the specified theme
    /// </summary>
    private Uri GetThemeUri(AppTheme theme)
    {
        var themeName = theme switch
        {
            AppTheme.Dark => "DarkTheme.axaml",
            AppTheme.Light => "LightTheme.axaml",
            _ => "DarkTheme.axaml"
        };

        return new Uri($"avares://OPLauncher/Themes/{themeName}");
    }

    /// <summary>
    /// Loads the saved theme preference from configuration.
    /// </summary>
    private AppTheme LoadThemePreference()
    {
        try
        {
            var theme = _configService.Current.Theme;
            _logger.Debug("Loaded theme preference from config: {Theme}", theme);
            return theme;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading theme preference, defaulting to Dark");
            return AppTheme.Dark;
        }
    }

    /// <summary>
    /// Saves the theme preference to configuration.
    /// </summary>
    private void SaveThemePreference(AppTheme theme)
    {
        try
        {
            var config = _configService.Current;
            config.Theme = theme;
            _configService.SaveConfiguration(config);
            _logger.Debug("Saved theme preference: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving theme preference");
        }
    }

    /// <summary>
    /// Gets a display name for the theme
    /// </summary>
    public static string GetThemeDisplayName(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Dark => "Dark",
            AppTheme.Light => "Light",
            _ => "Dark"
        };
    }
}
