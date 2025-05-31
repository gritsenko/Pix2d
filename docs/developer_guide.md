# Pix2D Developer Guide

## Architecture Overview

Pix2D is built on a modular, plugin-based architecture that enables extensibility and maintainability. Here's a high-level overview of the key components:

### Core Components

1. **Core Engine**
   - `Pix2d.Core`: Main application logic and rendering engine using SkiaSharp
   - `SkiaNodes`: Custom scene graph implementation for efficient rendering
   - `Pix2d.Infrastructure`: Base interfaces and utilities

2. **Plugin System**
   - Extensible architecture through `IPix2dPlugin` interface
   - Core plugins for essential functionality (sprites, file formats, etc.)
   - Support for custom plugins to extend functionality

3. **UI Layer**
   - Built with Avalonia UI framework
   - MVVM architecture with message-based communication
   - Platform-agnostic UI components

4. **File System**
   - Abstract file system interface (`IFileContentSource`)
   - Platform-specific implementations for different targets
   - Support for custom file formats and import/export

### Key Services

- `IProjectService`: Project management and file operations
- `IDrawingService`: Drawing and rendering operations
- `IToolService`: Tool management and interaction
- `IExportService`: Export functionality for various formats
- Various platform-specific services for different environments

## Build Instructions

### Prerequisites

- .NET SDK 7.0 or later
- Visual Studio 2022 or VS Code with C# extensions
- Android SDK (for Android builds)
- Node.js and npm (for web version)

### Windows Build

1. Clone the repository:
   ```bash
   git clone https://github.com/your-repo/pix2d.git
   cd pix2d
   ```

2. Build the solution:
   ```bash
   dotnet restore Sources/Pix2d.sln
   dotnet build Sources/Pix2d.sln
   ```

3. Run the Windows version:
   ```bash
   dotnet run --project Sources/Heads/Pix2d.Windows
   ```

### Linux Build

1. Install prerequisites:
   ```bash
   sudo apt-get update
   sudo apt-get install dotnet-sdk-7.0
   ```

2. Build and run:
   ```bash
   dotnet restore Sources/Pix2d.sln
   dotnet build Sources/Pix2d.sln
   dotnet run --project Sources/Heads/Pix2d.Linux
   ```

### Android Build

1. Install Android SDK and set ANDROID_HOME

2. Build the Android project:
   ```bash
   dotnet restore Sources/Heads/Pix2d.Droid
   dotnet build Sources/Heads/Pix2d.Droid
   ```

3. Deploy to device/emulator:
   ```bash
   dotnet build Sources/Heads/Pix2d.Droid -t:Install
   ```

### Web Version

1. Navigate to Browser project:
   ```bash
   cd Sources/Heads/Pix2d.Browser
   ```

2. Build and serve:
   ```bash
   dotnet publish -c Release
   dotnet run
   ```

## Testing

Run unit tests:
```bash
dotnet test Sources/Tests
```

## Project Format

For detailed information about the Pix2d project format (.pix2d), please refer to the [Project Format Documentation](project_format.md).