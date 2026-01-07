# Multi-Client Support Guide

## Table of Contents
- [Overview](#overview)
- [What is Multi-Client?](#what-is-multi-client)
- [How It Works](#how-it-works)
- [Requirements](#requirements)
- [Setup Guide](#setup-guide)
- [Using Decal with Multi-Client](#using-decal-with-multi-client)
- [Using Multi-Client Launch](#using-multi-client-launch)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)
- [Known Limitations](#known-limitations)
- [Technical Details](#technical-details)

---

## Overview

Multi-client support allows you to launch multiple Asheron's Call game clients simultaneously from a single AC installation. This feature is commonly used for:

- **Multi-boxing**: Running multiple characters for trading, buffing, or fellowship coordination
- **Bot armies**: Managing 20+ clients with automation tools like VTank
- **Testing**: Running parallel game sessions for character builds or quest testing
- **Allegiance management**: Coordinating multiple accounts across your allegiance

**Key Features:**
- ✅ Two launch methods: Native hook (no Decal) OR Decal's Dual Log feature
- ✅ Seamless Decal integration for users who want plugins + multi-client
- ✅ Works with vanilla AC client - no modifications needed
- ✅ Multi-client dialog for managing account launches
- ✅ Automatic configuration persistence per-world
- ✅ Easy account reordering with ↑↓ buttons
- ✅ Sequential launch with configurable delays per account
- ✅ Automatic UserPreferences.ini configuration
- ✅ Smart merging of new/deleted credentials
- ✅ Comprehensive validation and safety checks
- ✅ Clear in-app guidance for Decal + Multi-Client setup

---

## What is Multi-Client?

By default, Asheron's Call only allows one client instance to run at a time. This is enforced by a mutex (mutual exclusion object) in the AC client executable.

**Multi-client support bypasses this restriction** by using a native hook DLL (`OPLauncher.Hook.dll`) that hooks the Windows mutex creation functions and prevents the mutex check from blocking multiple instances.

This is a **legal and safe technique** that:
- Does not modify the AC executable
- Uses a separate hook DLL loaded via injector.dll
- Implements function hooking via the Reloaded.Hooks library
- Is compatible with all AC emulator servers

---

## How It Works

### Technical Overview

The OldPortal Launcher uses a native hook DLL to bypass the AC client's single-instance mutex:

1. **DLL Injection**: The launcher injects `OPLauncher.Hook.dll` into the AC process using `injector.dll`
2. **Function Hooking**: The hook DLL uses Reloaded.Hooks to intercept Windows mutex creation functions
3. **Mutex Bypass**: When the AC client tries to create its single-instance mutex, the hook prevents the check
4. **File Sharing Fix**: The hook also ensures proper file sharing by modifying file access flags
5. **Normal Execution**: The AC client runs normally with unique port assignment, unaware of other instances

### Port Configuration

For multiple clients to work, each instance must use a unique network port. This is configured in `UserPreferences.ini`:

```ini
[Options]
ComputeUniquePort=True
```

When enabled, each AC client automatically computes and uses a unique port based on its process ID, preventing port conflicts.

---

## Requirements

### System Requirements
- **Operating System**: Windows (7, 8, 10, 11)
- **Single AC Installation**: No need for multiple copies of AC
- **RAM**: ~200-500MB per AC client (most modern PCs can handle 10+ easily)
- **Disk Space**: Standard AC installation requirements

### Configuration Requirements
- **UserPreferences.ini**: Must have `ComputeUniquePort=True` (auto-configurable via launcher)
- **Saved Credentials**: At least 2 saved account credentials for the server
- **Multi-Client Enabled**: Toggle in OldPortal Launcher settings

---

## Setup Guide

### Step 1: Enable Multi-Client Support

1. Open OldPortal Launcher
2. Navigate to **Settings**
3. Scroll to the **Multi-Client Settings** section
4. Check **"Enable multi-client support"**
5. Configure optional settings:
   - **Auto-configure UserPreferences.ini**: Recommended (checked by default)
   - **Default launch delay**: 3-5 seconds recommended
   - **Max simultaneous clients**: Set based on your hardware (default: 12)
6. Click **Save Settings**

### Step 2: Configure UserPreferences.ini

**Option A: Automatic Configuration (Recommended)**
1. In Settings → Multi-Client Settings
2. Ensure **"Auto-configure UserPreferences.ini when enabled"** is checked
3. Click **"Configure Now"** button
4. The launcher will:
   - Backup your existing UserPreferences.ini
   - Add or update `ComputeUniquePort=True`
   - Confirm success with a status message

**Option B: Manual Configuration**
1. In Settings → Multi-Client Settings
2. Click **"Open UserPreferences.ini Location"**
3. Open `UserPreferences.ini` in a text editor (Notepad)
4. Find the `[Options]` section (or create it if missing)
5. Add or update: `ComputeUniquePort=True`
6. Save the file
7. Restart any running AC clients

**Verification:**
- Click **"Check Status"** in Settings to verify configuration
- Status should show: ✓ ComputeUniquePort enabled

### Step 3: Save Multiple Credentials

1. Navigate to a server's detail page
2. Add 2 or more account credentials:
   - Click **"Add Credential"**
   - Enter username and password
   - Click **"Save Credential"**
3. Repeat for each account you want to multi-box

---

## Using Decal with Multi-Client

If you want to use Decal plugins AND multi-client functionality simultaneously, the OldPortal Launcher makes this easy by leveraging Decal's built-in "Dual Log" feature.

### How It Works

When both Decal and Multi-Client are enabled in Settings:
- The launcher uses Decal's injection method instead of the native OPLauncher.Hook.dll
- Decal's "Dual Log" feature handles the multi-client mutex bypass
- You get full Decal plugin support across all clients
- UserPreferences.ini is still auto-configured for proper file sharing

### Setup Steps

1. **Enable Decal in Settings**
   - Navigate to Settings in OldPortal Launcher
   - Check "Use Decal when launching game"

2. **Enable Multi-Client in Settings**
   - Check "Enable multi-client support"
   - A warning panel will appear (orange-bordered)

3. **Enable Dual Log in Decal** (REQUIRED)
   - Open Decal (the standalone app)
   - Click the **"Options"** button
   - Check the **"Dual Log"** checkbox
   - Click **"OK"** to save

4. **Launch Multiple Clients**
   - Use the Multi-Client Launch dialog as normal
   - All clients will launch with Decal injection
   - Decal plugins will be available in each client

### Important Notes

- **Dual Log is Required**: Without enabling "Dual Log" in Decal Options, only ONE client will launch successfully
- **One-Time Setup**: You only need to enable Dual Log once - Decal remembers this setting
- **Warning Panel**: The launcher shows a clear warning with step-by-step instructions when both features are enabled
- **Launch Priority**: When both are enabled, Decal takes priority over the native hook

### Troubleshooting Decal + Multi-Client

**Problem**: Only one client launches when both Decal and Multi-Client are enabled

**Solution**:
1. Open Decal (not the launcher)
2. Click "Options"
3. Verify "Dual Log" checkbox is checked
4. Click "OK"
5. Try launching again

**Problem**: I don't want to use Decal for multi-client

**Solution**: Simply disable "Use Decal when launching game" in Settings. The launcher will use the native OPLauncher.Hook.dll instead.

---

## Using Multi-Client Launch

The multi-client dialog provides an easy way to launch multiple accounts with automatic configuration saving.

### Opening Multi-Client Dialog

1. Navigate to a server's detail page
2. Ensure you have 2+ saved credentials
3. Click **"🚀 Multi-Client Launch"** button

### Configuring Your Launch

The dialog shows all your saved credentials for the selected world. For each account, you can:

1. **Include/Exclude**: Check the box to include the account in the launch
2. **Set Delays**: Configure delay in seconds after each account launches
   - **0 seconds**: Simultaneous launch (fastest, requires good hardware)
   - **3-5 seconds**: Balanced stability (recommended)
   - **5-10 seconds**: Maximum stability (older systems)
3. **Reorder Accounts**: Use the ↑ and ↓ buttons to change launch order
   - Click ↑ to move an account earlier in the sequence
   - Click ↓ to move an account later in the sequence
   - The # column shows the current launch order

### Automatic Configuration Saving

Your configuration is **saved automatically** when you close the dialog:
- **Per-World Memory**: Each world remembers its own account order and delays
- **Persistent**: Settings are preserved between launcher sessions
- **Smart Merging**:
  - New accounts are automatically added to the end of your saved order
  - Deleted accounts are automatically removed from the saved configuration
  - Your existing order and delays are preserved

### Launching

1. Click **"Launch"** to start the sequence
2. Monitor progress in the dialog:
   - Status message shows current progress
   - Each account launches in order with configured delays
3. The launcher will:
   - Validate your configuration
   - Inject the hook DLL into each client
   - Launch each client in the specified order
   - Apply delays between launches
   - Report success/failure
4. Click **"Cancel"** to:
   - Abort mid-sequence if launching
   - Close the dialog and save your configuration

---

## Troubleshooting

### "Only one client allowed" Error

**Problem**: AC shows an error preventing multiple instances

**Solution**: Check UserPreferences.ini configuration
1. Go to Settings → Multi-Client Settings
2. Click **"Check Status"** to verify configuration
3. If not configured, click **"Configure Now"**
4. Restart any running AC clients
5. Try launching again

### Clients Won't Launch

**Problem**: Nothing happens when launching

**Solutions**:
- Verify AC client path in Settings is correct
- Increase launch delays (try 5 seconds)
- Check that credentials are still valid
- Review launcher logs for errors
- Ensure multi-client is enabled in Settings

### Port Conflicts

**Problem**: Clients conflict or can't connect

**Solution**: Verify ComputeUniquePort=True
- This setting makes each client use a unique port
- Without it, multiple clients will conflict
- Use Settings → Configure Now to fix automatically

### Performance Issues

**Problem**: System slows down with many clients

**Solutions**:
- Reduce number of simultaneous launches
- Increase launch delays to 5-10 seconds
- Set MaxSimultaneousClients limit in Settings
- Close unnecessary background applications
- Upgrade RAM if running 20+ clients regularly

### Validation Errors

**Problem**: "Multi-client support is disabled"
- **Solution**: Enable in Settings → Multi-Client Settings

**Problem**: "UserPreferences.ini is not configured"
- **Solution**: Click "Configure Now" in Settings

**Problem**: "Cannot launch X clients (limit is Y)"
- **Solution**: Reduce selection or increase MaxSimultaneousClients in Settings

**Problem**: "Credential 'username' not found"
- **Solution**: The credential was deleted. It will be automatically removed from your saved configuration on next close.

### Firewall Warnings

**Expected Behavior**: Each AC client instance may trigger a Windows Firewall prompt
- Allow access on private networks
- This is normal and safe
- You can create a permanent firewall rule for acclient.exe

---

## FAQ

### Q: Do I need Decal to use multi-client?
**A**: NO! Multi-client has two methods:
1. **Native Hook** (default): Uses OPLauncher.Hook.dll - no Decal required
2. **Decal's Dual Log**: If you enable both Decal and Multi-Client in settings, the launcher uses Decal's built-in Dual Log feature instead

Choose whichever method suits your needs!

### Q: Do I need multiple AC installations?
**A**: NO! You only need your single AC installation. The launcher injects a hook DLL that bypasses the single-instance mutex restriction.

### Q: Can I launch clients on different servers?
**A**: YES! However, each world has its own saved configuration. You'll configure the order and delays separately for each server.

### Q: Will this get me banned?
**A**: NO! This uses the standard AC client with a hook DLL. It's perfectly legal. However, check individual server policies about multi-boxing limits.

### Q: How many clients can I run simultaneously?
**A**: There's no hard limit. AC is lightweight (~200-500MB per client). Most modern PCs can handle 10+ easily. Bot armies commonly run 20+. Set MaxSimultaneousClients in Settings as a safety limit.

### Q: What are the recommended launch delays?
**A**:
- **3-5 seconds**: Balanced stability for most systems
- **0 seconds**: Fast simultaneous launch (good hardware required)
- **5-10 seconds**: Maximum stability on older systems

### Q: Can I use this with VTank or other bot tools?
**A**: YES! Multi-client is commonly used with VTank for bot armies (20+ clients). Check server policies regarding automation.

### Q: Does multi-client work with Decal?
**A**: YES! When you enable both Decal and Multi-Client in settings:
- The launcher automatically uses Decal's injection with Dual Log support
- You'll see a warning panel in Settings with instructions to enable "Dual Log" in Decal Options
- Once Dual Log is enabled in Decal, you get both Decal plugins AND multi-client functionality
- If you only enable Multi-Client (no Decal), the native OPLauncher.Hook.dll is used instead

### Q: Where is UserPreferences.ini located?
**A**: Click "Open UserPreferences.ini Location" in Settings → Multi-Client Settings. It's typically in your AC installation directory.

### Q: Does my configuration save automatically?
**A**: YES! When you close the multi-client dialog, your account order and delay settings are automatically saved for that world.

### Q: What happens if I add or delete credentials?
**A**: Your configuration automatically adjusts:
- **New credentials** are added to the end of your saved order with default delays
- **Deleted credentials** are automatically removed from your saved configuration

### Q: Can I launch the same account multiple times?
**A**: NO. The AC server will reject duplicate logins from the same account. Each client must use a different account credential.

### Q: How do I reset my saved configuration?
**A**: Simply reorder your accounts and adjust delays in the dialog. Your changes save automatically when you close the dialog.

---

## Known Limitations

### Current Limitations

1. **Windows Only**: Multi-client uses Windows-specific hook DLL and won't work on Linux/Mac (even with Wine)

2. **Server-Scoped Configurations**: Configurations are specific to one server. Each world has its own independent saved configuration.

3. **Manual Process Management**: The launcher does not track launched processes after they start. If clients crash, you must relaunch manually

4. **No Cross-Session Persistence**: Launch progress is not saved if you close the launcher mid-sequence

5. **Sequential Delays Only**: All delays are applied sequentially. You cannot configure complex timing patterns (e.g., launch 3 immediately, then wait 10s, then launch 2 more)

### Server Policy Restrictions

Some ACE servers may have policies restricting:
- Maximum number of simultaneous connections per IP
- Multi-boxing for PvP or competitive content
- Automated gameplay (bot armies)

**Always check server rules** before multi-boxing extensively. Most servers allow reasonable multi-boxing for legitimate purposes.

---

## Technical Details

### Implementation Architecture

The multi-client feature is implemented with the following components:

**Core Services:**
- `GameLaunchService` - Handles game client launching with hook DLL injection
- `LaunchSequencerService` - Orchestrates sequential launches with delays and progress tracking
- `MultiLaunchConfigService` - CRUD operations for multi-client configurations with LiteDB storage
- `UserPreferencesManager` - Reads/writes UserPreferences.ini for ComputeUniquePort configuration

**Data Models:**
- `MultiLaunchConfiguration` - Per-world configuration with account entries
- `LaunchEntryConfig` - Individual account entry with order and delay
- `LaunchTask` - Runtime task representation for sequencer

**UI Components:**
- `MultiLaunchDialog` - Modal for multi-client configuration and launching
- `MultiClientHelpView` - In-app help documentation
- Settings multi-client configuration panel

**Native Components:**
- `OPLauncher.Hook.dll` - Native C++ DLL using Reloaded.Hooks library
- `injector.dll` - DLL injection utility for loading the hook DLL

### Hook DLL Implementation

The `OPLauncher.Hook.dll` uses the Reloaded.Hooks library to:

1. **Hook CreateMutexW/CreateMutexA**: Intercepts mutex creation to prevent single-instance check
2. **Hook CreateFileW/CreateFileA**: Modifies file access flags to enable proper file sharing
3. **Return Success**: Allows the AC client to think it succeeded while bypassing restrictions

This is implemented in native C++ for maximum performance and compatibility.

### Security & Safety

**Credential Storage:**
- All passwords are encrypted using Windows DPAPI (Data Protection API)
- Credentials are scoped to the current user
- No plaintext passwords are stored or logged

**Process Isolation:**
- Each AC client runs as a separate, independent process
- Clients do not share memory or interfere with each other
- Standard Windows process security applies

**Validation & Safety:**
- Pre-launch validation prevents misconfiguration
- User-configurable limits (MaxSimultaneousClients)
- Comprehensive error handling and logging

---

## Support & Feedback

If you encounter issues or have suggestions:

1. **In-App Help**: Click the ❓ Help buttons in Settings or Multi-Client dialog
2. **Documentation**: Review this guide and other docs in the [docs/](../docs/) directory
3. **Logs**: Check launcher logs at `%LOCALAPPDATA%\OldPortal\launcher\logs\` for detailed error information
4. **Community Forum**: [Bug Reports & Help](https://oldportal.com/community/category/bug-reports)
5. **Discord**: [OldPortal Community Discord](https://discord.gg/UKdy2b9zBe)

---

## Credits

Multi-client support was implemented using:
- **Reloaded.Hooks** - Native function hooking library for mutex bypass
- **injector.dll** - DLL injection for loading the hook DLL
- **LiteDB** - Local database storage for configuration persistence
- **CommunityToolkit.Mvvm** - MVVM architecture for reactive ViewModels
- **Avalonia UI** - Cross-platform UI framework

---

*Last Updated: 2026-01-06*
