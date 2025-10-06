# Changelog

All notable changes to Package Inspector will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.0]: https://github.com/windowsadmins/pkg-inspector/releases/tag/v1.0.0
