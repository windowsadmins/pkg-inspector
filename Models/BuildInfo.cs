using YamlDotNet.Serialization;

namespace PkgInspector.Models;

/// <summary>
/// Represents the build-info.yaml metadata for package generation
/// </summary>
public class BuildInfo
{
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

    [YamlMember(Alias = "install_location")]
    public string InstallLocation { get; set; } = string.Empty;

    [YamlMember(Alias = "restart_action")]
    public string RestartAction { get; set; } = "None";

    // Support nested product structure
    [YamlMember(Alias = "product")]
    public ProductInfo? Product { get; set; }
}

/// <summary>
/// Nested product information in build-info.yaml
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
}
