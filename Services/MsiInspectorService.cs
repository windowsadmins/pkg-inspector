using System.IO;
using PkgInspector.Models;
using WixToolset.Dtf.WindowsInstaller;

namespace PkgInspector.Services;

/// <summary>
/// Service for inspecting .msi package files using DTF (direct msi.dll interop).
/// Handles both cimipkg-built MSI (with CIMIAN_PKG_BUILD_INFO) and commercial MSI.
/// </summary>
public class MsiInspectorService
{
    /// <summary>
    /// Inspects an MSI file and extracts metadata, file list, and custom actions.
    /// </summary>
    public Task<PackageData> InspectPackageAsync(string msiPath)
    {
        if (!File.Exists(msiPath))
            throw new FileNotFoundException("MSI file not found", msiPath);

        var packageData = new PackageData
        {
            FilePath = msiPath,
            FileName = Path.GetFileName(msiPath)
        };

        using var db = new Database(msiPath, DatabaseOpenMode.ReadOnly);

        // Extract metadata from Property table
        LoadMsiMetadata(db, packageData);

        // Extract file list from File table
        LoadMsiFiles(db, packageData);

        // Extract custom actions as "scripts"
        LoadMsiCustomActions(db, packageData);

        // Build file tree from the file list
        BuildFileTree(packageData);

        // Check Authenticode signature
        CheckSignature(msiPath, packageData);

        return Task.FromResult(packageData);
    }

    private void LoadMsiMetadata(Database db, PackageData packageData)
    {
        var properties = ReadAllProperties(db);

        // Check for cimipkg-built MSI
        var buildInfoYaml = properties.GetValueOrDefault("CIMIAN_PKG_BUILD_INFO");
        if (!string.IsNullOrEmpty(buildInfoYaml))
        {
            packageData.RawMetadata = buildInfoYaml;
            packageData.Metadata = new BuildInfo
            {
                Name = properties.GetValueOrDefault("ProductName") ?? Path.GetFileNameWithoutExtension(packageData.FileName),
                Version = properties.GetValueOrDefault("CIMIAN_FULL_VERSION") ?? properties.GetValueOrDefault("ProductVersion") ?? "Unknown",
                Author = properties.GetValueOrDefault("Manufacturer") ?? "Unknown",
                Description = properties.GetValueOrDefault("ARPCOMMENTS") ?? "Cimian MSI package",
            };
        }
        else
        {
            // Commercial MSI — build metadata from Property table
            var rawLines = new List<string> { "# MSI Property Table" };
            foreach (var (key, value) in properties.OrderBy(p => p.Key))
            {
                if (value.Length < 200) // Skip very long values
                    rawLines.Add($"{key}: {value}");
            }
            packageData.RawMetadata = string.Join("\n", rawLines);

            packageData.Metadata = new BuildInfo
            {
                Name = properties.GetValueOrDefault("ProductName") ?? Path.GetFileNameWithoutExtension(packageData.FileName),
                Version = properties.GetValueOrDefault("ProductVersion") ?? "Unknown",
                Author = properties.GetValueOrDefault("Manufacturer") ?? "Unknown",
                Description = properties.GetValueOrDefault("ARPCOMMENTS") ?? "",
            };
        }

        // Store MSI-specific info in metadata
        var identifier = properties.GetValueOrDefault("CIMIAN_IDENTIFIER");
        if (!string.IsNullOrEmpty(identifier) && packageData.Metadata != null)
        {
            packageData.Metadata.Description +=
                $"\n\nProductCode: {properties.GetValueOrDefault("ProductCode")}" +
                $"\nUpgradeCode: {properties.GetValueOrDefault("UpgradeCode")}" +
                $"\nIdentifier: {identifier}";
        }
    }

    private void LoadMsiFiles(Database db, PackageData packageData)
    {
        if (!db.Tables.Contains("File"))
            return;

        var files = new List<Models.FileInfo>();

        using var view = db.OpenView("SELECT `File`, `FileName`, `FileSize`, `Component_` FROM `File`");
        view.Execute();

        foreach (var record in view)
        {
            using (record)
            {
                var fileName = record.GetString(2) ?? "";
                // MSI FileName format: "ShortName|LongName"
                var longName = fileName.Contains('|') ? fileName.Split('|')[1] : fileName;

                files.Add(new Models.FileInfo
                {
                    Name = longName,
                    RelativePath = longName,
                    Size = record.GetInteger(3),
                    IsDirectory = false
                });
            }
        }

        packageData.Files = files;
    }

    private void LoadMsiCustomActions(Database db, PackageData packageData)
    {
        if (!db.Tables.Contains("CustomAction"))
            return;

        var scripts = new List<ScriptInfo>();

        using var view = db.OpenView("SELECT `Action`, `Type`, `Target` FROM `CustomAction`");
        view.Execute();

        foreach (var record in view)
        {
            using (record)
            {
                var actionName = record.GetString(1) ?? "";
                var actionType = record.GetInteger(2);
                var target = record.GetString(3) ?? "";

                // Type 6 or 38 = VBScript, Type 5 or 37 = JScript, Type 102 = VBS+sync+continue
                var isScript = (actionType & 0x07) == 6 || (actionType & 0x07) == 5;
                // Type 51 = set property
                var isSetProp = (actionType & 0x3F) == 51;

                if (isScript && target.Length > 10)
                {
                    scripts.Add(new ScriptInfo
                    {
                        Name = actionName,
                        Type = actionName.Contains("Preinstall") ? "Pre-Install Action"
                            : actionName.Contains("Postinstall") ? "Post-Install Action"
                            : actionName.Contains("Uninstall") ? "Uninstall Action"
                            : "Custom Action",
                        Content = target,
                        RelativePath = $"CustomAction/{actionName}"
                    });
                }
            }
        }

        packageData.Scripts = scripts;
    }

    private void BuildFileTree(PackageData packageData)
    {
        var root = new FileTreeNode
        {
            Name = "Files",
            IsDirectory = true,
            Children = new()
        };

        foreach (var file in packageData.Files.OrderBy(f => f.Name))
        {
            root.Children.Add(new FileTreeNode
            {
                Name = file.Name,
                FullPath = file.RelativePath,
                IsDirectory = false,
                Size = file.Size
            });
        }

        packageData.FileTree = new List<FileTreeNode> { root };
    }

    private void CheckSignature(string msiPath, PackageData packageData)
    {
        try
        {
            var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadSignedFile(msiPath);
            packageData.IsSigned = true;
            packageData.SignedBy = cert.Subject;
        }
        catch
        {
            packageData.IsSigned = false;
            packageData.SignedBy = string.Empty;
        }
    }

    private static Dictionary<string, string> ReadAllProperties(Database db)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!db.Tables.Contains("Property"))
            return properties;

        using var view = db.OpenView("SELECT `Property`, `Value` FROM `Property`");
        view.Execute();

        foreach (var record in view)
        {
            using (record)
            {
                var name = record.GetString(1);
                var value = record.GetString(2);
                if (!string.IsNullOrEmpty(name))
                    properties[name] = value ?? "";
            }
        }

        return properties;
    }
}
