# Package Inspector

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue)](https://www.microsoft.com/windows)

A modern Windows Installer (MSI) inspection tool with both GUI and CLI interfaces. Package Inspector reads `.msi` files directly via `msi.dll` and also supports NuGet (`.nupkg`) packages. Legacy `.pkg` archives from the early cimipkg stop-gap format are still readable, but MSI is now the primary format going forward.

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
- **Direct MSI Inspection** - Opens `.msi` files via `msi.dll` through the WiX DTF managed wrapper — no extraction, no WiX toolchain, no shelling out to `msiexec`
- **cimipkg MSI Decoder** - Automatically decodes PowerShell scripts embedded in cimipkg-built MSI custom actions and re-hydrates the original `build-info.yaml` metadata
- **NuGet Package Support** - Inspect `.nupkg` payloads and `.nuspec` metadata
- **Legacy `.pkg` Support** - Still reads the original cimipkg `.pkg` ZIP format for existing archives (kept for back-compatibility; new packages should be MSIs)
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

### From ZIP release (recommended)

Download the architecture-appropriate ZIP from the [releases page](https://github.com/windowsadmins/pkg-inspector/releases):

- `pkginspector-x64.zip` — Intel / AMD 64-bit
- `pkginspector-arm64.zip` — ARM64 (Surface Pro X, Copilot+ PCs, etc.)

Then:

1. Extract to a directory of your choice (e.g., `C:\Program Files\PkgInspector`)
2. Add that directory to your system `PATH`
3. Optionally create a desktop shortcut to `pkginspector.exe`

After extracting, run from any shell:

```powershell
pkginspector                    # Open GUI
pkginspector MyPackage.msi      # Inspect an MSI
pkginspector --help             # Show CLI help
```

Release binaries are shipped **unsigned**. For enterprise deployment, sign
them with your organization's code-signing certificate before distributing:

```powershell
signtool sign /fd SHA256 /tr https://timestamp.digicert.com /td SHA256 `
    /n "Your Certificate Name" pkginspector.exe
```

### From source

```powershell
# Clone repository
git clone https://github.com/windowsadmins/pkg-inspector.git
cd pkg-inspector

# Build both architectures (produces .exe in .\build\x64 and .\build\arm64)
.\build.ps1

# Build for specific architecture
.\build.ps1 -Architecture x64
.\build.ps1 -Architecture arm64

# List available code signing certificates
.\build.ps1 -ListCerts

# Build with a specific code-signing cert
.\build.ps1 -CertificateThumbprint <thumbprint>
```

Build outputs land in `.\build\<arch>\pkginspector.exe`. If `cimipkg` is
installed locally, the build script can also produce legacy `.pkg`
archives of itself — this is kept for back-compatibility but is not
the recommended distribution channel going forward.

## Usage

### GUI Mode

Launch the application and interact visually:

```powershell
pkginspector                    # Open empty window
pkginspector MyPackage.msi      # Inspect an MSI
pkginspector MyPackage.nupkg    # Inspect a NuGet package
pkginspector LegacyPackage.pkg  # Inspect a legacy cimipkg .pkg archive
```

Or simply drag and drop a package file onto the window.

Navigate through tabs:
- **Package Info** - Overview and installation details
- **All Files** - Tree view of payload files
- **All Scripts** - Pre/post-install scripts (including PowerShell decoded from MSI custom actions)
- **Metadata** - Raw YAML metadata (from `build-info.yaml` or the MSI `CIMIAN_PKG_BUILD_INFO` summary entry)

### CLI Mode

The same executable provides full command-line functionality:

```powershell
# Show help
pkginspector --help

# Show MSI signature information
pkginspector -g MyPackage.msi

# Open package with specific tab
pkginspector -s MyPackage.msi           # Scripts tab
pkginspector -f "path/to/file" MyPackage.msi  # Navigate to file

# Quiet mode for scripting
pkginspector -g -q MyPackage.msi
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

## Supported Package Formats

### `.msi` (Windows Installer) — primary format

Package Inspector reads `.msi` files directly via `msi.dll` through the
[WiX DTF](https://wixtoolset.org/docs/dtf/) managed wrapper, so no
extraction, no WiX toolchain, and no `msiexec` shell-out is required.
Both commercial MSIs and cimipkg-built MSIs are supported.

For MSIs built by cimipkg, the inspector automatically:

- Reads the embedded `CIMIAN_PKG_BUILD_INFO` summary information entry
  and re-hydrates the original `build-info.yaml` metadata.
- Detects preinstall/postinstall custom actions whose VBScript source
  contains base64-chunked PowerShell, and decodes them back to their
  original PowerShell form for display in the Script Inspector.

### `.nupkg` (NuGet) — secondary format

Standard NuGet package format — ZIP archives with `.nuspec` metadata
files. Supported for inspection alongside MSI.

### `.pkg` (cimipkg ZIP) — legacy

> **Note:** The `.pkg` ZIP format was a stop-gap while cimipkg was being
> taught to produce real MSI installers. It is being phased out in favor
> of `.msi` and should not be used for new packages. Package Inspector
> will continue reading existing `.pkg` archives for back-compatibility.

`.pkg` files from the legacy cimipkg flow are ZIP archives with this structure:

```
package.pkg (ZIP file)
├── payload/                   # Files to be installed
├── scripts/                   # Installation scripts
│   ├── preinstall.ps1
│   └── postinstall.ps1
└── build-info.yaml            # Package metadata
```

## Building

### Standard Build

```powershell
# Build both architectures
.\build.ps1

# Build specific architecture
.\build.ps1 -Architecture x64
.\build.ps1 -Architecture arm64

# Skip legacy .pkg packaging (on by default when cimipkg isn't installed)
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

The build script automatically detects and uses available code-signing
certificates, preferring enterprise certificates.

### Build Output

```
.\build\
├── x64\
│   ├── pkginspector.exe
│   └── [dependencies]
└── arm64\
    ├── pkginspector.exe
    └── [dependencies]
```

If `cimipkg` is available on the build machine, the script will
additionally emit `pkginspector-<arch>-<version>.pkg` archives of
itself alongside the binaries — a leftover from the legacy `.pkg`
distribution flow, retained for back-compatibility.

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

    // CLI mode detection: any flags (-x / --x) or the literal "help" word
    bool isCliMode = args.Length > 0 && (
        args.Any(a => a.StartsWith("-") || a.StartsWith("--")) ||
        args.Any(a => a.Equals("help", StringComparison.OrdinalIgnoreCase))
    );

    if (isCliMode)
    {
        // Attach to the parent shell's console for CLI output; fall back
        // to a fresh console if the parent isn't a console host.
        if (!AttachConsole(ATTACH_PARENT_PROCESS))
        {
            AllocConsole();
        }

        Shutdown(RunCliMode(args));
        return;
    }

    // Otherwise, continue with normal GUI startup
    base.OnStartup(e);
}
```

## Scripting Examples

### Check Package Signature

```powershell
$result = & pkginspector -g -q MyPackage.msi
if ($result -like "Signed|*") {
    Write-Host "Package is signed"
    $parts = $result -split '\|'
    Write-Host "Subject: $($parts[1])"
    Write-Host "Signed: $($parts[2])"
} else {
    Write-Host "Package is not signed"
}
```

### Batch Inspect MSIs in a Directory

```powershell
Get-ChildItem *.msi | ForEach-Object {
    Write-Host "`nInspecting: $($_.Name)"
    & pkginspector -g $_.FullName
}
```

## Development

### Prerequisites

- .NET 10.0 SDK
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
│   ├── PackageData.cs               # Package + file/script/tree-node data models
│   └── BuildInfo.cs                 # build-info.yaml + nested ProductInfo models
├── Services/
│   ├── MsiInspectorService.cs       # .msi inspection via WiX DTF / msi.dll (primary)
│   ├── CimipkgVbsDecoder.cs         # Decode PS embedded in cimipkg MSI custom actions
│   └── PackageInspectorService.cs   # .nupkg and legacy .pkg inspection (ZIP + YAML)
├── Converters/
│   └── BoolToVisibilityConverter.cs # BoolToVisibility, InverseBoolToVisibility, SignatureBrush
├── Resources/                       # Application resources
├── build.ps1                        # Build automation script
├── pkginspector.csproj              # Project file
└── pkg-inspector.sln                # Solution file
```

### Running from Source

```powershell
# Run GUI
dotnet run

# Run with a package (MSI, NuGet, or legacy .pkg all work)
dotnet run -- MyPackage.msi

# Run CLI mode
dotnet run -- --help
dotnet run -- -g MyPackage.msi

# Build debug
dotnet build

# Build release
dotnet build --configuration Release
```

### Dependencies

- **YamlDotNet** 13.7.1 — YAML parsing for `build-info.yaml`
- **AvalonEdit** 6.3.0.90 — Syntax-highlighted text editor for scripts
- **WixToolset.Dtf.WindowsInstaller** 5.0.0 — Managed wrapper over `msi.dll` for direct MSI inspection
- **Microsoft.Extensions.Logging** 10.0.0 — Logging framework
- **Microsoft.Extensions.Logging.Console** 10.0.0 — Console sink for CLI-mode diagnostics

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
