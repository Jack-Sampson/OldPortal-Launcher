# OldPortal Deep Link Integration

## Overview

The OldPortal Launcher supports deep links, allowing users to launch the application and navigate directly to a specific world from a web browser, email, or other applications.

## Deep Link Format

```
oldportal://launch/{serverId}
```

**Example:** `oldportal://launch/550e8400-e29b-41d4-a716-446655440000`

This deep link will:
1. Launch the OldPortal Launcher (if not already running)
2. Navigate to the world detail page for the specified server ID (Guid)
3. If the user is not logged in, they will be prompted to log in first, then automatically navigated to the world

## Installation

### Option 1: Manual Registry Import (Easiest)

1. Locate `register-protocol.reg` in the launcher installation directory
2. Double-click the file
3. Click "Yes" when prompted to add the information to the registry
4. The protocol is now registered!

### Option 2: PowerShell Script (Recommended for Advanced Users)

**Run as Administrator:**

```powershell
.\Register-OldPortalProtocol.ps1
```

Or specify a custom launcher path:

```powershell
.\Register-OldPortalProtocol.ps1 -LauncherPath "C:\Custom\Path\OldPortal.Launcher.exe"
```

### Option 3: Automatic Registration (Squirrel Installer)

If you installed the launcher using the official installer, the deep link protocol should be registered automatically during installation.

## Uninstallation

To remove the `oldportal://` protocol registration:

**Run as Administrator:**

```powershell
.\Unregister-OldPortalProtocol.ps1
```

## Testing

### From Command Line

```cmd
start oldportal://launch/550e8400-e29b-41d4-a716-446655440000
```

### From Web Browser

Create an HTML file with a link:

```html
<a href="oldportal://launch/550e8400-e29b-41d4-a716-446655440000">Launch OldPortal Server</a>
```

Or navigate directly in the address bar:

```
oldportal://launch/550e8400-e29b-41d4-a716-446655440000
```

### From PowerShell

```powershell
Start-Process "oldportal://launch/550e8400-e29b-41d4-a716-446655440000"
```

## Usage Scenarios

### Website Integration

OldPortal.com can link directly to worlds:

```html
<a href="oldportal://launch/{{serverId}}" class="launch-button">
  Launch with OldPortal Launcher
</a>
```

### Discord/Forum Posts

Users can share deep links in Discord or forums:

```
Check out this awesome server: oldportal://launch/550e8400-e29b-41d4-a716-446655440000
```

### Email Campaigns

Server owners can email players with direct launch links:

```
Join us now: oldportal://launch/550e8400-e29b-41d4-a716-446655440000
```

## Behavior

### Launcher Not Running

1. Windows launches `OldPortal.Launcher.exe` with the deep link as a command line argument
2. Launcher parses the deep link and extracts the server ID (Guid)
3. Launcher fetches the server details from the API using the server ID
4. If user is authenticated, navigates directly to world detail
5. If user is not authenticated, shows login screen, then navigates after login

### Launcher Already Running

1. Windows sends the deep link to the already-running launcher instance
2. Launcher brings itself to the foreground
3. Fetches the server details from the API using the server ID
4. Navigates to the specified world detail page

**Note:** Single-instance detection is implemented. If the launcher is already running, the deep link is sent to the existing instance via IPC.

## Security

- All server IDs are validated to ensure they are valid GUIDs
- Deep links are sanitized to prevent injection attacks
- Invalid or malformed deep links are safely rejected and logged
- Server IDs are fetched from the API to ensure they exist before navigation

## Troubleshooting

### Deep link doesn't work

1. Verify the protocol is registered:
   ```powershell
   Get-ItemProperty "HKLM:\SOFTWARE\Classes\oldportal\shell\open\command"
   ```

2. Check the launcher path in the registry matches your installation
3. Run the registration script again as Administrator

### Multiple launcher instances open

This is a known limitation. Only one launcher instance should be run at a time for best results. Future versions will implement single-instance enforcement.

### "This app can't run on your PC" error

The launcher path in the registry may be incorrect. Re-run the registration script with the correct path:

```powershell
.\Register-OldPortalProtocol.ps1 -LauncherPath "C:\Correct\Path\OldPortal.Launcher.exe"
```

## Development

### Code Structure

- **DeepLinkParser** (`Utilities/DeepLinkParser.cs`): Parses and validates deep link URIs
- **Program.cs**: Detects deep links in command line arguments
- **MainWindowViewModel.cs**: Handles deep link navigation logic
- **App.axaml.cs**: Passes deep link to MainWindowViewModel on startup

### Adding New Deep Link Actions

To add a new deep link action (e.g., `oldportal://settings`):

1. Update `DeepLinkParser.TryParseLaunchUri()` to handle the new action
2. Add a new method to `MainShellViewModel` to handle the action
3. Update `App.axaml.cs` to route the new action

## Related Files

- `register-protocol.reg` - Manual registry import file
- `Register-OldPortalProtocol.ps1` - PowerShell registration script
- `Unregister-OldPortalProtocol.ps1` - PowerShell unregistration script
- `Utilities/DeepLinkParser.cs` - Deep link parsing logic
- `Models/DeepLinkInfo.cs` - Deep link data model

## Future Enhancements

- [x] Single-instance enforcement (bring existing window to foreground) - **Implemented**
- [x] GUID-based server identification for reliable server lookup - **Implemented**
- [ ] Support for additional deep link actions (e.g., `oldportal://settings`, `oldportal://friends`)
- [ ] Query parameters for additional context (e.g., `oldportal://launch/{serverId}?credential=main`)
- [ ] macOS and Linux protocol registration support (when cross-platform support is added)
