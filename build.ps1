# Build and publish script for pkginspector

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\build",
    [string]$CertificateThumbprint,
    [string]$TimeStampServer = "http://timestamp.digicert.com",
    [switch]$ListCerts,
    [string]$FindCertSubject,
    [switch]$SkipMsi,
    [switch]$SkipPkg,
    [string]$Version = "",
    [ValidateSet("x64", "arm64", "both", "auto")]
    [string]$Architecture = "both"
)

$ErrorActionPreference = "Stop"

# Load .env file if it exists
if (Test-Path ".env") {
    Get-Content ".env" | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]*)\s*=\s*(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            Set-Item -Path "env:$name" -Value $value
        }
    }
}

# Generate timestamp version if not provided
if ([string]::IsNullOrEmpty($Version)) {
    $now = Get-Date
    $Version = "$($now.Year).$($now.Month.ToString('D2')).$($now.Day.ToString('D2')).$($now.Hour.ToString('D2'))$($now.Minute.ToString('D2'))"
}

# Certificate management functions
function Find-CodeSigningCerts {
    param([string]$SubjectFilter = "")
    
    $certs = @()
    $stores = @("Cert:\CurrentUser\My", "Cert:\LocalMachine\My")
    
    foreach ($store in $stores) {
        $storeCerts = Get-ChildItem $store -ErrorAction SilentlyContinue | Where-Object {
            ($_.EnhancedKeyUsageList -like "*Code Signing*" -or $_.HasPrivateKey) -and 
            $_.NotAfter -gt (Get-Date) -and
            ($SubjectFilter -eq "" -or $_.Subject -like "*$SubjectFilter*")
        }
        
        if ($storeCerts) {
            $certs += $storeCerts | Select-Object *, @{Name='Store'; Expression={$store}}
        }
    }
    
    return $certs | Sort-Object NotAfter -Descending
}

function Show-CertificateList {
    $certs = Find-CodeSigningCerts
    
    if ($certs) {
        Write-Host "Available code signing certificates:" -ForegroundColor Green
        for ($i = 0; $i -lt $certs.Count; $i++) {
            $cert = $certs[$i]
            Write-Host ""
            Write-Host "[$($i + 1)] Subject: $($cert.Subject)" -ForegroundColor Cyan
            Write-Host "    Issuer:  $($cert.Issuer)" -ForegroundColor Gray
            Write-Host "    Thumbprint: $($cert.Thumbprint)" -ForegroundColor Yellow
            Write-Host "    Valid Until: $($cert.NotAfter)" -ForegroundColor Gray
            Write-Host "    Store: $($cert.Store)" -ForegroundColor Gray
        }
        Write-Host ""
    } else {
        Write-Host "No valid code signing certificates found" -ForegroundColor Yellow
    }
    
    return $certs
}

function Get-BestCertificate {
    $certs = Find-CodeSigningCerts
    
    # First priority: Enterprise certificate from environment variable
    if ($env:PREFERRED_CERT_SUBJECT) {
        $enterpriseCert = $certs | Where-Object { $_.Subject -like "*$env:PREFERRED_CERT_SUBJECT*" } | Sort-Object NotAfter -Descending | Select-Object -First 1
        if ($enterpriseCert) {
            return $enterpriseCert
        }
    }
    
    # Fallback: Prefer CurrentUser over LocalMachine, and newest expiration date
    $best = $certs | Sort-Object @{Expression={$_.Store -eq "Cert:\CurrentUser\My"}; Descending=$true}, NotAfter -Descending | Select-Object -First 1
    
    return $best
}

# Handle certificate management commands
if ($ListCerts) {
    Show-CertificateList | Out-Null
    return
}

if ($FindCertSubject) {
    Write-Host "Searching for certificates with subject containing: $FindCertSubject" -ForegroundColor Green
    $certs = Find-CodeSigningCerts -SubjectFilter $FindCertSubject
    
    if ($certs) {
        for ($i = 0; $i -lt $certs.Count; $i++) {
            $cert = $certs[$i]
            Write-Host ""
            Write-Host "[$($i + 1)] Subject: $($cert.Subject)" -ForegroundColor Cyan
            Write-Host "    Thumbprint: $($cert.Thumbprint)" -ForegroundColor Yellow
            Write-Host "    Valid Until: $($cert.NotAfter)" -ForegroundColor Gray
            Write-Host "    Store: $($cert.Store)" -ForegroundColor Gray
        }
    } else {
        Write-Host "No certificates found matching: $FindCertSubject" -ForegroundColor Yellow
    }
    return
}

Write-Host "Building Package Inspector..." -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan

# Auto-detect and use certificate if not explicitly provided
if (-not $CertificateThumbprint) {
    $bestCert = Get-BestCertificate
    if ($bestCert) {
        $CertificateThumbprint = $bestCert.Thumbprint
        Write-Host "Auto-detected certificate: $($bestCert.Subject)" -ForegroundColor Green
        Write-Host "Thumbprint: $CertificateThumbprint" -ForegroundColor Gray
    }
}

# Find signtool.exe
$SignTool = $null
if ($CertificateThumbprint) {
    $PossiblePaths = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
        "${env:ProgramFiles}\Windows Kits\10\bin\*\x64\signtool.exe",
        "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\*\bin\signtool.exe"
    )
    
    foreach ($Path in $PossiblePaths) {
        $Found = Get-ChildItem $Path -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
        if ($Found) {
            $SignTool = $Found.FullName
            break
        }
    }
    
    if (-not $SignTool) {
        Write-Warning "Could not find signtool.exe. Install Windows SDK for code signing."
    } else {
        Write-Host "Using SignTool: $SignTool" -ForegroundColor Gray
    }
}

# Determine runtime identifiers to build
$RuntimeIds = @()
if ($Architecture -eq "both") {
    $RuntimeIds = @("win-x64", "win-arm64")
    Write-Host "Building for both architectures: x64 and ARM64" -ForegroundColor Green
} elseif ($Architecture -eq "auto") {
    $DetectedArch = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64" -or $env:PROCESSOR_ARCHITEW6432 -eq "ARM64") {
        "win-arm64"
    } elseif ($env:PROCESSOR_ARCHITECTURE -eq "AMD64" -or $env:PROCESSOR_ARCHITEW6432 -eq "AMD64") {
        "win-x64"
    } else {
        "win-x86"
    }
    $RuntimeIds = @($DetectedArch)
    Write-Host "Auto-detected architecture: $DetectedArch" -ForegroundColor Yellow
} else {
    $RuntimeIds = @("win-$Architecture")
    Write-Host "Building for specified architecture: win-$Architecture" -ForegroundColor Yellow
}

# Restore packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore pkginspector.csproj

# Build project
Write-Host "Building Package Inspector..." -ForegroundColor Yellow
dotnet build pkginspector.csproj --configuration $Configuration

# Build for each runtime
foreach ($RuntimeId in $RuntimeIds) {
    $ArchName = ($RuntimeId -replace "win-", "")
    Write-Host ""
    Write-Host "=== Building for $RuntimeId ===" -ForegroundColor Cyan
    
    $ArchOutputDir = "$OutputPath\$ArchName"
    
    # Publish GUI (WPF doesn't work well with PublishSingleFile, so use folder mode)
    Write-Host "Publishing Package Inspector for $RuntimeId..." -ForegroundColor Yellow
    dotnet publish pkginspector.csproj --configuration $Configuration `
        --runtime $RuntimeId `
        --self-contained true `
        --output $ArchOutputDir `
        /p:PublishSingleFile=false `
        /p:PublishTrimmed=false `
        /p:AssemblyVersion=$Version `
        /p:FileVersion=$Version `
        /p:InformationalVersion=$Version
    
    $ExePath = "$ArchOutputDir\pkginspector.exe"
    
    if (-not (Test-Path $ExePath)) {
        Write-Error "Failed to build for $RuntimeId"
        continue
    }
    
    $exe = Get-Item $ExePath
    Write-Host "Binary size ($ArchName): $([math]::Round($exe.Length / 1MB, 2)) MB" -ForegroundColor Cyan
    
    # Sign executable
    if ($CertificateThumbprint -and $SignTool) {
        Write-Host "Signing executable ($ArchName)..." -ForegroundColor Yellow
        $null = & $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimeStampServer /td SHA256 $ExePath 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Signing failed for $ArchName with exit code $LASTEXITCODE"
        } else {
            Write-Host "Successfully signed ($ArchName)" -ForegroundColor Green
        }
    }
    
    # Create .pkg package using cimipkg
    if (-not $SkipPkg) {
        Write-Host ""
        Write-Host "Creating .pkg package for $ArchName..." -ForegroundColor Green
        
        # Check for cimipkg
        $cimipkg = $null
        $cimipkgPaths = @(
            "C:\Program Files\sbin\cimipkg.exe",
            "$PSScriptRoot\..\sbin-installer\dist\x64\installer.exe",
            (Get-Command cimipkg -ErrorAction SilentlyContinue).Source
        )
        
        foreach ($path in $cimipkgPaths) {
            if ($path -and (Test-Path $path)) {
                $cimipkg = $path
                break
            }
        }
        
        if ($cimipkg) {
            Write-Host "Using cimipkg: $cimipkg" -ForegroundColor Gray
            
            # Create package staging directory
            $pkgStagingDir = "$OutputPath\pkg-staging-$ArchName"
            if (Test-Path $pkgStagingDir) {
                Remove-Item $pkgStagingDir -Recurse -Force
            }
            New-Item -ItemType Directory -Path "$pkgStagingDir\payload" -Force | Out-Null
            New-Item -ItemType Directory -Path "$pkgStagingDir\scripts" -Force | Out-Null
            
            # Copy single executable to payload
            $payloadPath = "$pkgStagingDir\payload"
            
            # Copy main executable only (single-file with runtime extraction)
            Copy-Item "$ArchOutputDir\*" "$payloadPath\" -Recurse -Force
            
            # Create postinstall script to add to PATH
            $postinstallScript = @'
# Add pkginspector to system PATH
$installPath = "C:\Program Files\PkgInspector"
$currentPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::Machine)

if ($currentPath -notlike "*$installPath*") {
    Write-Host "Adding $installPath to system PATH..."
    $newPath = $currentPath.TrimEnd(';') + ";$installPath"
    [Environment]::SetEnvironmentVariable("PATH", $newPath, [EnvironmentVariableTarget]::Machine)
    Write-Host "Added to PATH. Restart shell to use pkginspector command."
}
'@
            Set-Content -Path "$pkgStagingDir\scripts\postinstall.ps1" -Value $postinstallScript
            
            # Create build-info.yaml
            $buildInfo = @"
name: PkgInspector
version: $Version
description: Modern GUI tool for inspecting Windows package files
author: Windows Admins Open Source
license: MIT
architecture: $ArchName
install_location: C:\Program Files\PkgInspector
"@
            Set-Content -Path "$pkgStagingDir\build-info.yaml" -Value $buildInfo
            
            # Build package
            Write-Host "Building package..." -ForegroundColor Yellow
            
            # Run cimipkg with the staging directory as the project directory
            & $cimipkg "$pkgStagingDir"
            
            # cimipkg creates the .pkg file in staging/build/ subdirectory
            $generatedPkg = Get-ChildItem "$pkgStagingDir\build\*.pkg" -ErrorAction SilentlyContinue | Select-Object -First 1
            
            if ($LASTEXITCODE -eq 0 -and $generatedPkg) {
                # Move to desired output location with our naming convention
                $pkgOutput = "$OutputPath\pkginspector-$ArchName-$Version.pkg"
                Move-Item $generatedPkg.FullName $pkgOutput -Force
                
                $pkgFile = Get-Item $pkgOutput
                Write-Host "Package created ($ArchName): $pkgOutput" -ForegroundColor Green
                Write-Host "Package size ($ArchName): $([math]::Round($pkgFile.Length / 1MB, 2)) MB" -ForegroundColor Cyan
            } else {
                Write-Warning "Package creation failed for $ArchName (exit code: $LASTEXITCODE)"
                # Try to find where the package actually is for debugging
                $allPkgs = Get-ChildItem "$pkgStagingDir" -Recurse -Filter "*.pkg" -ErrorAction SilentlyContinue
                if ($allPkgs) {
                    Write-Host "Found packages at: $($allPkgs.FullName -join ', ')" -ForegroundColor Yellow
                }
            }
            
            # Clean up staging
            Remove-Item $pkgStagingDir -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Write-Warning "cimipkg not found. Skipping .pkg creation for $ArchName"
            Write-Host "Install cimipkg from sbin-installer to create .pkg packages" -ForegroundColor Yellow
        }
    }
}

# Handle case when no code signing certificate found
if (-not $CertificateThumbprint) {
    Write-Host ""
    Write-Host "No code signing certificate found. Executables will not be signed." -ForegroundColor Yellow
    Write-Host "To sign, provide: .\build.ps1 -CertificateThumbprint <thumbprint>" -ForegroundColor Gray
    Write-Host "Or list available certificates: .\build.ps1 -ListCerts" -ForegroundColor Gray
}

# Summary
Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
foreach ($RuntimeId in $RuntimeIds) {
    $ArchName = ($RuntimeId -replace "win-", "")
    
    Write-Host ""
    Write-Host "Architecture: $ArchName" -ForegroundColor Yellow
    
    $ExePath = "$OutputPath\$ArchName\pkginspector.exe"
    if (Test-Path $ExePath) {
        Write-Host "  Executable: $ExePath" -ForegroundColor Cyan
    }
    
    if (-not $SkipPkg) {
        $PkgPath = "$OutputPath\pkginspector-$ArchName-$Version.pkg"
        if (Test-Path $PkgPath) {
            Write-Host "  Package: $PkgPath" -ForegroundColor Cyan
        }
    }
}

Write-Host ""
Write-Host "Installation location: C:\Program Files\PkgInspector" -ForegroundColor Yellow
Write-Host ""
Write-Host "To list certificates: .\build.ps1 -ListCerts" -ForegroundColor Gray
Write-Host "To build for specific arch: .\build.ps1 -Architecture x64" -ForegroundColor Gray
Write-Host "To skip .pkg creation: .\build.ps1 -SkipPkg" -ForegroundColor Gray
