using System.IO;
using PkgInspector.Models;
using WixToolset.Dtf.WindowsInstaller;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PkgInspector.Services;

/// <summary>
/// Service for inspecting .msi package files using DTF (direct msi.dll interop).
/// Handles both cimipkg-built MSI (with CIMIAN_PKG_BUILD_INFO) and commercial MSI.
/// </summary>
public class MsiInspectorService
{
    private readonly IDeserializer _yamlDeserializer;

    public MsiInspectorService()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

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

            // Deserialize the embedded build-info.yaml through the same YAML
            // path the .pkg / .nupkg inspector uses so the structured Metadata
            // fields match parity — identifier, install_location, nested
            // product block, etc. Fall back to reading MSI properties if the
            // YAML is malformed for any reason.
            //
            // PackageInspectorService catches the broad Exception here so that
            // type conversion errors, invalid casts, and anything else
            // YamlDotNet might throw during deserialization degrade
            // gracefully into the property-table fallback instead of
            // bubbling out and failing the whole inspection. Match that
            // behavior so cimipkg MSIs with unusual/malformed build-info.yaml
            // still render something useful.
            BuildInfo? metadata = null;
            try
            {
                metadata = _yamlDeserializer.Deserialize<BuildInfo>(buildInfoYaml);
            }
            catch (Exception)
            {
                metadata = null;
            }

            if (metadata != null && metadata.Product != null)
            {
                // Merge the nested product block up into the top-level fields
                // so the UI can show them without having to navigate into
                // Product.*.
                if (string.IsNullOrEmpty(metadata.Name)) metadata.Name = metadata.Product.Name;
                if (string.IsNullOrEmpty(metadata.Version)) metadata.Version = metadata.Product.Version;
                if (string.IsNullOrEmpty(metadata.Author)) metadata.Author = metadata.Product.Developer;
                if (string.IsNullOrEmpty(metadata.Description)) metadata.Description = metadata.Product.Description;
            }

            packageData.Metadata = metadata ?? new BuildInfo
            {
                Name = properties.GetValueOrDefault("ProductName") ?? Path.GetFileNameWithoutExtension(packageData.FileName),
                Version = properties.GetValueOrDefault("CIMIAN_FULL_VERSION") ?? properties.GetValueOrDefault("ProductVersion") ?? "Unknown",
                Author = properties.GetValueOrDefault("Manufacturer") ?? "Unknown",
                Description = properties.GetValueOrDefault("ARPCOMMENTS") ?? "Cimian MSI package",
            };

            // Backfill any BuildInfo field the YAML left blank from the MSI
            // Property table so the UI always has something to display. This
            // has to cover Description too — the Description += "..." block
            // below appends ProductCode/UpgradeCode/Identifier unconditionally,
            // and starting from an empty string would leave the UI with a
            // leading blank line.
            if (string.IsNullOrEmpty(packageData.Metadata.Name))
                packageData.Metadata.Name = properties.GetValueOrDefault("ProductName") ?? Path.GetFileNameWithoutExtension(packageData.FileName);
            if (string.IsNullOrEmpty(packageData.Metadata.Version))
                packageData.Metadata.Version = properties.GetValueOrDefault("CIMIAN_FULL_VERSION") ?? properties.GetValueOrDefault("ProductVersion") ?? "Unknown";
            if (string.IsNullOrEmpty(packageData.Metadata.Author))
                packageData.Metadata.Author = properties.GetValueOrDefault("Manufacturer") ?? "Unknown";
            if (string.IsNullOrEmpty(packageData.Metadata.Description))
                packageData.Metadata.Description = properties.GetValueOrDefault("ARPCOMMENTS") ?? "Cimian MSI package";
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

                if (!isScript || target.Length <= 10) continue;

                // cimipkg-built MSIs embed the real PowerShell source as a
                // chunked base64 blob inside the VBS. Decode it so the
                // Scripts tab shows the preinstall/postinstall.ps1 content
                // users actually wrote, not 40 KB of unreadable VBS.
                var decodedPs1 = CimipkgVbsDecoder.TryDecode(target);
                var isCimipkgScript = decodedPs1 != null;

                string scriptName;
                string scriptType;
                string relativePath;

                if (isCimipkgScript)
                {
                    scriptName = actionName switch
                    {
                        "CimianPreinstall" => "preinstall.ps1",
                        "CimianPostinstall" => "postinstall.ps1",
                        "CimianUninstall" => "uninstall.ps1",
                        _ => $"{actionName}.ps1"
                    };
                    scriptType = actionName switch
                    {
                        "CimianPreinstall" => "Pre-Install Script",
                        "CimianPostinstall" => "Post-Install Script",
                        "CimianUninstall" => "Uninstall Script",
                        _ => "PowerShell Script"
                    };
                    // Mirror the layout the .pkg inspector produces so the UI
                    // treats cimipkg MSI scripts identically to .pkg scripts.
                    relativePath = $"scripts/{scriptName}";
                }
                else
                {
                    scriptName = actionName;
                    scriptType = actionName.Contains("Preinstall") ? "Pre-Install Action"
                        : actionName.Contains("Postinstall") ? "Post-Install Action"
                        : actionName.Contains("Uninstall") ? "Uninstall Action"
                        : "Custom Action";
                    relativePath = $"CustomAction/{actionName}";
                }

                scripts.Add(new ScriptInfo
                {
                    Name = scriptName,
                    Type = scriptType,
                    Content = decodedPs1 ?? target,
                    RelativePath = relativePath
                });
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
#pragma warning disable SYSLIB0057
            var cert = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromSignedFile(msiPath);
#pragma warning restore SYSLIB0057
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
