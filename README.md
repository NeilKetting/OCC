# OCC Client Application

**Powered by Origize63**

This is the official client application for OCC, built with Avalonia UI and .NET 9.

## features

- **Cross-Platform**: Runs on Windows, macOS, and Linux.
- **Auto-Updating**: Seamless background updates powered by Velopack.
- **Modern UI**: High-performance Fluent design.

## Development Setup

1.  **Prerequisites**:
    - .NET 9 SDK
    - Visual Studio 2022 or JetBrains Rider or VS Code

2.  **Building**:
    ```bash
    dotnet build
    ```

## Creating a Release

To build a new version and installer for clients:

1.  Open PowerShell in the project root.
2.  Run the publish script:
    ```powershell
    .\publish-desktop.ps1 -Version 1.0.0
    ```
3.  The installer (`Setup.exe`) will be generated in the `Releases` folder.

## License

Copyright (c) 2026 Origize63. All rights reserved.
Proprietary and Confidential. Unauthorized copying of this project, via any medium, is strictly prohibited.
