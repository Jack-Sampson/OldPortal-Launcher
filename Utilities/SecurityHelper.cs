// Component: OPLauncher
using System.Runtime.Versioning;
// TODO: [LAUNCH-Migration] Create security helper for DPAPI encryption
// Description: Utility for encrypting/decrypting sensitive data using Windows DPAPI

using System.Security.Cryptography;
using System.Text;

namespace OPLauncher.Utilities;

/// <summary>
/// Security helper for encrypting and decrypting sensitive data using Windows DPAPI
/// </summary>
[SupportedOSPlatform("windows")]
public static class SecurityHelper
{
    /// <summary>
    /// Encrypts a string using Windows DPAPI (Data Protection API)
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <returns>Base64-encoded encrypted string</returns>
    public static string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null, // No additional entropy
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encryptedBytes);
        }
        catch (CryptographicException)
        {
            // Return empty string if encryption fails
            return string.Empty;
        }
    }

    /// <summary>
    /// Decrypts a DPAPI-encrypted string
    /// </summary>
    /// <param name="encryptedText">Base64-encoded encrypted string</param>
    /// <returns>Decrypted plain text, or empty string if decryption fails</returns>
    public static string DecryptString(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null, // No additional entropy
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            // Return empty string if decryption fails
            return string.Empty;
        }
        catch (FormatException)
        {
            // Invalid base64 string
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks if a string is encrypted (basic check for base64 format)
    /// </summary>
    /// <param name="text">The text to check</param>
    /// <returns>True if the text appears to be encrypted (base64), false otherwise</returns>
    public static bool IsEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        try
        {
            Convert.FromBase64String(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
