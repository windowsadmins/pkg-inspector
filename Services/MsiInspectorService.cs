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
            FileName = Path.GetFileName(msiPath),
            Format = PackageFormat.Msi,
        };

        using var db = new Database(msiPath, DatabaseOpenMode.ReadOnly);

        // Extract metadata from Property table
        LoadMsiMetadata(db, packageData);

        // Derive architecture from the Summary Information stream ("Intel" / "x64"
        // / "Arm64" in the Template field). Kept separate from LoadMsiMetadata so
        // a SummaryInfo read failure can't disrupt the main metadata path.
        LoadArchitecture(db, packageData);

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

    private static void LoadArchitecture(Database db, PackageData packageData)
    {
        try
        {
            var template = db.SummaryInfo.Template;
            if (string.IsNullOrEmpty(template)) return;

            // Template format: "Platform;LanguageID" e.g. "x64;1033" or "Intel;1033".
            packageData.Architecture = template.Contains("x64", StringComparison.OrdinalIgnoreCase) ? "x64"
                : template.Contains("Arm64", StringComparison.OrdinalIgnoreCase) ? "arm64"
                : template.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "x86"
                : string.Empty;
        }
        catch
        {
            // SummaryInfo read failed — leave Architecture blank. UI hides the row.
        }
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

        // Surface MSI identity as first-class fields on PackageData rather
        // than string-concatenating it into Description. The Package Info
        // tab has a dedicated "MSI Identity" panel that binds these and
        // hides rows whose values are empty (so third-party MSIs without a
        // CIMIAN_IDENTIFIER still get ProductCode + UpgradeCode shown).
        packageData.ProductCode = properties.GetValueOrDefault("ProductCode") ?? string.Empty;
        packageData.UpgradeCode = properties.GetValueOrDefault("UpgradeCode") ?? string.Empty;
        packageData.Identifier = properties.GetValueOrDefault("CIMIAN_IDENTIFIER") ?? string.Empty;
        packageData.FullVersion = properties.GetValueOrDefault("CIMIAN_FULL_VERSION") ?? string.Empty;
        packageData.IsCimipkgMsi = !string.IsNullOrEmpty(properties.GetValueOrDefault("CIMIAN_PKG_BUILD_INFO"));

        // If the cimipkg YAML had product.identifier set but CIMIAN_IDENTIFIER
        // is missing from the Property table (older cimipkg builds), backfill
        // from the parsed metadata so the UI still has a value to show.
        if (string.IsNullOrEmpty(packageData.Identifier) && packageData.Metadata?.Product != null)
        {
            packageData.Identifier = packageData.Metadata.Product.Identifier;
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

                // cimipkg emits a "# No preinstall scripts" / "# No postinstall
                // scripts" / "# No uninstall scripts" placeholder when a phase
                // has no user-authored script. The custom action still exists
                // in the sequence (sequencing tables want it), but there's
                // nothing worth showing — skip the entry so the Scripts tab
                // only lists real scripts.
                if (isCimipkgScript && IsCimipkgPlaceholder(decodedPs1!)) continue;

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

    private static bool IsCimipkgPlaceholder(string content)
    {
        // cimipkg stubs are a single short comment line: "# No preinstall
        // scripts", "# No postinstall scripts", "# No uninstall scripts".
        // Match the shape rather than hard-coding phase names so any future
        // cimipkg stub in the same family also collapses.
        var trimmed = content.Trim();
        return trimmed.StartsWith("# No ", StringComparison.Ordinal) &&
               trimmed.EndsWith(" scripts", StringComparison.Ordinal) &&
               !trimmed.Contains('\n');
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
