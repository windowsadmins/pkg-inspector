namespace PkgInspector.Models;

/// <summary>
/// Package container format. Selected per-inspection by the MainWindow based
/// on file extension so downstream UI (and future format-specific views) can
/// key off it rather than sniffing the path again.
/// </summary>
public enum PackageFormat
{
    Unknown,
    Nupkg,  // NuGet / Chocolatey (also the legacy .pkg ZIP shape)
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
    // Populated only for Format == Msi. The Package Info tab hides the rows
    // whose values are empty so .nupkg views stay uncluttered.

    public string ProductCode { get; set; } = string.Empty;
    public string UpgradeCode { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string FullVersion { get; set; } = string.Empty;

    /// <summary>
    /// True when the MSI carries CIMIAN_PKG_BUILD_INFO (i.e. was produced by
    /// cimipkg). Lets future features offer richer views for known-shape MSIs
    /// without breaking third-party MSI support.
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
