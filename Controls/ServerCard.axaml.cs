// TODO: [LAUNCH-107] Phase 1 Week 3 - ServerCard Control Code-Behind
// Component: Launcher
// Module: UI Redesign - Card Grid Layout
// Description: Code-behind for ServerCard user control
//
// NOTE: This file appears "empty" but is REQUIRED for Avalonia partial class compilation.
// All card logic is handled in ServerCardViewModel (MVVM pattern).
// Do not delete this file - Avalonia requires the partial class declaration.

using Avalonia.Controls;

namespace OPLauncher.Controls;

/// <summary>
/// Server card control for displaying world servers and manual servers in card grid layout.
/// This is a minimal code-behind file - all logic is handled by ServerCardViewModel.
/// </summary>
public partial class ServerCard : UserControl
{
    public ServerCard()
    {
        InitializeComponent();
    }
}
