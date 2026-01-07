using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OPLauncher.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        // Prevent automatic scrolling when elements become visible
        this.AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Prevents automatic scrolling when status messages or other elements request to be brought into view.
    /// This ensures the scroll position stays where the user left it when clicking buttons.
    /// </summary>
    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        // Cancel the bring-into-view request to prevent automatic scrolling
        e.Handled = true;
    }
}
