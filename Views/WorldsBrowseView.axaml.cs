using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OPLauncher.Views;

public partial class WorldsBrowseView : UserControl
{
    public WorldsBrowseView()
    {
        InitializeComponent();

        // Add keyboard handler for Ctrl+F to focus search
        this.KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+F: Focus search box
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            SearchTextBox?.Focus();
            e.Handled = true;
        }
    }
}
