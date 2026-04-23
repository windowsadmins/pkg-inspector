namespace PkgInspector.Models;

/// <summary>
/// Package container format. Determines which Services implementation was used
/// and which fields will be populated on <see cref="PackageData"/>.
/// </summary>
public enum PackageFormat
{
    Unknown,
    Pkg,    // cimipkg ZIP-style (payload/ + scripts/ + build-info.yaml)
    Nupkg,  // NuGet / Chocolatey
    Msi,    // Windows Installer — cimipkg-built or any third-party MSI
}

/// <summary>
/// Container for all inspected package data
/// </summary>
public class PackageData
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public PackageFormat Format { get; set; } = PackageFormat.Unknown;

    public BuildInfo? Metadata { get; set; }
    public string RawMetadata { get; set; } = string.Empty;

    public List<FileInfo> Files { get; set; } = new();
    public List<ScriptInfo> Scripts { get; set; } = new();
    public List<FileTreeNode> FileTree { get; set; } = new();

    public bool IsSigned { get; set; }
    public string SignedBy { get; set; } = string.Empty;

    // --- MSI-specific identity fields ----------------------------------------
    // Populated only for Format == Msi. UI hides rows with empty values so the
    // .pkg / .nupkg views stay uncluttered.

    public string ProductCode { get; set; } = string.Empty;
    public string UpgradeCode { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string FullVersion { get; set; } = string.Empty;

    /// <summary>
    /// True when this is a cimipkg-produced MSI (i.e. Property table contains
    /// CIMIAN_PKG_BUILD_INFO). Lets the UI offer richer views for known-shape
    /// MSIs while still working on any third-party MSI.
    /// </summary>
    public bool IsCimipkgMsi { get; set; }
}

/// <summary>
/// Information about a file in the package
/// </summary>
public class FileInfo
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public string SizeFormatted => FormatBytes(Size);
    public bool IsDirectory { get; set; }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Information about a script in the package
/// </summary>
public class ScriptInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}

/// <summary>
/// Node in the file tree
/// </summary>
public class FileTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public string SizeFormatted => IsDirectory ? "" : FormatBytes(Size);
    public string Icon => IsDirectory ? "📁" : "📄";
    public List<FileTreeNode> Children { get; set; } = new();

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
