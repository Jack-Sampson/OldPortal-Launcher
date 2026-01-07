// TODO: [LAUNCH-139] Phase 4 Week 8 - PatchService
// Component: Launcher
// Module: First-Run Experience - End of Retail Patch Application
// Description: Downloads and applies End of Retail patch from Mega.nz

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;

namespace OPLauncher.Services;

/// <summary>
/// Service for downloading and applying the End of Retail patch to the AC client.
/// Uses MegaApiClient to download from Mega.nz and validates via MD5 checksum.
/// </summary>
public class PatchService
{
    private readonly LoggingService _logger;
    private readonly ConfigService _configService;

    /// <summary>
    /// Mega.nz download URL for the End of Retail patch.
    /// Contains 4 files: acclient.exe, portal.dat, cell_1.dat, and other required files.
    /// </summary>
    private const string PatchMegaUrl = "https://mega.nz/file/Q98n0BiR#p5IugPS8ZkQ7uX2A_LdN3Un2_wMX4gZBHowgs1Qomng";

    /// <summary>
    /// Expected MD5 hash of the patch file for validation.
    /// Hash calculated from: ac-updates.zip (End of Retail patch)
    /// File: C:\Users\jonny\Downloads\ac-updates.zip
    /// Calculated: 2026-01-03
    ///
    /// Security: Patch installation will validate the downloaded file against this hash
    /// to prevent man-in-the-middle attacks and corrupted downloads.
    /// </summary>
    private const string ExpectedMd5Hash = "968e92e9325ed19d7a9188734dfae090";

    /// <summary>
    /// Marker file created after successful patch application.
    /// </summary>
    private const string PatchMarkerFileName = ".oldportal_eor_patch_applied";

    /// <summary>
    /// Progress callback delegate for download progress reporting.
    /// </summary>
    /// <param name="bytesDownloaded">Number of bytes downloaded so far</param>
    /// <param name="totalBytes">Total size of the download in bytes</param>
    public delegate void DownloadProgressCallback(long bytesDownloaded, long totalBytes);

    /// <summary>
    /// Initializes a new instance of the PatchService.
    /// </summary>
    public PatchService(
        LoggingService logger,
        ConfigService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// Checks if the End of Retail patch has been applied to the AC client installation.
    /// </summary>
    /// <param name="acClientPath">Path to acclient.exe</param>
    /// <returns>True if patch is applied</returns>
    public bool IsPatchApplied(string? acClientPath)
    {
        if (string.IsNullOrWhiteSpace(acClientPath))
        {
            return false;
        }

        var installDir = Path.GetDirectoryName(acClientPath);
        if (string.IsNullOrEmpty(installDir))
        {
            return false;
        }

        var markerPath = Path.Combine(installDir, PatchMarkerFileName);
        return File.Exists(markerPath);
    }

    /// <summary>
    /// Downloads the End of Retail patch from Mega.nz.
    /// </summary>
    /// <param name="progressCallback">Optional callback for progress reporting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the downloaded patch file</returns>
    public async Task<string> DownloadPatchAsync(
        DownloadProgressCallback? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting End of Retail patch download from Mega.nz");

        try
        {
            // Create temp directory for download
            var tempDir = Path.Combine(Path.GetTempPath(), "OldPortal_Patch");
            Directory.CreateDirectory(tempDir);

            var downloadPath = Path.Combine(tempDir, "EndOfRetail_Patch.zip");

            // Initialize Mega client (anonymous login)
            var megaClient = new MegaApiClient();
            await megaClient.LoginAnonymousAsync();

            _logger.Debug("Connected to Mega.nz anonymously");

            // Get file information
            var fileLink = new Uri(PatchMegaUrl);
            var node = await megaClient.GetNodeFromLinkAsync(fileLink);

            _logger.Information("Downloading patch: {Name} ({Size} bytes)", node.Name, node.Size);

            // Download with progress tracking
            var progress = new Progress<double>(percentage =>
            {
                var bytesDownloaded = (long)(node.Size * percentage / 100.0);
                progressCallback?.Invoke(bytesDownloaded, node.Size);
                _logger.Debug("Download progress: {Percentage:F1}%", percentage);
            });

            await megaClient.DownloadFileAsync(fileLink, downloadPath, progress, cancellationToken);

            await megaClient.LogoutAsync();

            _logger.Information("Patch downloaded successfully to: {Path}", downloadPath);

            return downloadPath;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to download patch from Mega.nz: {Error}", ex.Message);
            throw new InvalidOperationException($"Patch download failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the downloaded patch file using MD5 checksum.
    /// </summary>
    /// <param name="patchFilePath">Path to the downloaded patch file</param>
    /// <returns>True if MD5 hash matches expected value</returns>
    public async Task<bool> ValidatePatchAsync(string patchFilePath)
    {
        if (!File.Exists(patchFilePath))
        {
            _logger.Error("Patch file not found: {Path}", patchFilePath);
            return false;
        }

        _logger.Information("Validating patch file MD5 hash");

        try
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(patchFilePath);
            var hashBytes = await md5.ComputeHashAsync(stream);
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            _logger.Debug("Computed MD5: {Hash}", hash);
            _logger.Debug("Expected MD5: {Expected}", ExpectedMd5Hash);

            if (hash.Equals(ExpectedMd5Hash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Information("Patch validation successful");
                return true;
            }
            else
            {
                _logger.Error("Patch validation failed: MD5 mismatch");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to validate patch file: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Result of a patch application operation.
    /// </summary>
    public class PatchResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ExtractedFilesPath { get; set; }
    }

    /// <summary>
    /// Applies the End of Retail patch to the AC client installation.
    /// Extracts patch files and copies them to the AC directory.
    /// </summary>
    /// <param name="patchFilePath">Path to the downloaded and validated patch file</param>
    /// <param name="acClientPath">Path to acclient.exe</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PatchResult with success status and file paths</returns>
    public async Task<PatchResult> ApplyPatchAsync(
        string patchFilePath,
        string acClientPath,
        CancellationToken cancellationToken = default)
    {
        var result = new PatchResult { Success = false };

        if (!File.Exists(patchFilePath))
        {
            _logger.Error("Patch file not found: {Path}", patchFilePath);
            result.ErrorMessage = $"Patch file not found: {patchFilePath}";
            return result;
        }

        var installDir = Path.GetDirectoryName(acClientPath);
        if (string.IsNullOrEmpty(installDir))
        {
            _logger.Error("Invalid AC client path: {Path}", acClientPath);
            result.ErrorMessage = "Invalid AC client path";
            return result;
        }

        _logger.Information("Applying End of Retail patch to: {InstallDir}", installDir);

        // Extract patch to temp directory (keep this outside try block so we can preserve it on failure)
        var tempExtractDir = Path.Combine(Path.GetTempPath(), "OldPortal_Patch_Extract");
        if (Directory.Exists(tempExtractDir))
        {
            try
            {
                Directory.Delete(tempExtractDir, true);
            }
            catch (Exception ex)
            {
                _logger.Warning("Could not delete old temp directory: {Error}", ex.Message);
            }
        }
        Directory.CreateDirectory(tempExtractDir);

        try
        {
            // Store extraction path for manual installation if needed
            result.ExtractedFilesPath = tempExtractDir;

            _logger.Debug("Extracting patch to: {TempDir}", tempExtractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(patchFilePath, tempExtractDir);

            // Check for ZIP wrapper folder (common when creating ZIPs on Windows/Mac)
            // If the ZIP contains a single top-level directory, unwrap it
            _logger.Information("========================================");
            _logger.Information("ANALYZING ZIP STRUCTURE");
            _logger.Information("========================================");

            var topLevelEntries = Directory.GetFileSystemEntries(tempExtractDir);
            _logger.Information("Found {Count} top-level entries in ZIP:", topLevelEntries.Length);
            foreach (var entry in topLevelEntries)
            {
                var isDir = Directory.Exists(entry);
                var entryName = Path.GetFileName(entry);
                _logger.Information("  - {Name} ({Type})", entryName, isDir ? "DIRECTORY" : "FILE");
            }

            if (topLevelEntries.Length == 1 && Directory.Exists(topLevelEntries[0]))
            {
                // Only one entry and it's a directory - this is likely a wrapper folder
                var wrapperDir = topLevelEntries[0];
                var wrapperName = Path.GetFileName(wrapperDir);
                _logger.Information("========================================");
                _logger.Information("ZIP WRAPPER DETECTED");
                _logger.Information("========================================");
                _logger.Information("Wrapper folder name: '{Folder}'", wrapperName);
                _logger.Information("Unwrapping to access patch files inside...");

                // Adjust tempExtractDir to point inside the wrapper
                tempExtractDir = wrapperDir;
                _logger.Information("New source directory: {TempDir}", tempExtractDir);
                _logger.Information("========================================");
            }
            else
            {
                _logger.Information("========================================");
                _logger.Information("No wrapper folder detected - files are at ZIP root level");
                _logger.Information("Using extraction directory as-is: {TempDir}", tempExtractDir);
                _logger.Information("========================================");
            }

            // Log extracted files for debugging
            var extractedFiles = Directory.GetFiles(tempExtractDir, "*", SearchOption.AllDirectories);
            _logger.Information("Extracted {Count} files from patch archive", extractedFiles.Length);
            _logger.Information("Extraction source directory: {Dir}", tempExtractDir);

            // Log ALL extracted files to help debug structure issues
            foreach (var file in extractedFiles)
            {
                var relativePath = Path.GetRelativePath(tempExtractDir, file);
                var fileSize = new FileInfo(file).Length;
                _logger.Information("  [Extracted] {File} ({Size:N0} bytes)", relativePath, fileSize);
            }

            if (extractedFiles.Length == 0)
            {
                _logger.Error("No files were extracted from the patch archive!");
                result.ErrorMessage = "No files were extracted from the patch archive";
                return result;
            }

            // Check for locked files before copying
            _logger.Information("Checking for locked files in game directory...");
            var lockedFiles = new List<string>();
            foreach (var criticalFile in new[] { "acclient.exe", "portal.dat", "cell_1.dat" })
            {
                var filePath = Path.Combine(installDir, criticalFile);
                if (File.Exists(filePath))
                {
                    if (IsFileLocked(filePath))
                    {
                        lockedFiles.Add(criticalFile);
                        _logger.Warning("  ❌ LOCKED: {File} (in use by another process)", criticalFile);
                    }
                    else
                    {
                        _logger.Information("  ✅ Available: {File}", criticalFile);
                    }
                }
                else
                {
                    _logger.Information("  ℹ️  Not found: {File} (will be created)", criticalFile);
                }
            }

            if (lockedFiles.Any())
            {
                _logger.Error("========================================");
                _logger.Error("CANNOT APPLY PATCH - FILES ARE LOCKED");
                _logger.Error("========================================");
                _logger.Error("The following files are in use: {Files}", string.Join(", ", lockedFiles));
                _logger.Error("Please close Asheron's Call and any related processes:");
                _logger.Error("  • acclient.exe");
                _logger.Error("  • Decal");
                _logger.Error("  • Any file managers browsing the AC directory");
                _logger.Error("========================================");
                result.ErrorMessage = $"Files are locked: {string.Join(", ", lockedFiles)}. Close Asheron's Call and try again.";
                return result;
            }

            _logger.Information("All critical files are available for patching");

            // Create backup directory AFTER confirming files are not locked
            var backupDir = Path.Combine(installDir, "Backup_PreEoRPatch");
            Directory.CreateDirectory(backupDir);

            // Backup ALL files that will be overwritten
            _logger.Information("========================================");
            _logger.Information("BACKING UP EXISTING FILES");
            _logger.Information("========================================");
            var backupResult = await BackupExistingFilesAsync(tempExtractDir, installDir, backupDir, cancellationToken);

            if (!backupResult.Success)
            {
                _logger.Error("Backup failed: {Error}", backupResult.ErrorMessage);
                result.ErrorMessage = $"Backup failed: {backupResult.ErrorMessage}";
                return result;
            }

            _logger.Information("Backed up {Count} files", backupResult.FilesCopied);
            _logger.Information("========================================");

            // Copy all files from extracted patch to AC directory
            _logger.Information("========================================");
            _logger.Information("COPYING PATCH FILES");
            _logger.Information("Source: {Source}", tempExtractDir);
            _logger.Information("Destination: {Dest}", installDir);
            _logger.Information("========================================");

            var copyResult = await CopyDirectoryAsync(tempExtractDir, installDir, overwrite: true, cancellationToken);

            if (!copyResult.Success)
            {
                _logger.Error("========================================");
                _logger.Error("PATCH COPY FAILED - RESTORING BACKUPS");
                _logger.Error("========================================");
                _logger.Error("Error: {Error}", copyResult.ErrorMessage);

                // Restore from backup
                var restoreResult = await RestoreFromBackupAsync(backupDir, installDir, cancellationToken);

                if (restoreResult.Success)
                {
                    _logger.Information("Successfully restored {Count} files from backup", restoreResult.FilesCopied);
                    result.ErrorMessage = $"Patch installation failed: {copyResult.ErrorMessage}. Original files have been restored.";
                }
                else
                {
                    _logger.Error("Failed to restore from backup: {Error}", restoreResult.ErrorMessage);
                    result.ErrorMessage = $"Patch installation failed AND backup restoration failed. Game may be in an inconsistent state. Error: {copyResult.ErrorMessage}";
                }

                _logger.Information("========================================");
                return result;
            }

            _logger.Information("========================================");
            _logger.Information("PATCH COPY COMPLETE");
            _logger.Information("Files copied: {Count}", copyResult.FilesCopied);
            _logger.Information("Files failed: {Count}", copyResult.FilesFailed);
            _logger.Information("========================================");

            // Create marker file to indicate patch was applied
            var markerPath = Path.Combine(installDir, PatchMarkerFileName);
            await File.WriteAllTextAsync(markerPath,
                $"End of Retail patch applied on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                cancellationToken);

            _logger.Information("End of Retail patch applied successfully");

            // Cleanup temp directory and backup on success
            try
            {
                _logger.Information("Cleaning up temporary files...");
                Directory.Delete(tempExtractDir, true);
                _logger.Information("Cleaned up extraction directory: {Dir}", tempExtractDir);
            }
            catch (Exception ex)
            {
                _logger.Warning("Could not delete temp extraction directory: {Error}", ex.Message);
            }

            result.Success = true;
            result.ExtractedFilesPath = null; // Clear path since we cleaned up
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to apply patch: {Error}", ex.Message);
            _logger.Error("Exception: {Exception}", ex);
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            // Don't delete tempExtractDir so user can manually install
            return result;
        }
    }

    /// <summary>
    /// Backs up all files in the destination that will be overwritten by the patch.
    /// </summary>
    private async Task<CopyDirectoryResult> BackupExistingFilesAsync(
        string sourceDir,
        string destDir,
        string backupDir,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var result = new CopyDirectoryResult { Success = true };

            try
            {
                // Get all files that will be copied from source
                var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

                foreach (var sourceFile in sourceFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Calculate relative path
                    var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                    var destFile = Path.Combine(destDir, relativePath);
                    var backupFile = Path.Combine(backupDir, relativePath);

                    // Only backup if file exists in destination (will be overwritten)
                    if (File.Exists(destFile))
                    {
                        try
                        {
                            var backupFileDir = Path.GetDirectoryName(backupFile);
                            if (!string.IsNullOrEmpty(backupFileDir))
                            {
                                Directory.CreateDirectory(backupFileDir);
                            }

                            var fileSize = new FileInfo(destFile).Length;
                            _logger.Information("Backing up: {File} ({Size:N0} bytes)", relativePath, fileSize);

                            File.Copy(destFile, backupFile, overwrite: true);

                            // Verify backup
                            if (File.Exists(backupFile) && new FileInfo(backupFile).Length == fileSize)
                            {
                                _logger.Debug("  ✅ Backup verified: {File}", relativePath);
                                result.FilesCopied++;
                            }
                            else
                            {
                                _logger.Error("  ❌ Backup verification failed: {File}", relativePath);
                                result.FilesFailed++;
                                result.Success = false;
                                result.ErrorMessage = $"Backup verification failed for {relativePath}";
                                return result;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Failed to backup {File}: {Error}", relativePath, ex.Message);
                            result.FilesFailed++;
                            result.Success = false;
                            result.ErrorMessage = $"Failed to backup {relativePath}: {ex.Message}";
                            return result;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error("Backup operation failed: {Error}", ex.Message);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Restores files from backup directory to the installation directory.
    /// </summary>
    private async Task<CopyDirectoryResult> RestoreFromBackupAsync(
        string backupDir,
        string destDir,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var result = new CopyDirectoryResult { Success = true };

            try
            {
                if (!Directory.Exists(backupDir))
                {
                    _logger.Warning("Backup directory does not exist: {Dir}", backupDir);
                    result.ErrorMessage = "Backup directory not found";
                    result.Success = false;
                    return result;
                }

                var backupFiles = Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories);
                _logger.Information("Restoring {Count} files from backup...", backupFiles.Length);

                foreach (var backupFile in backupFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(backupDir, backupFile);
                    var destFile = Path.Combine(destDir, relativePath);

                    try
                    {
                        _logger.Information("Restoring: {File}", relativePath);

                        // Remove read-only attribute if present
                        if (File.Exists(destFile))
                        {
                            var attributes = File.GetAttributes(destFile);
                            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            {
                                File.SetAttributes(destFile, attributes & ~FileAttributes.ReadOnly);
                            }
                        }

                        File.Copy(backupFile, destFile, overwrite: true);
                        result.FilesCopied++;
                        _logger.Information("  ✅ Restored: {File}", relativePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Failed to restore {File}: {Error}", relativePath, ex.Message);
                        result.FilesFailed++;
                        // Continue trying to restore other files
                    }
                }

                if (result.FilesFailed > 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to restore {result.FilesFailed} files";
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error("Restore operation failed: {Error}", ex.Message);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Result of a directory copy operation.
    /// </summary>
    private class CopyDirectoryResult
    {
        public bool Success { get; set; }
        public int FilesCopied { get; set; }
        public int FilesFailed { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Recursively copies a directory and all its contents.
    /// </summary>
    private async Task<CopyDirectoryResult> CopyDirectoryAsync(
        string sourceDir,
        string destDir,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var result = new CopyDirectoryResult { Success = true };

        try
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destDir, fileName);

                try
                {
                    // Get file info before copy
                    var sourceFileInfo = new FileInfo(file);
                    var sourceSize = sourceFileInfo.Length;
                    var sourceModified = sourceFileInfo.LastWriteTimeUtc;

                    // Check if destination exists
                    var destExists = File.Exists(destFile);
                    long? destSizeBefore = destExists ? new FileInfo(destFile).Length : null;

                    _logger.Information("Copying: {File} ({Size:N0} bytes)", fileName, sourceSize);
                    if (destExists)
                    {
                        _logger.Information("  Overwriting existing file (was {OldSize:N0} bytes)", destSizeBefore);

                        // Remove read-only attribute if present
                        var attributes = File.GetAttributes(destFile);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            _logger.Warning("  File is read-only, removing attribute: {File}", fileName);
                            File.SetAttributes(destFile, attributes & ~FileAttributes.ReadOnly);
                        }
                    }

                    // Perform the copy
                    await Task.Run(() => File.Copy(file, destFile, overwrite), cancellationToken);

                    // Verify the copy succeeded
                    if (File.Exists(destFile))
                    {
                        var copiedFileInfo = new FileInfo(destFile);
                        var copiedSize = copiedFileInfo.Length;

                        if (copiedSize == sourceSize)
                        {
                            _logger.Information("  ✅ Successfully copied and verified: {File}", fileName);
                            result.FilesCopied++;
                        }
                        else
                        {
                            _logger.Error("  ❌ File size mismatch after copy: {File} (expected {Expected:N0}, got {Actual:N0})",
                                fileName, sourceSize, copiedSize);
                            result.FilesFailed++;
                            result.Success = false;
                            result.ErrorMessage = $"File size mismatch: {fileName}";
                        }
                    }
                    else
                    {
                        _logger.Error("  ❌ File does not exist after copy: {File}", fileName);
                        result.FilesFailed++;
                        result.Success = false;
                        result.ErrorMessage = $"File not found after copy: {fileName}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("  ❌ Failed to copy {File}: {Error}", fileName, ex.Message);
                    _logger.Error("     Exception type: {Type}", ex.GetType().Name);
                    if (ex.InnerException != null)
                    {
                        _logger.Error("     Inner exception: {Inner}", ex.InnerException.Message);
                    }

                    result.FilesFailed++;
                    result.Success = false;
                    result.ErrorMessage = $"Failed to copy {fileName}: {ex.Message}";

                    // Don't continue if we're failing to copy files
                    return result;
                }
            }

            // Recursively copy subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                var destSubDir = Path.Combine(destDir, dirName);

                _logger.Information("Entering subdirectory: {Dir}", dirName);
                var subResult = await CopyDirectoryAsync(subDir, destSubDir, overwrite, cancellationToken);

                result.FilesCopied += subResult.FilesCopied;
                result.FilesFailed += subResult.FilesFailed;

                if (!subResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = subResult.ErrorMessage;
                    return result;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error("Directory copy operation failed: {Error}", ex.Message);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Checks if a file is locked (in use by another process).
    /// </summary>
    private bool IsFileLocked(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Helper method for developers: Downloads patch and calculates MD5 hash.
    /// This should be run once to obtain the hash value for the ExpectedMd5Hash constant.
    /// The hash will be logged to the console and log file.
    /// </summary>
    /// <returns>The calculated MD5 hash as a hex string</returns>
    public async Task<string> CalculateAndLogPatchHashAsync()
    {
        _logger.Information("========================================");
        _logger.Information("DEVELOPER TOOL: Calculating Patch MD5 Hash");
        _logger.Information("========================================");

        try
        {
            _logger.Information("Downloading patch from Mega.nz...");
            var patchFilePath = await DownloadPatchAsync();

            _logger.Information("Patch downloaded to: {Path}", patchFilePath);
            _logger.Information("Calculating MD5 hash...");

            using var md5 = MD5.Create();
            using var stream = File.OpenRead(patchFilePath);
            var hashBytes = await md5.ComputeHashAsync(stream);
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            _logger.Information("========================================");
            _logger.Information("PATCH MD5 HASH CALCULATED:");
            _logger.Information("  Hash: {Hash}", hash);
            _logger.Information("========================================");
            _logger.Information("NEXT STEPS:");
            _logger.Information("1. Copy the hash value above");
            _logger.Information("2. Open PatchService.cs");
            _logger.Information("3. Update the ExpectedMd5Hash constant (line ~42):");
            _logger.Information("   FROM: private const string ExpectedMd5Hash = \"NEEDS_CALCULATION\";");
            _logger.Information("   TO:   private const string ExpectedMd5Hash = \"{Hash}\";", hash);
            _logger.Information("4. Save the file and rebuild");
            _logger.Information("========================================");

            // Clean up downloaded file
            try
            {
                File.Delete(patchFilePath);
                _logger.Debug("Deleted temporary patch file");
            }
            catch (Exception ex)
            {
                _logger.Warning("Could not delete temporary file: {Error}", ex.Message);
            }

            return hash;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to calculate patch hash: {Error}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Downloads, validates, and applies the End of Retail patch in one operation.
    /// </summary>
    /// <param name="acClientPath">Path to acclient.exe</param>
    /// <param name="progressCallback">Optional callback for progress reporting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PatchResult with success status and file paths</returns>
    public async Task<PatchResult> DownloadAndApplyPatchAsync(
        string acClientPath,
        DownloadProgressCallback? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new PatchResult { Success = false };

        try
        {
            // 1. Download patch
            var patchFilePath = await DownloadPatchAsync(progressCallback, cancellationToken);

            // 2. Validate patch (recommended for security)
            // Hash is configured - validate it
            if (!await ValidatePatchAsync(patchFilePath))
            {
                _logger.Error("Patch validation failed - MD5 hash mismatch. Aborting installation.");
                _logger.Error("The downloaded patch file may be corrupted or tampered with.");
                result.ErrorMessage = "Patch validation failed - MD5 hash mismatch. The downloaded file may be corrupted.";
                return result;
            }

            // 3. Apply patch
            result = await ApplyPatchAsync(patchFilePath, acClientPath, cancellationToken);

            if (!result.Success)
            {
                _logger.Error("Patch application failed: {Error}", result.ErrorMessage);
                return result;
            }

            // 4. Cleanup downloaded file (only on success)
            try
            {
                File.Delete(patchFilePath);
                _logger.Debug("Deleted temporary patch file: {Path}", patchFilePath);
            }
            catch (Exception ex)
            {
                _logger.Warning("Failed to delete temporary patch file: {Error}", ex.Message);
                // Non-critical, continue
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to download and apply patch: {Error}", ex.Message);
            result.ErrorMessage = $"Failed to download and apply patch: {ex.Message}";
            return result;
        }
    }
}
