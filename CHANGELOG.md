# Changelog

All notable changes to Package Inspector will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-04-11

### Added
- **Direct MSI inspection** via the WiX DTF managed wrapper (`WixToolset.Dtf.WindowsInstaller`) — `.msi` files are read through `msi.dll` without extraction or shelling out to `msiexec`. Supports both commercial MSIs and cimipkg-built MSIs.
- **cimipkg MSI decoder** (`Services/CimipkgVbsDecoder.cs`) — automatically extracts PowerShell source from base64-chunked VBScript custom actions in cimipkg-built MSIs for display in the Script Inspector.
- **CIMIAN_PKG_BUILD_INFO property lookup** — for MSIs built by cimipkg, re-hydrates the original `build-info.yaml` metadata from the MSI `Property` table.
- MSI file picker filter and drag-and-drop dispatch in the GUI (`*.msi` joins `*.pkg;*.nupkg`).

### Changed
- **Primary package format reframed from `.pkg` to `.msi`.** `.nupkg` is secondary; legacy `.pkg` ZIP archives remain readable for back-compatibility but are no longer the recommended distribution format.
- Migrated from **.NET 9.0** to **.NET 10.0** (`net10.0-windows`).
- `Microsoft.Extensions.Logging` bumped from 9.0.0 to **10.0.0**; added `Microsoft.Extensions.Logging.Console 10.0.0` for CLI-mode diagnostics.
- README rewritten with MSI-first framing, corrected dependency list, corrected project-structure tree (listing the new `MsiInspectorService.cs` and `CimipkgVbsDecoder.cs`), and a corrected `App.xaml.cs` `OnStartup` code snippet.
- Release artifacts are now ZIP-only (`pkginspector-x64.zip`, `pkginspector-arm64.zip`); the README installation flow documents the ZIP path instead of the removed "install via sbin-installer" flow.

### Fixed
- Removed unused `-SkipMsi` parameter from `build.ps1` (declared but never referenced).

### Known Limitations
- `pkginspector -g` (CLI signature check) currently reads signature metadata from `build-info.yaml` inside a ZIP-based archive, so it works with `.pkg` and `.nupkg` only. MSI Authenticode signature extraction via the CLI is a planned enhancement — for now, MSI signatures are visible in the GUI only.
- `pkginspector -f <path>` currently switches to the Files tab but does not yet navigate to the specified file (TODO in `MainWindow.xaml.cs`).

## [1.0.0] - 2025-10-06

### Added
- Initial standalone release of Package Inspector
- Modern WPF GUI for inspecting `.pkg` and `.nupkg` files
- Unified binary architecture supporting both GUI and CLI modes in single executable
- Package overview with metadata display
- File browser with tree view
- Script inspector for pre/post-install PowerShell scripts
- YAML metadata viewer with syntax highlighting
- Drag and drop support for package files
- Full command-line interface with options for scripting and automation
- Support for Windows x64 and ARM64 architectures
- Self-contained deployment with all dependencies included
- Automated build script with code signing support
- Installation to C:\Program Files\PkgInspector with automatic PATH configuration

### Changed
- Migrated from `sbin-installer` repository to standalone project
- Consolidated CLI and GUI into single unified binary
- Updated installation location to C:\Program Files\PkgInspector

### Technical Details
- Built with .NET 9.0 and WPF
- Single executable intelligently detects GUI vs CLI mode
- Automatic console attachment for CLI output
- Code signing with enterprise certificates
- Package size approximately 67 MB including all dependencies

### Project History
- Originally developed as part of [sbin-installer](https://github.com/windowsadmins/sbin-installer)
- Extracted to standalone repository due to utility value beyond sbin-installer ecosystem

[1.1.0]: https://github.com/windowsadmins/pkg-inspector/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/windowsadmins/pkg-inspector/releases/tag/v1.0.0
