# OldPortal Launcher - Versioning Strategy

## Single Source of Truth

The launcher version is now managed in **ONE place only**: `OPLauncher.csproj`

```xml
<Version>1.0.91</Version>
<AssemblyVersion>1.0.91.0</AssemblyVersion>
<FileVersion>1.0.91.0</FileVersion>
```

## How It Works

All parts of the application automatically read the version from the compiled assembly at runtime:

- **Logs**: Version displayed in startup logs comes from assembly metadata
- **Settings UI**: Version shown in Settings screen reads from assembly
- **Update checks**: UpdateService reads version from assembly for update comparison
- **Installer**: InnoSetup reads version directly from the built executable using `GetFileVersion()`

## How to Update Version

**Step 1:** Edit `OPLauncher.csproj` and update the version:
```xml
<Version>1.0.92</Version>
<AssemblyVersion>1.0.92.0</AssemblyVersion>
<FileVersion>1.0.92.0</FileVersion>
```

**Step 2:** Build the installer:
```powershell
.\scripts\Build-Release.ps1
```

**That's it!** The version will be automatically synchronized across:
- Application binary (assembly metadata)
- Installer package (InnoSetup reads from the exe dynamically)
- Log files (startup version message)
- Settings UI (version display)
- Update service (version comparison)

**No manual updates needed** - the installer reads the version directly from the built executable using InnoSetup's `GetFileVersion()` function.

## Build Script

The `scripts\Build-Release.ps1` script automates the build process:
1. Reads version from OPLauncher.csproj (for display only)
2. Cleans previous builds
3. Publishes the application (dotnet publish)
4. Creates installer with InnoSetup (ISCC.exe reads version from built exe)

## Installer Location

All installers are created in the `Releases/` folder:
- `OPLauncher-Setup.exe` - Main installer (InnoSetup)

## How Dynamic Versioning Works

The `installer.iss` file uses InnoSetup's preprocessor to read the version directly from the built executable:

```pascal
#define MyAppVersion GetFileVersion("publish\win-x86\OPLauncher.exe")

[Setup]
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
```

This means:
- **No manual updates needed** in `installer.iss` when changing versions
- The installer version is **always synchronized** with the application version
- **Single source of truth**: Only `OPLauncher.csproj` needs to be updated
- **Impossible to have version mismatches** between app and installer

## Migration Notes

**Previous system** (removed in v1.0.26):
- ❌ Version in `appsettings.json` (ApiSettings.LauncherVersion)
- ❌ Version in `LauncherConfig.cs` model
- ❌ Manual version updates in 3+ files
- ❌ Version mismatches between logs and installer
- ❌ Two installer locations (`installers/` and `Releases/`)
- ❌ Old `Build-Installer.ps1` using Clowd.Squirrel

**Velopack system** (v1.0.26 - v1.0.75):
- ✅ Single source: `OPLauncher.csproj`
- ✅ Automatic version reading from assembly
- ✅ Build script using Velopack (vpk pack)
- ❌ No custom installation directory
- ❌ Limited branding options
- ❌ Larger installer size (62 MB)

**Current system** (v1.0.76+):
- ✅ **True single source**: `OPLauncher.csproj` only (installer reads from exe dynamically)
- ✅ Automatic version reading from assembly everywhere
- ✅ No manual version updates needed in installer.iss
- ✅ Synchronized versions everywhere
- ✅ One installer location: `Releases/`
- ✅ New `scripts\Build-Release.ps1` using InnoSetup 6.6.1+
- ✅ Custom installation directory picker
- ✅ Full branding customization
- ✅ Smaller installer size (~46 MB, 26% reduction from Velopack)

## Verification

After building, verify the version is correct:

1. **Check installer filename**: `Releases/OPLauncher-Setup.exe`
2. **Check installer properties**: Right-click → Properties → Details tab should show the version from `OPLauncher.csproj`
3. **Run the application** and check logs:
   ```
   2026-01-06 12:00:00.000 [INF] OldPortal Launcher starting up
   2026-01-06 12:00:00.000 [INF] Version: 1.0.91
   ```
4. **Open Settings** and confirm version matches

All should show the same version number - no discrepancies possible since there's only one source of truth.
