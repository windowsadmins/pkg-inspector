using YamlDotNet.Serialization;

namespace PkgInspector.Models;

/// <summary>
/// build-info.yaml schema, mirroring cimipkg's
/// Cimian.CLI.Cimipkg.Models.BuildInfo so round-tripping a cimipkg-built MSI
/// shows every field the author configured (not just the first handful).
///
/// Fields that don't appear in a given package's YAML stay at their default —
/// the Package Info tab hides empty values so the UI stays quiet.
/// </summary>
public class BuildInfo
{
    // --- Legacy top-level fields kept for .pkg / .nupkg compatibility --------

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "author")]
    public string Author { get; set; } = string.Empty;

    [YamlMember(Alias = "license")]
    public string License { get; set; } = string.Empty;

    [YamlMember(Alias = "homepage")]
    public string Homepage { get; set; } = string.Empty;

    [YamlMember(Alias = "dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [YamlMember(Alias = "target")]
    public string Target { get; set; } = "/";

    // --- Shared with cimipkg --------------------------------------------------

    [YamlMember(Alias = "install_location")]
    public string InstallLocation { get; set; } = string.Empty;

    [YamlMember(Alias = "restart_action")]
    public string RestartAction { get; set; } = "None";

    [YamlMember(Alias = "product")]
    public ProductInfo? Product { get; set; }

    // --- cimipkg-specific top-level fields (all optional) --------------------

    [YamlMember(Alias = "install_arguments")]
    public string? InstallArguments { get; set; }

    [YamlMember(Alias = "valid_exit_codes")]
    public string? ValidExitCodes { get; set; }

    [YamlMember(Alias = "uninstall_arguments")]
    public string? UninstallArguments { get; set; }

    [YamlMember(Alias = "software_detection")]
    public string? SoftwareDetection { get; set; }

    [YamlMember(Alias = "postinstall_action")]
    public string? PostinstallAction { get; set; }

    [YamlMember(Alias = "signing_certificate")]
    public string? SigningCertificate { get; set; }

    [YamlMember(Alias = "signing_thumbprint")]
    public string? SigningThumbprint { get; set; }

    [YamlMember(Alias = "minimum_os_version")]
    public string? MinimumOsVersion { get; set; }

    [YamlMember(Alias = "category")]
    public string? Category { get; set; }

    [YamlMember(Alias = "icon")]
    public string? Icon { get; set; }

    [YamlMember(Alias = "blocking_applications")]
    public List<string>? BlockingApplications { get; set; }

    [YamlMember(Alias = "upgrade_code")]
    public string? UpgradeCode { get; set; }

    [YamlMember(Alias = "msi_properties")]
    public Dictionary<string, string>? MsiProperties { get; set; }
}

/// <summary>
/// Nested product block in build-info.yaml. Matches cimipkg's ProductInfo.
/// </summary>
public class ProductInfo
{
    [YamlMember(Alias = "identifier")]
    public string Identifier { get; set; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; set; } = string.Empty;

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "developer")]
    public string Developer { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "installer_type")]
    public string? InstallerType { get; set; }

    [YamlMember(Alias = "url")]
    public string? Url { get; set; }

    [YamlMember(Alias = "copyright")]
    public string? Copyright { get; set; }

    [YamlMember(Alias = "license")]
    public string? License { get; set; }

    [YamlMember(Alias = "tags")]
    public List<string>? Tags { get; set; }
}
