# Contributing to OldPortal Launcher

First off, thank you for considering contributing to OldPortal Launcher! It's people like you that make this project better for the Asheron's Call community.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Coding Standards](#coding-standards)
- [Commit Guidelines](#commit-guidelines)
- [Pull Request Process](#pull-request-process)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Features](#suggesting-features)

## Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Getting Started

### Prerequisites

- **Operating System**: Windows 10/11
- **.NET SDK**: .NET 9.0 SDK
- **IDE**: Visual Studio 2022, Visual Studio Code, or JetBrains Rider
- **Git**: For version control
- **InnoSetup 6.6.1+**: For building installers (optional) - [Download](https://jrsoftware.org/isdl.php)

### Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/oplauncher.git
   cd oplauncher
   ```

2. **Open in Visual Studio** (recommended)
   - Double-click `OPLauncher.sln`
   - Visual Studio will automatically restore dependencies
   - Press F5 to build and run with debugging

   **Or use command line:**

3. **Restore dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the project**
   ```bash
   dotnet build
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

6. **Run with hot reload (recommended for CLI development)**
   ```bash
   dotnet watch run
   ```

## How to Contribute

### Reporting Bugs

Please report bugs on our community forum:

**Bug Reports Forum**: https://oldportal.com/community/category/bug-reports

Before creating a bug report, please check existing forum threads to avoid duplicates. When creating a bug report, include:

- **Clear title and description**
- **Steps to reproduce** the issue
- **Expected behavior** vs **actual behavior**
- **Screenshots** (if applicable)
- **Environment details** (OS version, launcher version, .NET version)
- **Log files** from `%LOCALAPPDATA%\OldPortal\launcher\logs\`

### Suggesting Features

Feature suggestions are welcome! Please post them on our community forum:

**Feature Requests**: https://oldportal.com/community/category/bug-reports

When suggesting features:

- **Check existing forum threads** first to avoid duplicates
- **Provide a clear use case** for the feature
- **Explain how it benefits users**
- **Consider implementation complexity**

### Contributing Code

1. **Fork the repository**
2. **Create a feature branch** from `main`
   ```bash
   git checkout -b feature/amazing-feature
   ```
3. **Make your changes**
4. **Test thoroughly**
5. **Commit your changes** (see [Commit Guidelines](#commit-guidelines))
6. **Push to your fork**
   ```bash
   git push origin feature/amazing-feature
   ```
7. **Open a Pull Request**

## Coding Standards

### Architecture

- **Pattern**: Strictly follow **MVVM** (Model-View-ViewModel)
- **ViewModels**: Use `CommunityToolkit.Mvvm` and `ReactiveUI` features
- **Services**: Business logic goes in service classes
- **Separation**: Keep UI logic in Views/ViewModels, business logic in Services

### Code Style

- **Language**: C# 12 with .NET 9 features
- **Formatting**: Follow standard C# conventions
  - PascalCase for public members
  - camelCase for private fields (with `_` prefix)
  - UPPERCASE for constants
- **Async/Await**: Use `async`/`await` for all I/O operations
- **Null Safety**: Leverage nullable reference types
- **XAML**: Use Avalonia UI best practices

### Documentation

- **XML Comments**: Add XML documentation for all public APIs
  ```csharp
  /// <summary>
  /// Launches the game with the specified connection details.
  /// </summary>
  /// <param name="connection">The world connection information.</param>
  /// <returns>A LaunchResult indicating success or failure.</returns>
  public async Task<LaunchResult> LaunchGameAsync(WorldConnectionDto connection)
  ```

- **Code Comments**: Use for complex logic only, prefer self-documenting code
- **README Updates**: Update documentation for user-facing changes

### Security Requirements

**Critical**: All contributions MUST adhere to these security rules:

- ✅ **Encrypt credentials** using DPAPI with `CurrentUser` scope
- ✅ **Never persist JWTs** to disk (memory only)
- ✅ **Never log secrets** in plaintext (passwords, tokens, API keys)
- ✅ **Use HTTPS** for all API communication
- ✅ **Sanitize user input** before use
- ✅ **Clean up sensitive data** after use (especially in GameLaunchService)

See [SECURITY.md](SECURITY.md) for details.

### Testing

- **Manual Testing**: Test your changes with:
  - Fresh installation (no cached data)
  - Existing installation (with cached data)
  - Multiple world types (PvE, PvP, RP, etc.)
  - With and without Decal

- **Edge Cases**: Consider:
  - Offline mode (no internet)
  - Invalid credentials
  - Server offline/unreachable
  - Corrupted cache database

- **Future**: Unit tests and integration tests are planned

## Commit Guidelines

### Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Type**: One of the following:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting, missing semicolons, etc.
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `perf`: Performance improvement
- `test`: Adding tests
- `chore`: Maintenance (build scripts, dependencies, etc.)

**Scope** (optional): Area of change (e.g., `auth`, `worlds`, `ui`, `installer`)

**Subject**: Short description (50 chars or less)

**Examples**:
```
feat(worlds): add search and filter functionality

Implemented debounced search and ruleset filtering for Browse Worlds screen.
Search queries are sanitized and cached results are rebuilt after filtering.

Closes #123
```

```
fix(auth): prevent token refresh loop on 401

Added check to prevent infinite refresh loop when refresh token is expired.
Now properly logs out user and shows login screen.

Fixes #456
```

## Pull Request Process

1. **Update documentation** if needed (README, docs/, etc.)
2. **Update version** in `OPLauncher.csproj` and `installer.iss` if applicable
3. **Ensure PR description** clearly describes the problem and solution
4. **Link related issues** using "Closes #123" or "Fixes #456"
5. **Request review** from maintainers
6. **Address review comments** promptly
7. **Squash commits** if requested before merge

### PR Checklist

Use this checklist in your PR:

- [ ] Code follows MVVM architecture
- [ ] No hardcoded secrets or credentials
- [ ] All async operations use `async`/`await`
- [ ] XML documentation added for public APIs
- [ ] Tested locally with multiple scenarios
- [ ] No breaking changes (or clearly documented)
- [ ] README/docs updated if needed
- [ ] Commit messages follow guidelines

## Project Structure

```
OPLauncher/
├── Assets/              # Images, icons, resources
├── Controls/            # Reusable UI controls
├── Converters/          # XAML value converters
├── DTOs/                # Data Transfer Objects (API models)
├── Models/              # Local data models
├── Services/            # Business logic and API wrappers
├── Utilities/           # Helper classes
├── ViewModels/          # MVVM ViewModels
├── Views/               # XAML UI views
├── Styles/              # Global styles
├── Themes/              # Color themes
├── docs/                # Documentation
└── scripts/             # Build and utility scripts
```

## Resources

- **Documentation**: See [docs/](docs/) folder
- **Deep Links**: [docs/DEEP_LINKS.md](docs/DEEP_LINKS.md)
- **Versioning**: [docs/VERSIONING.md](docs/VERSIONING.md)
- **Avalonia UI**: https://avaloniaui.net/
- **.NET 9**: https://learn.microsoft.com/en-us/dotnet/

## Questions?

Feel free to ask questions on our [Community Forum](https://oldportal.com/community/category/bug-reports) or reach out on our [Discord server](https://discord.gg/UKdy2b9zBe).

## License

By contributing, you agree that your contributions will be licensed under the same [MIT License](LICENSE) that covers this project.

---

**Thank you for making OldPortal Launcher better!** 🎉
