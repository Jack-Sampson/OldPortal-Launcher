using Avalonia.Controls;
using Avalonia.Interactivity;
using OPLauncher.ViewModels;

namespace OPLauncher.Views;

/// <summary>
/// Multi-client launch dialog window.
/// Allows users to launch multiple game clients sequentially from a single server.
/// </summary>
public partial class MultiLaunchDialog : Window
{
    public MultiLaunchDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Called when the window is initialized.
    /// Subscribes to ViewModel events for auto-close behavior.
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Subscribe to ViewModel's ShouldClose property
        if (DataContext is MultiLaunchDialogViewModel viewModel)
        {
            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(MultiLaunchDialogViewModel.ShouldClose) &&
                    viewModel.ShouldClose)
                {
                    Close();
                }
            };
        }
    }
}
