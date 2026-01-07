# Changelog

All notable changes to OldPortal Launcher will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- Plugin manager
- Server ping/latency testing
- In-launcher news feed
- Theme customization options

---

## [1.0.92] - 2026-01-07

### Added
- **Auto-Save Settings**: Settings now automatically save when navigating away from the Settings view
  - Removed manual "Save Settings" button for improved UX
  - Silent auto-save on navigation with comprehensive logging
  - Validation warnings logged but don't block navigation
  - Users no longer need to manually click save

- **Decal + Multi-Client Integration**: Full support for using Decal with multi-client functionality
  - New launch priority system: Decal injection takes precedence over custom multi-client hook
  - When both Decal and Multi-Client are enabled, launcher uses Decal's injection with Dual Log support
  - Added prominent warning panel in Settings when both features are enabled
  - Step-by-step instructions for enabling "Dual Log" in Decal Options
  - Seamless integration leverages Decal's built-in multi-client capabilities

### Changed
- **Launch Priority System**: Restructured game launch logic for better Decal integration
  - Priority 1: Decal injection (if enabled, handles multi-client via Dual Log)
  - Priority 2: OPLauncher.Hook.dll (if multi-client enabled but Decal disabled)
  - Priority 3: Standard launch (no injection)
  - Enhanced logging shows which injection method is being used
  - Decal's "Dual Log" feature now handles multi-client when both features are enabled

- **Settings User Experience**: Improved settings workflow
  - Settings auto-save when user navigates away from Settings view
  - Cleaner interface without manual save button
  - Real-time property change notifications for UI updates
  - Better feedback for Decal + Multi-Client configuration

### Technical
- Added `ShowDecalDualLogWarning` computed property in `SettingsViewModel`
  - Reactive property updates when UseDecal or EnableMultiClient changes
  - Triggers UI warning panel visibility automatically
- Updated `GameLaunchService.cs` with new launch decision tree
  - Added `useDecalInjection` flag for tracking injection type
  - Reorganized priority logic to check Decal first
  - Multi-client hook only used when Decal is disabled
- Enhanced `SettingsViewModel.OnNavigatedFrom()` lifecycle hook
  - Calls new `SaveSettingsOnNavigateAway()` method
  - Automatically configures UserPreferences.ini if needed
  - Graceful error handling that doesn't block navigation
- Updated Settings view XAML with conditional warning panel
  - Orange-bordered info panel with warning icon
  - Only visible when `ShowDecalDualLogWarning` is true
  - Clear 4-step instructions for Decal configuration

### Documentation
- Updated CHANGELOG.md to reflect new version and features
- Updated version to 1.0.92 in project file

---

## [1.0.77] - 2026-01-04

### Added
- **Multi-Client Support**: Launch multiple AC game instances simultaneously
  - Pure C# implementation using Windows API (no Decal required)
  - Suspended process launch technique bypasses AC's single-instance mutex
  - Quick-launch modal for ad-hoc multi-client sessions
  - Batch Groups system for saving multi-client launch profiles
  - Sequential launch with configurable delays (0-30 seconds)
  - Automatic UserPreferences.ini configuration (ComputeUniquePort=True)
  - Comprehensive in-app help and troubleshooting guide
  - Launch history tracking (last 50 launches)
  - Pre-launch validation and safety checks
  - Favorite batch quick-launch feature
  - Server-scoped batch management

- **New Settings Section**: Multi-Client Settings panel
  - Enable/disable multi-client support toggle
  - Auto-configure UserPreferences.ini option
  - Default launch delay configuration (0-30 seconds)
  - Max simultaneous clients limit (1-50, default: 12)
  - UserPreferences.ini status checking
  - One-click configuration and folder access

- **Multi-Launch Dialog**: Modal for quick batch launches
  - Select multiple accounts from credential list
  - Configure launch order and delays per account
  - Sequential vs simultaneous launch modes
  - Real-time progress tracking
  - Cancel mid-launch support
  - Validation before launch

- **Batch Groups Management**: Save and reuse multi-client configurations
  - Create, edit, and delete batch groups
  - Reorder entries with drag-and-drop or order fields
  - Set favorite batches per server
  - Launch history per batch
  - Server-scoped storage (each server has separate batches)
  - Batch validation (checks for deleted credentials)

- **In-App Help System**: Comprehensive help documentation
  - Multi-client overview and setup guide
  - Troubleshooting common issues
  - FAQ with 11+ questions
  - Tips & best practices
  - Accessible from Settings, WorldDetailView, and Multi-Launch Dialog

### Technical
- Added `SuspendedProcessLauncher` utility class (Windows API P/Invoke)
  - CreateProcessW with CREATE_SUSPENDED flag
  - ResumeThread for process control
  - Safe handle management
- Added `LaunchSequencerService` for orchestrating multi-client launches
  - Event-driven architecture with progress tracking
  - Configurable delays and abort-on-failure support
  - Pre-launch validation (multi-client enabled, UserPreferences configured, etc.)
  - Post-launch tracking and reporting
- Added `BatchGroupService` with LiteDB storage
  - CRUD operations for batch groups
  - Launch history recording and retrieval
  - Favorite batch management
  - Automatic history trimming (keeps last 50 entries)
- Added `UserPreferencesManager` for AC settings configuration
  - Read/write UserPreferences.ini with thread-safe locking
  - Automatic backup before modifications
  - INI file parsing and validation
  - ComputeUniquePort detection and enablement
- Added `MultiClientLaunchHistory` model for tracking launch records
- Added `MultiClientHelpViewModel` for in-app help navigation
- Updated `WorldDetailViewModel` with batch management integration
- Updated `SettingsViewModel` with multi-client configuration

### Changed
- Updated version to 1.0.77
- Enhanced credential management to support multi-client workflows
- Improved validation messages with actionable guidance

### Documentation
- Created comprehensive `docs/MULTI_CLIENT_GUIDE.md`
- Updated README.md with multi-client feature highlights
- Added multi-client to feature list and roadmap
- Moved multi-character launch from planned to completed features

---

## [1.0.76] - 2026-01-03

### Added
- **InnoSetup Installer**: Migrated from Velopack to InnoSetup for better customization
  - Custom installation directory picker
  - Desktop shortcut option (user choice)
  - Auto-startup with Windows option (user choice)
  - Professional uninstaller
  - Full OldPortal branding with custom wizard images
- **Search and Filter Fixes**: Fixed Browse Worlds search and filter functionality
  - Search now properly updates UI sections
  - Ruleset filter rebuilds sections correctly
  - Online-only toggle works as expected
  - Sort options update display

### Changed
- **Installer Size Reduction**: 26% smaller installer (62 MB → 46 MB)
- **Update System**: Updates now manual (download from oldportal.com/downloads)
  - Removed auto-update download/install functionality
  - Kept version checking via API
  - "Install Update" button now opens download page in browser

### Removed
- **Velopack**: Completely removed Velopack auto-update system
  - Removed all Velopack package references
  - Removed VelopackApp initialization code
  - Removed UpdateManager download/apply methods
  - Removed UpdateSourceUrl configuration property

### Fixed
- Decal Installation onboarding step scrolling issue
- Browse Worlds sections not updating with search/filter

---

## [1.0.75] - 2025-12-XX

### Changed
- Optimized Decal Installation step in onboarding wizard
  - Reduced spacing and font sizes to fit on screen without scrolling
  - Compacted instructions to single line
  - Reduced MinHeight of status box

---

## [1.0.74] - 2025-12-XX

### Changed
- General bug fixes and optimizations

---

## [1.0.70-1.0.73] - 2025-11-XX

### Added
- Initial stable release
- World browsing with search and filters
- Credential management with DPAPI encryption
- Favorites and recent servers
- Decal support
- Deep link protocol (`oldportal://play/{worldId}`)
- Offline mode with cached data
- Server status monitoring via UDP

### Changed
- Various bug fixes and improvements
- UI refinements and polish

---

## [0.9.x] - 2025-10-XX (Beta)

### Added
- Initial beta releases
- Core functionality development
- UI design and implementation
- API integration

---

## Version History Summary

| Version | Release Date | Installer Type | Size | Key Feature |
|---------|--------------|----------------|------|-------------|
| 1.0.92  | 2026-01-07   | InnoSetup      | ~43 MB | Auto-save settings, Decal + Multi-Client |
| 1.0.77  | 2026-01-04   | InnoSetup      | ~43 MB | Multi-client support |
| 1.0.76  | 2026-01-03   | InnoSetup      | 46 MB | Custom install directory |
| 1.0.75  | 2025-12-XX   | Velopack       | 62 MB | Onboarding optimization |
| 1.0.70  | 2025-11-XX   | Velopack       | 62 MB | Initial stable release |

---

## Links

- [GitHub Repository](https://github.com/yourusername/oplauncher)
- [Download Page](https://oldportal.com/downloads)
- [Documentation](https://github.com/yourusername/oplauncher/tree/main/docs)
- [Report Issues](https://github.com/yourusername/oplauncher/issues)

---

## Upgrade Notes

### Upgrading to 1.0.76 from Earlier Versions

**Important**: Version 1.0.76 changes the installer system. To upgrade:

1. **Uninstall** the old version (your data is preserved)
2. **Download** the new installer from oldportal.com/downloads
3. **Run** `OPLauncher-Setup.exe` and choose your installation directory
4. **Launch** and verify your credentials and favorites are intact

**Data Location**: User data is stored in `%LOCALAPPDATA%\OldPortal\launcher\` and is NOT removed during uninstall.

---

**Legend**:
- **Added**: New features
- **Changed**: Changes to existing functionality
- **Deprecated**: Soon-to-be removed features
- **Removed**: Removed features
- **Fixed**: Bug fixes
- **Security**: Security improvements
