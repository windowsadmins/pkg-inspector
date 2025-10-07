# Package Inspector

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue)](https://www.microsoft.com/windows)

A modern Windows package inspection tool with both GUI and CLI interfaces for examining `.pkg` and `.nupkg` files.

Originally part of the [sbin-installer](https://github.com/windowsadmins/sbin-installer) project, Package Inspector is now a standalone tool for inspecting package contents.

## Features

### Unified Binary Architecture
- **Single Executable** - One binary intelligently switches between GUI and CLI modes
- **Self-Contained** - All dependencies included, no external runtime required
- **Cross-Architecture** - Native support for both x64 and ARM64 Windows systems
- **Simplified Distribution** - One binary to maintain and sign
- **Consistent Behavior** - Same package inspection logic in both modes

### GUI Features
- **Modern WPF Interface** - Clean, intuitive design inspired by macOS Suspicious Package
- **Automatic Theme Detection** - Adapts to Windows light/dark mode preferences
- **Drag & Drop Support** - Simply drag package files into the window
- **Package Overview** - View metadata including name, version, author, license, and dependencies
- **File Browser** - Navigate through all files in the package payload with a tree view
- **Script Inspector** - View and analyze pre-install and post-install PowerShell scripts
- **Syntax Highlighting** - Code highlighting for PowerShell scripts and YAML metadata
- **Export Functionality** - Export entire packages or individual files/scripts
- **Digital Signature Verification** - Check if packages are digitally signed

### CLI Features
- **Scripting Support** - Automate package inspection in scripts and CI/CD pipelines
- **Quiet Mode** - Minimal output for scripting and automation
- **Signature Verification** - Show package signature information from command line
- **Flexible Navigation** - Open GUI with specific tab or file pre-selected
- **Batch Processing** - Process multiple packages in automated workflows

## Installation

### From .pkg Package

Download the latest `.pkg` file from the [releases page](https://github.com/windowsadmins/pkg-inspector/releases) and install using sbin-installer:

```powershell
# Using sbin-installer (cimipkg)
installer --pkg pkginspector-x64-<version>.pkg --target /
```

This installs to `C:\Program Files\PkgInspector` and adds it to your system PATH.

After installation, restart your shell and run from any location:

```powershell
pkginspector              # Open GUI
pkginspector --help       # Show CLI help
```

### Manual Installation

1. Download the appropriate build for your architecture from the releases page
2. Extract the contents to a directory of your choice (e.g., `C:\Program Files\PkgInspector`)
3. Add the installation directory to your PATH environment variable
4. Optionally create a desktop shortcut to `pkginspector.exe`

### From Source

```powershell
# Clone repository
git clone https://github.com/windowsadmins/pkg-inspector.git
cd pkg-inspector

# Build (both architectures, signed, with .pkg packages)
.\build.ps1

# Build for specific architecture
.\build.ps1 -Architecture x64
.\build.ps1 -Architecture arm64

# Build without creating .pkg packages
.\build.ps1 -SkipPkg

# List available code signing certificates
.\build.ps1 -ListCerts
```

Build outputs:
- **Executable**: `.\build\x64\pkginspector.exe` and `.\build\arm64\pkginspector.exe`
- **Packages**: `.\build\pkginspector-x64-<version>.pkg` and `.\build\pkginspector-arm64-<version>.pkg`

All executables are automatically code-signed if certificates are available.

## Usage

### GUI Mode

Launch the application and interact visually:

```powershell
pkginspector                    # Open empty window
pkginspector MyPackage.pkg      # Open specific package
```

Or simply drag and drop a package file onto the window.

Navigate through tabs:
- **Package Info** - Overview and installation details
- **All Files** - Tree view of payload files
- **All Scripts** - Pre/post-install scripts
- **Metadata** - Raw YAML metadata

### CLI Mode

The same executable provides full command-line functionality:

```powershell
# Show help
pkginspector --help

# Show package signature information
pkginspector -g MyPackage.pkg

# Open package with specific tab
pkginspector -s MyPackage.pkg           # Scripts tab
pkginspector -f "path/to/file" MyPackage.pkg  # Navigate to file

# Quiet mode for scripting
pkginspector -g -q MyPackage.pkg
```

### Command-Line Options

- `-f, --reveal-file <path>` - Open package and navigate to specified file
- `-s, --reveal-scripts` - Open package and show Scripts tab
- `-g, --show-signature` - Show package signature information (CLI output)
- `-p, --show-component-packages` - List component packages (CLI output)
- `-q, --quiet` - Minimal output for scripting
- `-h, --help` - Show help message

### Mode Detection

The application automatically detects which mode to use:
- **GUI Mode**: No arguments, or just a package file path
- **CLI Mode**: Any flags present (`-g`, `-s`, `-f`, etc.)

## Package Format

The inspector supports `.pkg` files created by `cimipkg`, which are ZIP archives with this structure:

```
package.pkg (ZIP file)
├── payload/                   # Files to be installed
├── scripts/                   # Installation scripts
│   ├── preinstall.ps1
│   └── postinstall.ps1
└── build-info.yaml            # Package metadata
```

Also compatible with NuGet package (`.nupkg`) structure with `.nuspec` metadata files.

## Building

### Standard Build

```powershell
# Build both architectures with .pkg packages
.\build.ps1

# Build specific architecture
.\build.ps1 -Architecture x64
.\build.ps1 -Architecture arm64

# Build without creating packages
.\build.ps1 -SkipPkg
```

### Code Signing

```powershell
# List available certificates
.\build.ps1 -ListCerts

# Build with specific certificate
.\build.ps1 -CertificateThumbprint <thumbprint>

# Auto-detect and use best certificate (default behavior)
.\build.ps1
```

The build script automatically detects and uses available code signing certificates, preferring enterprise certificates.

### Build Output

```
.\build\
├── x64\
│   ├── pkginspector.exe
│   └── [dependencies]
├── arm64\
│   ├── pkginspector.exe
│   └── [dependencies]
├── pkginspector-x64-<version>.pkg
└── pkginspector-arm64-<version>.pkg
```

## Unified Binary Architecture

Package Inspector uses a single executable that intelligently switches between GUI and CLI modes based on command-line arguments. This design provides:

- **Simplified Distribution** - One binary to maintain and sign
- **Reduced Footprint** - No duplicate code between GUI and CLI
- **Consistent Behavior** - Same package inspection logic in both modes
- **Flexible Invocation** - Works seamlessly as GUI app or CLI tool

### Technical Implementation

The mode detection happens in `App.xaml.cs`:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    var args = e.Args;
    bool isCliMode = args.Length > 0 && 
        args.Any(a => a.StartsWith("-") || a.StartsWith("--"));
    
    if (isCliMode)
    {
        AttachConsole(ATTACH_PARENT_PROCESS);
        RunCliMode(args);
        Environment.Exit(0);
    }
    else
    {
        // Continue with GUI initialization
        base.OnStartup(e);
    }
}
```

## Scripting Examples

### Check Package Signature

```powershell
$result = & pkginspector -g -q MyPackage.pkg
if ($result -like "Signed|*") {
    Write-Host "Package is signed"
    $parts = $result -split '\|'
    Write-Host "Subject: $($parts[1])"
    Write-Host "Signed: $($parts[2])"
} else {
    Write-Host "Package is not signed"
}
```

### Batch Inspect Packages

```powershell
Get-ChildItem *.pkg | ForEach-Object {
    Write-Host "`nInspecting: $($_.Name)"
    & pkginspector -g $_.FullName
}
```

## Development

### Prerequisites

- .NET 9.0 SDK
- Windows 10/11 (x64 or ARM64)
- Windows SDK (for code signing)
- Visual Studio 2022 or VS Code (optional)

### Project Structure

```
pkg-inspector/
├── App.xaml / App.xaml.cs           # Application entry point, CLI detection
├── MainWindow.xaml / .cs            # Main GUI window
├── WelcomeControl.xaml / .cs        # Welcome screen
├── Models/
│   ├── PackageData.cs               # Package data model
│   └── BuildInfo.cs                 # Build metadata model
├── Services/
│   └── PackageInspectorService.cs   # Package inspection logic
├── Converters/
│   └── BoolToVisibilityConverter.cs # XAML converters
├── Resources/                       # Application resources
├── build.ps1                        # Build automation script
├── pkginspector.csproj              # Project file
└── pkg-inspector.sln                # Solution file
```

### Running from Source

```powershell
# Run GUI
dotnet run

# Run with package
dotnet run -- MyPackage.pkg

# Run CLI mode
dotnet run -- --help
dotnet run -- -g MyPackage.pkg

# Build debug
dotnet build

# Build release
dotnet build --configuration Release
```

### Dependencies

- **YamlDotNet** 13.7.1 - YAML parsing for build-info.yaml
- **AvalonEdit** 6.3.0.90 - Syntax-highlighted text editor for scripts
- **Microsoft.Extensions.Logging** 9.0.0 - Logging framework

## Contributing

Contributions are welcome! This project originated from [sbin-installer](https://github.com/windowsadmins/sbin-installer).

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Author

**Windows Admins**
- GitHub: [@windowsadmins](https://github.com/windowsadmins)
- Original Project: [sbin-installer](https://github.com/windowsadmins/sbin-installer)

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.
