# Security Policy

## Supported Versions

We release security updates for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Security Architecture

### Credential Storage

OldPortal Launcher uses **Windows Data Protection API (DPAPI)** to securely store sensitive data:

- **Encryption Scope**: `CurrentUser` - credentials are encrypted per Windows user account
- **Encrypted Data**:
  - ACE account passwords (saved in credential vault)
  - OldPortal refresh tokens (for persistent login)
- **Non-Exportable**: Encrypted data cannot be decrypted by other Windows users or on other machines
- **Storage Location**: `%LOCALAPPDATA%\OldPortal\launcher\cache.db` (LiteDB database)

### Token Management

- **Access Tokens (JWT)**: Stored **in memory only**, never persisted to disk
- **Refresh Tokens**: Encrypted with DPAPI and stored in local database
- **Token Lifetime**: Short-lived access tokens (15 minutes), long-lived refresh tokens (7 days)
- **Automatic Refresh**: Access tokens are refreshed automatically before expiration

### Network Security

- **HTTPS Only**: All API communication uses HTTPS with SSL certificate validation
- **API Endpoint**: `https://oldportal.com/api/v1`
- **Certificate Validation**: Enforced (rejects self-signed certificates)
- **No Man-in-the-Middle**: TLS 1.2+ required

### Logging Security

- **No Plaintext Secrets**: Passwords, tokens, and API keys are NEVER logged
- **Redacted Logging**: Sensitive fields are redacted even in Debug level
- **Log Location**: `%LOCALAPPDATA%\OldPortal\launcher\logs\`
- **Log Rotation**: Automatically rotates logs daily, keeps last 30 days

### Game Launch Security

The launcher passes credentials to the AC client process:

- **Memory Cleanup**: Password strings are cleared after use
- **Process Isolation**: Each game instance runs in its own process
- **No Persistence**: Credentials are not written to disk during launch

## Known Security Limitations

### 1. Decal Injection

The launcher supports Decal plugin framework via DLL injection (`injector.dll`):

- **User Choice**: Decal is optional, disabled by default
- **Trusted DLL**: `injector.dll` is included in the installer
- **Antivirus False Positives**: DLL injection may trigger antivirus warnings
- **Mitigation**: Users can verify DLL hash and whitelist the launcher

### 2. Local Database Encryption

The LiteDB cache database is **not encrypted at rest**:

- **Stored Data**: Cached world list, user preferences, configuration
- **Not Stored**: Plaintext passwords (encrypted with DPAPI before storage)
- **Risk**: Low - no sensitive plaintext data
- **Optional Encryption**: Can be enabled via `DatabasePassword` config option

### 3. No Code Signing

The launcher executable is **not code-signed**:

- **SmartScreen Warning**: Windows may show "Unknown Publisher" warning on first install
- **User Action Required**: Users must click "More info" → "Run anyway"
- **Mitigation**: Verify installer hash from official download page
- **Future Plan**: Obtain code signing certificate for production releases

## Reporting a Vulnerability

**Please DO NOT report security vulnerabilities through public GitHub issues.**

Instead, please report them via email to: **security@oldportal.com**

You should receive a response within 48 hours. If the issue is confirmed, we will:

1. **Acknowledge** receipt of your report
2. **Investigate** the vulnerability
3. **Develop** a fix
4. **Release** a security update
5. **Credit** you in the changelog (if desired)

### What to Include

When reporting a vulnerability, please include:

- **Description** of the vulnerability
- **Steps to reproduce** the issue
- **Potential impact** (e.g., credential theft, privilege escalation)
- **Suggested fix** (if any)
- **Your contact information** for follow-up

### Disclosure Policy

- **Responsible Disclosure**: We ask that you do not publicly disclose the vulnerability until we have released a fix
- **Coordinated Release**: We will coordinate with you on the disclosure timeline
- **Public Credit**: We will credit you in the security advisory (unless you prefer to remain anonymous)

## Security Best Practices for Users

### 1. Keep Software Updated

- **Auto-Check**: Enable "Check for updates on startup" in Settings
- **Manual Check**: Click "Check for Updates" in Settings regularly
- **Download Source**: Only download from official sources (oldportal.com/downloads)

### 2. Verify Downloads

Before installing, verify the SHA-256 hash of the installer:

```powershell
Get-FileHash .\OPLauncher-Setup.exe -Algorithm SHA256
```

Compare with the hash published on the official download page.

### 3. Secure Your Windows Account

- **Strong Password**: Use a strong Windows account password
- **Account Protection**: DPAPI encryption is only as strong as your Windows account
- **Lock Screen**: Lock your PC when away (credentials remain encrypted but accessible to your account)

### 4. Review Saved Credentials

- **Credential Vault**: Regularly review saved credentials in Settings
- **Remove Unused**: Delete credentials for accounts you no longer use
- **Update Passwords**: Update saved passwords when you change them on the server

### 5. Antivirus Compatibility

If your antivirus blocks the launcher:

1. **Verify Legitimacy**: Check installer hash against official website
2. **Whitelist**: Add launcher installation directory to antivirus exclusions
3. **Report False Positive**: Report to your antivirus vendor if it's a false positive

### 6. Decal Safety

If using Decal:

- **Official Source**: Only download Decal from decaldev.com
- **Verify Plugins**: Only use trusted Decal plugins from known sources
- **Disable if Unsure**: You can disable Decal per-launch if you're concerned

## Security Audits

- **Last Audit**: January 2026 (internal review)
- **External Audit**: Not yet conducted
- **Continuous Review**: Security is reviewed with each major release

## Compliance

- **GDPR**: User data is stored locally only, not transmitted except for API authentication
- **Data Collection**: No telemetry or analytics collected by default (opt-in only)
- **Third-Party Services**: Only communicates with oldportal.com API

## Security Roadmap

Future security improvements:

- [ ] Code signing certificate for releases
- [ ] Optional 2FA support for OldPortal accounts
- [ ] Encrypted local database (opt-in)
- [ ] Security audit by external firm
- [ ] Vulnerability disclosure program
- [ ] Automated security scanning (Dependabot, CodeQL)

## Contact

- **Security Email**: security@oldportal.com
- **General Support**: support@oldportal.com
- **Website**: https://oldportal.com

## Acknowledgments

We would like to thank the following people for responsibly disclosing security issues:

- *No security issues reported yet*

---

**Last Updated**: January 2026
