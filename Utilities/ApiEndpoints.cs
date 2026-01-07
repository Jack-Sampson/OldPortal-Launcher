// Component: OPLauncher
// TODO: [LAUNCH-Migration] Create API endpoints helper
// Description: Centralized API endpoint URLs

namespace OPLauncher.Utilities;

/// <summary>
/// Centralized API endpoint URLs for OldPortal API
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// Base API URL (configured in appsettings.json)
    /// </summary>
    public static string BaseUrl { get; set; } = "https://oldportal.com/api/v1";

    /// <summary>
    /// Authentication endpoints
    /// </summary>
    public static class Auth
    {
        public static string Login => $"{BaseUrl}/auth/login";
        public static string Logout => $"{BaseUrl}/auth/logout";
        public static string Refresh => $"{BaseUrl}/auth/refresh";
        public static string RefreshToken => $"{BaseUrl}/auth/refresh";
        public static string Register => $"{BaseUrl}/auth/register";
        public static string Profile => $"{BaseUrl}/auth/profile";
        public static string VerifyMfa => $"{BaseUrl}/auth/verify-mfa";
        public static string MfaVerify => $"{BaseUrl}/auth/verify-mfa";
    }

    /// <summary>
    /// World/Server endpoints
    /// </summary>
    public static class Worlds
    {
        public static string GetAll => $"{BaseUrl}/servers";
        public static string List => $"{BaseUrl}/servers";
        public static string Featured => $"{BaseUrl}/servers?featured=true";
        public static string GetById(Guid id) => $"{BaseUrl}/servers/{id}";
    }

    /// <summary>
    /// User profile endpoints
    /// </summary>
    public static class User
    {
        public static string Profile => $"{BaseUrl}/auth/profile";
        public static string UpdateProfile => $"{BaseUrl}/auth/profile";
    }

    /// <summary>
    /// News endpoints
    /// </summary>
    public static class News
    {
        public static string GetAll => $"{BaseUrl}/news";
        public static string GetById(Guid id) => $"{BaseUrl}/news/{id}";
        public static string GetWithLimit(int limit) => $"{BaseUrl}/news?limit={limit}";
    }

    /// <summary>
    /// Launcher update endpoints
    /// </summary>
    public static class Launcher
    {
        public static string CheckVersion => $"{BaseUrl}/launcher/version";
        public static string Download => $"{BaseUrl}/launcher/download";
    }
}
