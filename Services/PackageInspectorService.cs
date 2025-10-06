using System.IO;
using System.IO.Compression;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using PkgInspector.Models;

namespace PkgInspector.Services;

/// <summary>
/// Service for inspecting .pkg and .nupkg package files
/// </summary>
public class PackageInspectorService
{
    private readonly IDeserializer _yamlDeserializer;

    public PackageInspectorService()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Inspects a package file and extracts all relevant information
    /// </summary>
    public async Task<PackageData> InspectPackageAsync(string packagePath)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Package file not found", packagePath);
        }

        var packageData = new PackageData
        {
            FilePath = packagePath,
            FileName = Path.GetFileName(packagePath)
        };

        string tempDir = Path.Combine(Path.GetTempPath(), $"pkginspector_{Guid.NewGuid()}");

        try
        {
            // Extract package to temp directory
            Directory.CreateDirectory(tempDir);
            await Task.Run(() => ZipFile.ExtractToDirectory(packagePath, tempDir));

            // Load metadata
            await LoadMetadataAsync(tempDir, packageData);

            // Scan files
            await ScanFilesAsync(tempDir, packageData);

            // Load scripts
            await LoadScriptsAsync(tempDir, packageData);

            // Build file tree
            BuildFileTree(packageData);

            // Check for digital signature
            CheckPackageSignature(packagePath, packageData);

            return packageData;
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private async Task LoadMetadataAsync(string extractedPath, PackageData packageData)
    {
        // Try to find build-info.yaml
        string buildInfoPath = Path.Combine(extractedPath, "build-info.yaml");
        
        if (File.Exists(buildInfoPath))
        {
            string yamlContent = await File.ReadAllTextAsync(buildInfoPath);
            packageData.RawMetadata = yamlContent;
            
            try
            {
                var metadata = _yamlDeserializer.Deserialize<BuildInfo>(yamlContent);
                // Ensure metadata is not null and has at least a name
                if (metadata != null)
                {
                    // If product section exists, merge it into main metadata
                    if (metadata.Product != null)
                    {
                        if (string.IsNullOrWhiteSpace(metadata.Name))
                            metadata.Name = metadata.Product.Name;
                        if (string.IsNullOrWhiteSpace(metadata.Version))
                            metadata.Version = metadata.Product.Version;
                        if (string.IsNullOrWhiteSpace(metadata.Author))
                            metadata.Author = metadata.Product.Developer;
                        if (string.IsNullOrWhiteSpace(metadata.Description))
                            metadata.Description = metadata.Product.Description;
                    }
                    
                    // Set defaults for empty fields
                    if (string.IsNullOrWhiteSpace(metadata.Name))
                        metadata.Name = Path.GetFileNameWithoutExtension(packageData.FileName);
                    if (string.IsNullOrWhiteSpace(metadata.Version))
                        metadata.Version = "Unknown";
                    if (string.IsNullOrWhiteSpace(metadata.Description))
                        metadata.Description = "No description available";
                    
                    packageData.Metadata = metadata;
                }
                else
                {
                    packageData.Metadata = CreateDefaultMetadata(packageData.FileName);
                }
            }
            catch
            {
                // If deserialization fails, create default metadata
                packageData.Metadata = CreateDefaultMetadata(packageData.FileName);
            }
        }
        else
        {
            // Try to find .nuspec for .nupkg files
            var nuspecFiles = Directory.GetFiles(extractedPath, "*.nuspec", SearchOption.TopDirectoryOnly);
            if (nuspecFiles.Length > 0)
            {
                packageData.RawMetadata = await File.ReadAllTextAsync(nuspecFiles[0]);
                packageData.Metadata = await ParseNuspecMetadata(nuspecFiles[0], packageData.FileName);
            }
            else
            {
                packageData.RawMetadata = "No metadata file found";
                packageData.Metadata = CreateDefaultMetadata(packageData.FileName);
            }
        }
    }

    private BuildInfo CreateDefaultMetadata(string fileName)
    {
        return new BuildInfo
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            Version = "Unknown",
            Description = "No metadata file found",
            Author = "Unknown",
            License = "Unknown",
            Target = "/",
            RestartAction = "None"
        };
    }

    private async Task ScanFilesAsync(string extractedPath, PackageData packageData)
    {
        string payloadPath = Path.Combine(extractedPath, "payload");
        
        if (!Directory.Exists(payloadPath))
        {
            return;
        }

        await Task.Run(() =>
        {
            var files = new List<Models.FileInfo>();
            ScanDirectory(payloadPath, payloadPath, files);
            packageData.Files = files;
        });
    }

    private void ScanDirectory(string rootPath, string currentPath, List<Models.FileInfo> files)
    {
        try
        {
            // Add directories
            foreach (var dir in Directory.GetDirectories(currentPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                files.Add(new Models.FileInfo
                {
                    Name = dirInfo.Name,
                    RelativePath = Path.GetRelativePath(rootPath, dir),
                    IsDirectory = true,
                    Size = 0
                });
                
                ScanDirectory(rootPath, dir, files);
            }

            // Add files
            foreach (var file in Directory.GetFiles(currentPath))
            {
                var fileInfo = new System.IO.FileInfo(file);
                files.Add(new Models.FileInfo
                {
                    Name = fileInfo.Name,
                    RelativePath = Path.GetRelativePath(rootPath, file),
                    IsDirectory = false,
                    Size = fileInfo.Length
                });
            }
        }
        catch
        {
            // Skip directories we can't access
        }
    }

    private async Task LoadScriptsAsync(string extractedPath, PackageData packageData)
    {
        var scripts = new List<ScriptInfo>();

        // Check scripts/ directory (preferred)
        string scriptsPath = Path.Combine(extractedPath, "scripts");
        if (Directory.Exists(scriptsPath))
        {
            await AddScriptsFromDirectory(scriptsPath, "scripts", scripts);
        }

        // Check tools/ directory (Chocolatey compatibility)
        string toolsPath = Path.Combine(extractedPath, "tools");
        if (Directory.Exists(toolsPath))
        {
            await AddScriptsFromDirectory(toolsPath, "tools", scripts);
        }

        packageData.Scripts = scripts;
    }

    private async Task AddScriptsFromDirectory(string directoryPath, string directoryName, List<ScriptInfo> scripts)
    {
        var scriptFiles = Directory.GetFiles(directoryPath, "*.ps1", SearchOption.TopDirectoryOnly);
        
        foreach (var scriptFile in scriptFiles)
        {
            var fileName = Path.GetFileName(scriptFile);
            var scriptType = GetScriptType(fileName);
            var content = await File.ReadAllTextAsync(scriptFile);

            scripts.Add(new ScriptInfo
            {
                Name = fileName,
                Type = scriptType,
                Content = content,
                RelativePath = Path.Combine(directoryName, fileName)
            });
        }
    }

    private string GetScriptType(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "preinstall.ps1" => "Pre-Install Script",
            "postinstall.ps1" => "Post-Install Script",
            "chocolateybeforeinstall.ps1" => "Chocolatey Pre-Install Script",
            "chocolateyinstall.ps1" => "Chocolatey Install Script",
            "chocolateyuninstall.ps1" => "Chocolatey Uninstall Script",
            _ => "PowerShell Script"
        };
    }

    private void BuildFileTree(PackageData packageData)
    {
        var root = new FileTreeNode
        {
            Name = "payload",
            IsDirectory = true,
            Children = new()
        };

        foreach (var file in packageData.Files.OrderBy(f => f.RelativePath))
        {
            AddToTree(root, file);
        }

        packageData.FileTree = new List<FileTreeNode> { root };
    }

    private void AddToTree(FileTreeNode parent, Models.FileInfo fileInfo)
    {
        var parts = fileInfo.RelativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = parent;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var isLast = i == parts.Length - 1;

            if (isLast && !fileInfo.IsDirectory)
            {
                // Add file
                current.Children.Add(new FileTreeNode
                {
                    Name = part,
                    FullPath = fileInfo.RelativePath,
                    IsDirectory = false,
                    Size = fileInfo.Size
                });
            }
            else
            {
                // Add or navigate to directory
                var existing = current.Children.FirstOrDefault(c => c.Name == part && c.IsDirectory);
                if (existing == null)
                {
                    existing = new FileTreeNode
                    {
                        Name = part,
                        IsDirectory = true,
                        Children = new()
                    };
                    current.Children.Add(existing);
                }
                current = existing;
            }
        }
    }

    private void CheckPackageSignature(string packagePath, PackageData packageData)
    {
        // Check if signature information is in metadata
        if (packageData.Metadata != null && !string.IsNullOrEmpty(packageData.RawMetadata))
        {
            // Look for signature info in YAML metadata
            var lines = packageData.RawMetadata.Split('\n');
            foreach (var line in lines)
            {                if (line.TrimStart().StartsWith("signed_at:") || line.TrimStart().StartsWith("certificate_hash:"))
                {
                    packageData.IsSigned = true;
                }
                if (line.TrimStart().StartsWith("certificate_subject:"))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        packageData.SignedBy = parts[1].Trim();
                    }
                }
            }
        }

        // If no signature info found in metadata, packages are unsigned (ZIP files don't support Authenticode)
        if (!packageData.IsSigned)
        {
            packageData.IsSigned = false;
            packageData.SignedBy = string.Empty;
        }
    }

    private async Task<BuildInfo> ParseNuspecMetadata(string nuspecPath, string fileName)
    {
        try
        {
            var xmlContent = await File.ReadAllTextAsync(nuspecPath);
            var xml = System.Xml.Linq.XDocument.Parse(xmlContent);
            var ns = xml.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;
            var metadata = xml.Root?.Element(ns + "metadata");

            if (metadata != null)
            {
                return new BuildInfo
                {
                    Name = metadata.Element(ns + "id")?.Value ?? Path.GetFileNameWithoutExtension(fileName),
                    Version = metadata.Element(ns + "version")?.Value ?? "Unknown",
                    Description = metadata.Element(ns + "description")?.Value ?? "No description",
                    Author = metadata.Element(ns + "authors")?.Value ?? "Unknown",
                    License = metadata.Element(ns + "license")?.Value ?? metadata.Element(ns + "licenseUrl")?.Value ?? "Unknown",
                    Homepage = metadata.Element(ns + "projectUrl")?.Value ?? string.Empty,
                    Target = "/",
                    RestartAction = "None"
                };
            }
        }
        catch
        {
            // Fall through to default
        }

        return CreateDefaultMetadata(fileName);
    }
}
