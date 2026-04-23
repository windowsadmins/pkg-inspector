using System.IO;
using PkgInspector.Models;
using WixToolset.Dtf.WindowsInstaller;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PkgInspector.Services;

/// <summary>
/// Service for inspecting .msi package files using DTF (direct msi.dll interop).
/// Handles both cimipkg-built MSI (with CIMIAN_PKG_BUILD_INFO) and any third-party MSI.
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

        var properties = ReadAllProperties(db);
        LoadMsiMetadata(properties, packageData);
        LoadArchitecture(db, packageData);
        LoadMsiFiles(db, packageData);
        LoadMsiCustomActions(db, packageData);
        BuildFileTree(packageData);
        CheckSignature(msiPath, packageData);

        return Task.FromResult(packageData);
    }

    private void LoadMsiMetadata(Dictionary<string, string> properties, PackageData packageData)
    {
        // MSI identity — always populated regardless of whether this is a cimipkg MSI
        packageData.ProductCode = properties.GetValueOrDefault("ProductCode") ?? string.Empty;
        packageData.UpgradeCode = properties.GetValueOrDefault("UpgradeCode") ?? string.Empty;
        packageData.Identifier = properties.GetValueOrDefault("CIMIAN_IDENTIFIER") ?? string.Empty;
        packageData.FullVersion = properties.GetValueOrDefault("CIMIAN_FULL_VERSION") ?? string.Empty;

        var buildInfoYaml = properties.GetValueOrDefault("CIMIAN_PKG_BUILD_INFO");
        packageData.IsCimipkgMsi = !string.IsNullOrEmpty(buildInfoYaml);

        if (packageData.IsCimipkgMsi)
        {
            packageData.RawMetadata = buildInfoYaml!;
            packageData.Metadata = DeserializeBuildInfo(buildInfoYaml!, packageData);
            return;
        }

        // Third-party MSI: synthesise a YAML-ish dump of the Property table as the
        // "raw metadata" view, and a BuildInfo from the ARP fields users expect.
        packageData.RawMetadata = FormatPropertyDump(properties);
        packageData.Metadata = new BuildInfo
        {
            Name = properties.GetValueOrDefault("ProductName")
                ?? Path.GetFileNameWithoutExtension(packageData.FileName),
            Version = properties.GetValueOrDefault("ProductVersion") ?? "Unknown",
            Author = properties.GetValueOrDefault("Manufacturer") ?? "Unknown",
            Description = properties.GetValueOrDefault("ARPCOMMENTS") ?? string.Empty,
            Homepage = properties.GetValueOrDefault("ARPURLINFOABOUT") ?? string.Empty,
            InstallLocation = properties.GetValueOrDefault("INSTALLDIR")
                ?? properties.GetValueOrDefault("APPLICATIONFOLDER") ?? string.Empty,
        };
    }

    private BuildInfo DeserializeBuildInfo(string yaml, PackageData packageData)
    {
        try
        {
            var parsed = _yamlDeserializer.Deserialize<BuildInfo>(yaml) ?? new BuildInfo();

            // Flatten the nested product: block into the top-level BuildInfo fields
            // the UI currently binds against, but keep Product around so consumers
            // (and the Metadata tab) still see the whole structure.
            if (parsed.Product != null)
            {
                if (string.IsNullOrWhiteSpace(parsed.Name))
                    parsed.Name = parsed.Product.Name;
                if (string.IsNullOrWhiteSpace(parsed.Version))
                    parsed.Version = parsed.Product.Version;
                if (string.IsNullOrWhiteSpace(parsed.Author))
                    parsed.Author = parsed.Product.Developer;
                if (string.IsNullOrWhiteSpace(parsed.Description))
                    parsed.Description = parsed.Product.Description;
                if (string.IsNullOrWhiteSpace(parsed.License))
                    parsed.License = parsed.Product.License ?? string.Empty;
                if (string.IsNullOrWhiteSpace(parsed.Homepage))
                    parsed.Homepage = parsed.Product.Url ?? string.Empty;

                if (string.IsNullOrWhiteSpace(packageData.Identifier))
                    packageData.Identifier = parsed.Product.Identifier;
            }

            return parsed;
        }
        catch
        {
            // Corrupted or unexpected YAML shape — fall back to a minimal BuildInfo
            // from the raw properties so the UI still populates something useful.
            return new BuildInfo
            {
                Name = Path.GetFileNameWithoutExtension(packageData.FileName),
                Version = packageData.FullVersion,
                Description = "build-info.yaml embedded in MSI could not be parsed",
            };
        }
    }

    private static string FormatPropertyDump(IDictionary<string, string> properties)
    {
        var lines = new List<string> { "# MSI Property Table" };
        foreach (var (key, value) in properties.OrderBy(p => p.Key))
        {
            if (value.Length < 200) // Skip very long values (build-info YAML, CABs, etc.)
                lines.Add($"{key}: {value}");
        }
        return string.Join("\n", lines);
    }

    private static void LoadArchitecture(Database db, PackageData packageData)
    {
        try
        {
            var template = db.SummaryInfo.Template;
            if (string.IsNullOrEmpty(template)) return;

            // Template format: "Platform;LanguageID" e.g. "x64;1033" or "Intel;1033"
            packageData.Architecture = template.Contains("x64", StringComparison.OrdinalIgnoreCase) ? "x64"
                : template.Contains("Arm64", StringComparison.OrdinalIgnoreCase) ? "arm64"
                : template.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "x86"
                : string.Empty;
        }
        catch
        {
            // SummaryInfo read failed — leave blank
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

                // cimipkg emits Type 102 (inline VBS, sync, continue) for its
                // chunked-base64 PowerShell wrappers. Decode those back to .ps1.
                if (MsiScriptDecoder.LooksLikeCimipkgWrapper(target))
                {
                    scripts.AddRange(MsiScriptDecoder.DecodeToScripts(actionName, target));
                    continue;
                }

                // Other script-type custom actions: VBS (6/38) or JScript (5/37).
                var isScript = (actionType & 0x07) == 6 || (actionType & 0x07) == 5;
                if (isScript && target.Length > 10)
                {
                    scripts.Add(new ScriptInfo
                    {
                        Name = actionName,
                        Type = ClassifyActionName(actionName),
                        Content = target,
                        RelativePath = $"CustomAction/{actionName}"
                    });
                }
            }
        }

        packageData.Scripts = scripts;
    }

    private static string ClassifyActionName(string actionName)
    {
        if (actionName.Contains("Preinstall", StringComparison.OrdinalIgnoreCase))
            return "Pre-Install Action";
        if (actionName.Contains("Postinstall", StringComparison.OrdinalIgnoreCase))
            return "Post-Install Action";
        if (actionName.Contains("Uninstall", StringComparison.OrdinalIgnoreCase))
            return "Uninstall Action";
        return "Custom Action";
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
