# OldPortal Launcher

A modern, cross-platform launcher for Asheron's Call emulator servers via the OldPortal.com platform.

![Version](https://img.shields.io/badge/version-1.0.92-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

## 🎮 Overview

OldPortal Launcher is a desktop application that provides a unified interface for discovering, managing, and launching Asheron's Call emulator servers. Built with modern technologies including .NET 9 and Avalonia UI, it offers a sleek, responsive experience for the AC emulator community.

### Key Features

- 🌍 **World Browser**: Browse and search available AC servers with advanced filtering
- ⭐ **Favorites & Recent**: Quick access to your favorite servers and play history
- 🔐 **Credential Management**: Secure storage of account credentials using DPAPI encryption
- 🚀 **Advanced Multi-Client**: Launch multiple game instances without Decal - configurations save automatically, easy account reordering, custom delays per-world
- 🎨 **Modern UI**: Beautiful, responsive interface built with Avalonia UI
- 🔧 **Decal Support**: Optional Decal plugin framework integration for advanced features
- 📊 **Server Status**: Real-time UDP status checks for server availability
- 🔗 **Deep Links**: Launch servers directly via `oldportal://` protocol URLs
- 🌐 **Offline Mode**: Cached server data for offline browsing

## 🚀 Getting Started

### Prerequisites

- Windows 10/11 (x86 or x64)
- .NET 9.0 SDK (for building from source)
- InnoSetup 6.6.1+ (for creating installer) - [Download](https://jrsoftware.org/isdl.php)
- Asheron's Call client (`acclient.exe`)
- UserPreferences.ini configuration for multi-client (auto-configurable)

### Installation

#### Option 1: Download Installer (Recommended)

1. Download the latest `OPLauncher-Setup.exe` from [Releases](../../releases)
2. Run the installer and follow the setup wizard
3. Choose your installation directory
4. Launch OldPortal Launcher from the Start Menu or Desktop

#### Option 2: Build from Source

```powershell
# Clone the repository

# Open in Visual Studio (recommended)
# Double-click OPLauncher.sln

# Or build from command line
dotnet build -c Release

# Run the application
dotnet run

# Or build the installer
.\scripts\Build-Release.ps1
```

### First Run Setup

On first launch, you'll be guided through a setup wizard:

1. **Welcome** - Introduction to OldPortal Launcher
2. **AC Client Path** - Locate your `acclient.exe` installation
4. **Decal** - Optionally install and configure Decal
5. **Complete** - Start browsing worlds!

## 📖 Features in Detail

### World Discovery

Browse hundreds of AC emulator servers with:
- **Search**: Find servers by name or description
- **Filters**: Filter by ruleset (PvE, PvP, RP, etc.)
- **Sorting**: Sort by name, player count, uptime, or server type
- **Featured**: Discover highlighted servers curated by the community

### Credential Vault

Securely store your account credentials:
- **DPAPI Encryption**: Windows Data Protection API for secure storage
- **Multi-Account**: Save credentials for multiple servers
- **Quick Launch**: One-click login with saved credentials
- **Import/Export**: Backup and restore your credentials

### Server Management

- **Favorites**: Star your favorite servers for quick access
- **Manual Servers**: Add custom/private servers by IP and port
- **Server Details**: View comprehensive server information and player counts

### Decal Integration

Optional support for the Decal plugin framework:
- **Auto-Detection**: Automatically detects Decal installation
- **Per-Launch Toggle**: Choose to enable/disable Decal
- **Injection**: Seamless DLL injection for plugin support
- **Multi-Client Compatible**: Works with multi-client when Dual Log is enabled in Decal

### Multi-Client Support

Launch multiple game instances simultaneously with powerful management features:
- **Flexible Launch Methods**: Choose between native hook (no Decal) or Decal's Dual Log feature
- **Decal Integration**: When both Decal and Multi-Client are enabled, leverages Decal's built-in Dual Log
- **Automatic Configuration**: Settings save automatically when closing the multi-client dialog
- **Per-World Memory**: Each world remembers its own account order and launch delays
- **Easy Reordering**: Simple ↑↓ buttons to adjust launch order - no manual numbering
- **Custom Delays**: Set individual delays between account launches (0-60 seconds)
- **Smart Merging**: New accounts automatically append to saved configurations
- **File Sharing Fix**: Automatic UserPreferences.ini configuration for proper file sharing
- **Clear Guidance**: In-app warnings and instructions when Dual Log setup is required

## 🏗️ Architecture

### Technology Stack

- **UI Framework**: [Avalonia UI 11.3](https://avaloniaui.net/) - Cross-platform XAML-based UI
- **Platform**: .NET 9.0 - Modern .NET runtime
- **MVVM**: CommunityToolkit.Mvvm for reactive ViewModels
- **Database**: LiteDB for local caching and configuration
- **Networking**: System.Net.Http with retry policies
- **Encryption**: DPAPI (Data Protection API) for credential security
- **Multi-Client Hook**: Reloaded.Hooks library for native function hooking (mutex bypass)
- **Logging**: Serilog with file and console sinks
- **Installer**: InnoSetup 6.6.1+ for Windows installer creation

### Project Structure

```
OPLauncher/
├── Assets/                      # Images, icons, and resources
├── Controls/                    # Reusable UI controls
├── Converters/                  # XAML value converters
├── DTOs/                        # Data Transfer Objects (API models)
├── Models/                      # Local data models
├── Services/                    # Business logic and API wrappers
├── Utilities/                   # Helper classes and extensions
├── ViewModels/                  # MVVM ViewModels
├── Views/                       # XAML UI views
├── Styles/                      # Global XAML styles
├── Themes/                      # Color themes and styling
├── OPLauncher.Hook.Native/      # C++ native hook DLL (mutex bypass)
├── docs/                        # Documentation files
├── scripts/                     # Build scripts
├── injector.dll                 # Decal injection library
├── installer.iss                # InnoSetup installer script
└── appsettings.json             # Default configuration
```

## 🔧 Building & Development

### Development Setup

```powershell
# Install dependencies
dotnet restore

# Run in Debug mode
dotnet run

# Run with hot reload
dotnet watch run

# Build for Release
dotnet build -c Release
```

### Creating an Installer

```powershell
# Build and create installer (requires InnoSetup)
.\Build-Release.ps1

# Output: Releases/OPLauncher-Setup.exe
```

### Versioning

Version is managed in **one place**: `OPLauncher.csproj`

```xml
<Version>1.0.92</Version>
<AssemblyVersion>1.0.92.0</AssemblyVersion>
<FileVersion>1.0.92.0</FileVersion>
```

The installer dynamically reads the version from the built executable, so no manual updates needed in `installer.iss`.

See [docs/VERSIONING.md](docs/VERSIONING.md) for details.

## 📝 Configuration

Configuration files are stored in `%LOCALAPPDATA%\OldPortal\launcher\`:

- **config.json**: User preferences and settings
- **cache.db**: LiteDB database for cached server data and credentials
- **logs/**: Application log files

### appsettings.json

Default configuration (committed to repo):

```json
{
  "ApiSettings": {
    "ApiBaseUrl": "https://oldportal.com/api/v1",
    "TimeoutSeconds": 30
  },
  "Logging": {
    "MinimumLevel": "Information",
    "LogFilePath": "%LOCALAPPDATA%\\OldPortal\\launcher\\logs\\launcher-.log",
    "RetainedFileCountLimit": 30
  }
}
```

## 🔒 Security

### Credential Storage

- Credentials are encrypted using **Windows DPAPI** (Data Protection API)
- Encryption scope: `CurrentUser` (per-user, non-exportable)
- Stored in LiteDB database at `%LOCALAPPDATA%\OldPortal\launcher\cache.db`
- **Never** stored in plaintext

### API Communication

- All API calls use **HTTPS** with SSL certificate validation
- JWT tokens for authentication (short-lived access tokens)
- Refresh tokens encrypted with DPAPI
- No sensitive data logged (passwords/tokens redacted)

### Best Practices

- ✅ DPAPI encryption for local secrets
- ✅ HTTPS-only API communication
- ✅ No hardcoded credentials
- ✅ Secure token refresh mechanism
- ✅ Minimal permission requirements (no admin needed)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Development Guidelines

1. Follow the existing code style (see [CLAUDE.md](CLAUDE.md))
2. Use MVVM pattern for UI code
3. Add XML documentation comments for public APIs
4. Test with multiple server types before submitting
5. Update version in `OPLauncher.csproj` and `installer.iss`

### Code Style

- C# 12 / .NET 9 language features
- `async`/`await` for all I/O operations
- MVVM with ReactiveUI/CommunityToolkit
- XML documentation for public members
- Serilog structured logging

### Submitting Changes

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Asheron's Call**: Original game by Turbine Entertainment
- **ACEmulator**: The AC emulator community
- **Decal**: Plugin framework by Virindi
- **Avalonia UI**: Cross-platform UI framework
- **Reloaded.Hooks**: Native function hooking library for multi-client support
- **OldPortal.com**: Community platform for AC servers

## 📞 Support

- **Bug Reports**: [Community Forum](https://oldportal.com/community/category/bug-reports)
- **Feature Requests**: [Community Forum](https://oldportal.com/community/category/bug-reports)
- **Documentation**: [docs/](docs/)
- **Website**: [oldportal.com](https://oldportal.com)
- **Discord**: [OldPortal Community](https://discord.gg/UKdy2b9zBe)

## 🗺️ Roadmap

### Current Version (1.0.92)
- ✅ World browsing and search
- ✅ Credential management with DPAPI encryption
- ✅ Decal integration (optional)
- ✅ Favorites and recent servers
- ✅ Custom installation directory
- ✅ InnoSetup installer with dynamic versioning
- ✅ Advanced multi-client support (native hook or Decal Dual Log)
- ✅ Decal + Multi-Client integration with Dual Log support
- ✅ Auto-save settings (no manual save button required)
- ✅ Multi-client configuration persistence per world
- ✅ Easy account reordering with ↑↓ buttons
- ✅ Custom launch delays per account
- ✅ Launch history tracking
- ✅ System tray icon with context menu
- ✅ Smart launch priority system (Decal > Native Hook > Standard)

### Future Plans
- 🔄 Microsoft Trusted Signing

---

**Made with ❤️ for the Asheron's Call community**
