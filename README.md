# Package Inspector# Package Inspector# Package Inspector



A modern Windows package inspection tool with both GUI and CLI interfaces for examining `.pkg` and `.nupkg` files.



Originally part of the [sbin-installer](https://github.com/windowsadmins/sbin-installer) project, Package Inspector is now a standalone tool for inspecting package contents, metadata, and installation scripts.A modern GUI tool for inspecting Windows package files (`.pkg` and `.nupkg`).A modern **unified binary** tool for inspecting Windows package files (`.pkg` and `.nupkg`).



## Features



- **Unified Binary Architecture** - Single executable supports both GUI and CLI modesOriginally part of the [sbin-installer](https://github.com/windowsadmins/sbin-installer) project, Package Inspector is now a standalone tool for inspecting package contents.Package Inspector combines both GUI and CLI functionality in a single executable:

- **Package Overview** - View metadata including name, version, description, author, and license

- **File Browser** - Navigate through all files in the package payload with a tree view- **`pkginspector.exe`** - Main binary (works as both GUI and CLI)

- **Script Inspector** - View and analyze pre-install and post-install PowerShell scripts

- **Metadata Viewer** - Examine raw YAML metadata from build-info.yaml## Features- **`spkg.exe`** - Convenient CLI alias (same binary, different name)

- **Drag & Drop** - Simply drag a package file onto the window to inspect it

- **Command-Line Interface** - Full CLI support for scripting and automation

- **Code Signing** - Automatic code signing with available certificates

- **Modern UI** - Clean, intuitive interface inspired by macOS Suspicious Package- **Package Overview**: View metadata including name, version, description, author, license, and moreOriginally part of the [sbin-installer](https://github.com/windowsadmins/sbin-installer) project, Package Inspector is now a standalone tool for inspecting package contents.



## Installation- **File Browser**: Navigate through all files in the package payload with a tree view



### From .pkg Package- **Script Inspector**: View all pre-install and post-install scripts> **New in 2025.10**: Unified binary architecture! See [UNIFIED-BINARY.md](UNIFIED-BINARY.md) for details.



```powershell- **Metadata Viewer**: Examine raw YAML metadata from build-info.yaml

# Using sbin-installer (cimipkg)

installer --pkg pkginspector-x64-<version>.pkg --target /- **Drag & Drop**: Simply drag a package file onto the window to inspect it

```

- **Modern UI**: Clean, intuitive interface inspired by macOS Suspicious Package

This installs to `C:\Program Files\PkgInspector\` and adds it to your system PATH.

## Features

After installation, restart your shell and run from any location:

## Building

```powershell

pkginspector              # Open GUI

pkginspector --help       # Show CLI help

``````powershell



### From Source# Standard build (both architectures, signed, with .pkg packages)- **Package Overview**: View metadata including name, version, description, author, license, and moreOriginally part of the [sbin-installer](https://github.com/windowsadmins/sbin-installer) project, Package Inspector is now a standalone tool for inspecting package contents.## Features



```powershell.\build.ps1

# Clone repository

git clone https://github.com/windowsadmins/pkg-inspector.git- **File Browser**: Navigate through all files in the package payload with a tree view

cd pkg-inspector

# Build for specific architecture only

# Build

.\build.ps1.\build.ps1 -Architecture x64- **Script Inspector**: View all pre-install and post-install scripts



# Build for specific architecture

.\build.ps1 -Architecture x64

.\build.ps1 -Architecture arm64# Build without creating .pkg packages- **Metadata Viewer**: Examine raw YAML metadata from build-info.yaml

```

.\build.ps1 -SkipPkg

## Usage

- **Drag & Drop**: Simply drag a package file onto the window to inspect it## Features- **Package Overview**: View metadata including name, version, description, author, license, and more

### GUI Mode

# List available code signing certificates

Launch the application and interact visually:

.\build.ps1 -ListCerts- **Modern UI**: Clean, intuitive interface inspired by macOS Suspicious Package

```powershell

pkginspector                    # Open empty window```

pkginspector MyPackage.pkg      # Open specific package

```- **CLI Tool**: Command-line tool (`spkg`) for scripting and automation- **File Browser**: Navigate through all files in the package payload with a tree view



Or simply drag and drop a package file onto the window.Build outputs:



Navigate through tabs:- **Executable**: `.\build\x64\pkginspector.exe` and `.\build\arm64\pkginspector.exe`

- **Package Info** - Overview and installation details

- **All Files** - Tree view of payload files- **Packages**: `.\build\pkginspector-x64-<version>.pkg` and `.\build\pkginspector-arm64-<version>.pkg`

- **All Scripts** - Pre/post-install scripts

- **Metadata** - Raw YAML metadata## Building- **Package Overview**: View metadata including name, version, description, author, license, and more- **Script Inspector**: View all pre-install and post-install scripts



### CLI ModeAll executables are automatically code-signed if certificates are available.



The same executable provides full command-line functionality:



```powershell## Usage

# Show help

pkginspector --help```powershell- **File Browser**: Navigate through all files in the package payload with a tree view- **Metadata Viewer**: Examine raw YAML metadata from build-info.yaml



# Show package signature information1. Launch the application: `pkginspector.exe`

pkginspector -g MyPackage.pkg

2. Either:# Standard build (both architectures, signed, with .pkg packages)

# Open package with specific tab

pkginspector -s MyPackage.pkg           # Scripts tab   - Click "Open Package..." and select a `.pkg` or `.nupkg` file

pkginspector -f "path/to/file" MyPackage.pkg  # Navigate to file

   - Drag and drop a package file onto the window.\build.ps1- **Script Inspector**: View all pre-install and post-install scripts- **Drag & Drop**: Simply drag a package file onto the window to inspect it

# Quiet mode for scripting

pkginspector -g -q MyPackage.pkg3. Browse through the tabs:

```

   - **Package Info**: Overview and installation details

### Command-Line Options

   - **All Files**: Tree view of files in the payload

- `-f, --reveal-file <path>` - Open package and navigate to specified file

- `-s, --reveal-scripts` - Open package and show Scripts tab   - **All Scripts**: View pre/post-install scripts# Build for specific architecture only- **Metadata Viewer**: Examine raw YAML metadata from build-info.yaml- **Modern UI**: Clean, intuitive interface inspired by macOS Suspicious Package

- `-g, --show-signature` - Show package signature information (CLI output)

- `-p, --show-component-packages` - List component packages (CLI output)   - **Metadata**: Raw YAML metadata

- `-q, --quiet` - Minimal output for scripting

- `-h, --help` - Show help message.\build.ps1 -Architecture x64



### Mode Detection## Installation



The application automatically detects which mode to use:- **Drag & Drop**: Simply drag a package file onto the window to inspect it



- **GUI Mode**: No arguments, or just a package file path### From .pkg Package

- **CLI Mode**: Any flags present (`-g`, `-s`, `-f`, etc.)

# Build without creating .pkg packages

## Building

```powershell

### Standard Build

# Using sbin-installer (cimipkg).\build.ps1 -SkipPkg- **Modern UI**: Clean, intuitive interface inspired by macOS Suspicious Package## Building

```powershell

# Build both architectures with .pkg packagesinstaller --pkg pkginspector-x64-<version>.pkg --target /

.\build.ps1

```

# Build specific architecture

.\build.ps1 -Architecture x64

.\build.ps1 -Architecture arm64

This installs to `C:\Program Files\sbin\pkginspector\` and adds it to your system PATH.# List available code signing certificates

# Build without creating packages

.\build.ps1 -SkipPkg

```

After installation, you can run `pkginspector` from any location..\build.ps1 -ListCerts

### Code Signing



```powershell

# List available certificates## Package Format```## Building```powershell

.\build.ps1 -ListCerts



# Build with specific certificate

.\build.ps1 -CertificateThumbprint <thumbprint>The inspector supports `.pkg` files created by `cimipkg`, which are ZIP archives with this structure:



# Auto-detect and use best certificate (default behavior)

.\build.ps1

``````Build outputs:cd src\pkginspector



The build script automatically detects and uses available code signing certificates, preferring enterprise certificates.package.pkg (ZIP file)



### Build Output├── payload/                   # Files to be installed- **GUI**: `.\build\x64\pkginspector.exe` and `.\build\arm64\pkginspector.exe`



```├── scripts/                   # Installation scripts

.\build\

├── x64\│   ├── preinstall.ps1- **CLI**: `.\build\x64\spkg.exe` and `.\build\arm64\spkg.exe````powershelldotnet build

│   ├── pkginspector.exe

│   └── [dependencies]│   └── postinstall.ps1

├── arm64\

│   ├── pkginspector.exe└── build-info.yaml            # Package metadata- **Packages**: `.\build\pkginspector-x64-<version>.pkg` and `.\build\pkginspector-arm64-<version>.pkg`

│   └── [dependencies]

├── pkginspector-x64-<version>.pkg```

└── pkginspector-arm64-<version>.pkg

```dotnet builddotnet run



## Package Format## Documentation



The inspector supports `.pkg` files created by `cimipkg`, which are ZIP archives with this structure:All executables are automatically code-signed if certificates are available.



```- **[BUILD-SYSTEM-COMPLETE.md](BUILD-SYSTEM-COMPLETE.md)** - Build system features and usage

package.pkg (ZIP file)

├── payload/                   # Files to be installed- **[DEVELOPMENT.md](DEVELOPMENT.md)** - Developer guidedotnet run```

│   └── [installation files]

├── scripts/                   # Installation scripts- **[QUICK-START.md](QUICK-START.md)** - Quick reference

│   ├── preinstall.ps1

│   └── postinstall.ps1- **[GIT-SETUP.md](GIT-SETUP.md)** - GitHub setup instructions## Usage

└── build-info.yaml            # Package metadata

```



## Development## Development```



### Prerequisites



- .NET 9.0 SDK```powershell### GUI Application

- Windows 10/11 (x64 or ARM64)

- Windows SDK (for code signing)# Run from source

- Visual Studio 2022 or VS Code (optional)

dotnet run --project pkginspector.csproj## Usage

### Project Structure



```

pkg-inspector/# Build debug1. Launch the application: `pkginspector.exe`

├── App.xaml / App.xaml.cs           # Application entry point, CLI detection

├── MainWindow.xaml / .cs            # Main GUI windowdotnet build pkg-inspector.sln

├── WelcomeControl.xaml / .cs        # Welcome screen

├── Models/2. Either:Or use the build script to create release builds:

│   ├── PackageData.cs               # Package data model

│   └── BuildInfo.cs                 # Build metadata model# Run tests (if any)

├── Services/

│   └── PackageInspectorService.cs   # Package inspection logicdotnet test   - Click "Open Package..." and select a `.pkg` or `.nupkg` file

├── Converters/

│   └── BoolToVisibilityConverter.cs # XAML converters```

├── Resources/                       # Application resources

├── build.ps1                        # Build automation script   - Drag and drop a package file onto the window1. Launch the application

├── pkginspector.csproj              # Project file

└── pkg-inspector.sln                # Solution file## Requirements

```

3. Browse through the tabs:

### Running from Source

- .NET 9.0

```powershell

# Run GUI- Windows 10/11 (x64 or ARM64)   - **Package Info**: Overview and installation details```powershell2. Either:

dotnet run

- Windows SDK (for code signing)

# Run with package

dotnet run -- MyPackage.pkg   - **All Files**: Tree view of files in the payload



# Run CLI mode## Dependencies

dotnet run -- --help

dotnet run -- -g MyPackage.pkg   - **All Scripts**: View pre/post-install scripts.\build.ps1   - Click "Open Package..." and select a `.pkg` or `.nupkg` file



# Build debug- **YamlDotNet** 13.7.1 - YAML parsing

dotnet build

- **AvalonEdit** 6.3.0.90 - Text editor control   - **Metadata**: Raw YAML metadata

# Build release

dotnet build --configuration Release- **Microsoft.Extensions.Logging** 9.0.0 - Logging framework

```

```   - Drag and drop a package file onto the window

### Dependencies

## License

- **YamlDotNet** 13.7.1 - YAML parsing for build-info.yaml

- **AvalonEdit** 6.3.0.90 - Syntax-highlighted text editor for scripts### CLI Tool (spkg)

- **Microsoft.Extensions.Logging** 9.0.0 - Logging framework

MIT License - see [LICENSE](LICENSE) file for details.

## Unified Binary Architecture

3. Browse through the tabs:

Package Inspector uses a single executable that intelligently switches between GUI and CLI modes based on command-line arguments. This design provides:

## Contributing

- **Simplified Distribution** - One binary to maintain and sign

- **Reduced Footprint** - No duplicate code between GUI and CLI```powershell

- **Consistent Behavior** - Same package inspection logic in both modes

- **Flexible Invocation** - Works seamlessly as GUI app or CLI toolThis project originated from [sbin-installer](https://github.com/windowsadmins/sbin-installer). Contributions are welcome!



### Technical Implementation# Show package signature informationThis will create builds in `.\build\win-x64` and `.\build\win-arm64`.   - **Package Info**: Overview and installation details



The mode detection happens in `App.xaml.cs`:1. Fork the repository



```csharp2. Create a feature branchspkg -g MyPackage.pkg

protected override void OnStartup(StartupEventArgs e)

{3. Make your changes

    var args = e.Args;

    bool isCliMode = args.Length > 0 && 4. Submit a pull request   - **All Files**: Tree view of files in the payload

        args.Any(a => a.StartsWith("-") || a.StartsWith("--"));

    

    if (isCliMode)

    {## Author# Show signature (quiet mode for scripting)

        AttachConsole(ATTACH_PARENT_PROCESS);

        RunCliMode(args);

        Environment.Exit(0);

    }**Windows Admins**spkg -g -q MyPackage.pkg## Usage   - **All Scripts**: View pre/post-install scripts

    else

    {- GitHub: [@windowsadmins](https://github.com/windowsadmins)

        // Continue with GUI initialization

        base.OnStartup(e);- Original Project: [sbin-installer](https://github.com/windowsadmins/sbin-installer)

    }

}

```# Open package in GUI   - **Metadata**: Raw YAML metadata



## Scripting Examplesspkg MyPackage.pkg



### Check Package Signature1. Launch the application



```powershell# Open package and show scripts tab

$result = & pkginspector -g -q MyPackage.pkg

if ($result -like "Signed|*") {spkg -s MyPackage.pkg2. Either:## Package Format

    Write-Host "Package is signed"

    $parts = $result -split '\|'

    Write-Host "Subject: $($parts[1])"

    Write-Host "Signed: $($parts[2])"# Show help   - Click "Open Package..." and select a `.pkg` or `.nupkg` file

} else {

    Write-Host "Package is not signed"spkg --help

}

``````   - Drag and drop a package file onto the windowThe inspector supports `.pkg` files created by `cimipkg`, which are ZIP archives with this structure:



### Batch Inspect Packages



```powershellSee [CLI-USAGE.md](CLI-USAGE.md) for detailed CLI documentation.3. Browse through the tabs:

Get-ChildItem *.pkg | ForEach-Object {

    Write-Host "`nInspecting: $($_.Name)"

    & pkginspector -g $_.FullName

}## Installation   - **Package Info**: Overview and installation details```

```



## Contributing

### From .pkg Package   - **All Files**: Tree view of files in the payloadpackage.pkg (ZIP file)

Contributions are welcome! This project originated from [sbin-installer](https://github.com/windowsadmins/sbin-installer).



1. Fork the repository

2. Create a feature branch (`git checkout -b feature/amazing-feature`)```powershell   - **All Scripts**: View pre/post-install scripts├── payload/                   # Files to be installed

3. Commit your changes (`git commit -m 'Add amazing feature'`)

4. Push to the branch (`git push origin feature/amazing-feature`)# Using sbin-installer (cimipkg)

5. Open a Pull Request

installer --pkg pkginspector-x64-<version>.pkg --target /   - **Metadata**: Raw YAML metadata├── scripts/                   # Installation scripts

## License

```

MIT License - see [LICENSE](LICENSE) file for details.

│   ├── preinstall.ps1

## Author

This installs to `C:\Program Files\sbin\pkginspector\` and adds it to your system PATH.

**Windows Admins**

- GitHub: [@windowsadmins](https://github.com/windowsadmins)## Package Format│   └── postinstall.ps1

- Original Project: [sbin-installer](https://github.com/windowsadmins/sbin-installer)

After installation, you can run:

## Changelog

- `pkginspector` - Open GUI└── build-info.yaml            # Package metadata

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.

- `spkg` - Use CLI tool

The inspector supports `.pkg` files created by `cimipkg`, which are ZIP archives with this structure:```

## Package Format



The inspector supports `.pkg` files created by `cimipkg`, which are ZIP archives with this structure:

```## Installation Target

```

package.pkg (ZIP file)package.pkg (ZIP file)

├── payload/                   # Files to be installed

├── scripts/                   # Installation scripts├── payload/                   # Files to be installedTo install pkginspector to `\Program Files\sbin\pkginspector`, use the sbin-installer:

│   ├── preinstall.ps1

│   └── postinstall.ps1├── scripts/                   # Installation scripts

└── build-info.yaml            # Package metadata

```│   ├── preinstall.ps1```powershell



## Documentation│   └── postinstall.ps1sbin\installer --pkg pkginspector.pkg --target /



- **[BUILD-SYSTEM-COMPLETE.md](BUILD-SYSTEM-COMPLETE.md)** - Build system features and usage└── build-info.yaml            # Package metadata```

- **[CLI-USAGE.md](CLI-USAGE.md)** - Detailed CLI tool documentation

- **[DEVELOPMENT.md](DEVELOPMENT.md)** - Developer guide```

- **[QUICK-START.md](QUICK-START.md)** - Quick reference

- **[GIT-SETUP.md](GIT-SETUP.md)** - GitHub setup instructions## License



## Development## Command-Line Interface



```powershellSame as sbin-installer project.

# Run from source

dotnet run --project pkginspector.csprojA CLI version (`spkg`) is also available in the `cli` subdirectory for scripting and automation.



# Build debug## License

dotnet build pkg-inspector.sln

MIT License - see LICENSE file for details.

# Run tests (if any)
dotnet test
```

## Requirements

- .NET 9.0
- Windows 10/11 (x64 or ARM64)
- Windows SDK (for code signing)

## Dependencies

- **YamlDotNet** 13.7.1 - YAML parsing
- **AvalonEdit** 6.3.0.90 - Text editor control
- **Microsoft.Extensions.Logging** 9.0.0 - Logging framework

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Contributing

This project originated from [sbin-installer](https://github.com/windowsadmins/sbin-installer). Contributions are welcome!

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## Author

**Windows Admins**
- GitHub: [@windowsadmins](https://github.com/windowsadmins)
- Original Project: [sbin-installer](https://github.com/windowsadmins/sbin-installer)
