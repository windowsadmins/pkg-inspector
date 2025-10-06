using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PkgInspector;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
    
    private const int ATTACH_PARENT_PROCESS = -1;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        // Check if we should run in CLI mode
        var args = e.Args;
        
        // CLI mode detection: has any flags or specific commands
        bool isCliMode = args.Length > 0 && (
            args.Any(a => a.StartsWith("-") || a.StartsWith("--")) ||
            args.Any(a => a.Equals("help", StringComparison.OrdinalIgnoreCase))
        );
        
        if (isCliMode)
        {
            // Attach to parent console for CLI output
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                AllocConsole();
            }
            
            // Run CLI mode - suppress WPF startup
            Shutdown(RunCliMode(args));
            return;
        }
        
        // Otherwise, continue with normal GUI startup
        base.OnStartup(e);
    }
    
    private int RunCliMode(string[] args)
    {
        string? packagePath = null;
        string? revealFile = null;
        bool revealScripts = false;
        bool showSignature = false;
        bool showComponentPackages = false;
        bool quiet = false;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            if (arg == "--reveal-file" || arg == "-f")
            {
                if (i + 1 < args.Length)
                {
                    revealFile = args[++i];
                }
            }
            else if (arg == "--reveal-scripts" || arg == "-s")
            {
                revealScripts = true;
            }
            else if (arg == "--show-signature" || arg == "-g")
            {
                showSignature = true;
            }
            else if (arg == "--show-component-packages" || arg == "-p")
            {
                showComponentPackages = true;
            }
            else if (arg == "--quiet" || arg == "-q")
            {
                quiet = true;
            }
            else if (arg == "--help" || arg == "-h" || arg == "help")
            {
                ShowHelp();
                return 0;
            }
            else if (!arg.StartsWith("-"))
            {
                packagePath = arg;
            }
        }

        if (string.IsNullOrEmpty(packagePath))
        {
            Console.Error.WriteLine("Error: No package path specified");
            ShowHelp();
            return 1;
        }

        if (!System.IO.File.Exists(packagePath))
        {
            Console.Error.WriteLine($"Error: Package file not found: {packagePath}");
            return 1;
        }

        // Make path absolute
        packagePath = System.IO.Path.GetFullPath(packagePath);

        try
        {
            if (showSignature)
            {
                return ShowSignatureInfo(packagePath, quiet);
            }
            else if (showComponentPackages)
            {
                return ShowComponentPackages(packagePath, quiet);
            }
            else
            {
                // Launch GUI with package path and reveal options
                return LaunchGuiWithPackage(packagePath, revealFile, revealScripts);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine("pkginspector - Package Inspector");
        Console.WriteLine();
        Console.WriteLine("Usage: pkginspector [options] [package-file]");
        Console.WriteLine();
        Console.WriteLine("Without arguments or flags: Opens GUI");
        Console.WriteLine("With package file only:     Opens GUI with package loaded");
        Console.WriteLine("With flags:                 Runs in CLI mode");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --reveal-file <path>      Open package and navigate to specified file");
        Console.WriteLine("  -s, --reveal-scripts          Open package and show Scripts tab");
        Console.WriteLine("  -g, --show-signature          Show package signature information (CLI)");
        Console.WriteLine("  -p, --show-component-packages List component packages (CLI)");
        Console.WriteLine("  -q, --quiet                   Minimal output for scripting");
        Console.WriteLine("  -h, --help                    Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  pkginspector                               # Open GUI");
        Console.WriteLine("  pkginspector MyPackage.pkg                 # Open package in GUI");
        Console.WriteLine("  pkginspector -s MyPackage.pkg              # Open and show scripts");
        Console.WriteLine("  pkginspector -f /Apps MyPackage.pkg        # Open and navigate to folder");
        Console.WriteLine("  pkginspector -g MyPackage.pkg              # Show signature info (CLI)");
        Console.WriteLine("  pkginspector -p MyPackage.pkg              # List component packages (CLI)");
    }

    private int LaunchGuiWithPackage(string packagePath, string? revealFile, bool revealScripts)
    {
        // Set environment variables for GUI to read
        if (!string.IsNullOrEmpty(revealFile))
        {
            Environment.SetEnvironmentVariable("PKGINSPECTOR_REVEAL_FILE", revealFile);
        }
        if (revealScripts)
        {
            Environment.SetEnvironmentVariable("PKGINSPECTOR_REVEAL_SCRIPTS", "true");
        }
        
        // Start new instance in GUI mode (without CLI flags)
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? "pkginspector.exe",
            Arguments = $"\"{packagePath}\"",
            UseShellExecute = true
        };
        
        Process.Start(startInfo);
        return 0;
    }

    private int ShowSignatureInfo(string packagePath, bool quiet)
    {
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(packagePath);
            var buildInfoEntry = zip.GetEntry("build-info.yaml");
            
            if (buildInfoEntry != null)
            {
                using var stream = buildInfoEntry.Open();
                using var reader = new System.IO.StreamReader(stream);
                var content = reader.ReadToEnd();
                
                var lines = content.Split('\n');
                string? signedAt = null;
                string? certSubject = null;
                string? certHash = null;
                
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("signed_at:"))
                        signedAt = line.Split(':', 2)[1].Trim();
                    else if (line.TrimStart().StartsWith("certificate_subject:"))
                        certSubject = line.Split(':', 2)[1].Trim();
                    else if (line.TrimStart().StartsWith("certificate_hash:"))
                        certHash = line.Split(':', 2)[1].Trim();
                }
                
                if (!string.IsNullOrEmpty(certSubject))
                {
                    if (!quiet)
                    {
                        Console.WriteLine($"Signature information for \"{System.IO.Path.GetFileName(packagePath)}\"");
                        Console.WriteLine($"   summary                 : Signed by \"{certSubject}\"");
                        if (!string.IsNullOrEmpty(signedAt))
                            Console.WriteLine($"   signed at               : {signedAt}");
                        if (!string.IsNullOrEmpty(certHash))
                            Console.WriteLine($"   certificate hash        : {certHash}");
                    }
                    else
                    {
                        Console.WriteLine($"Signed|{certSubject}|{signedAt ?? ""}|{certHash ?? ""}");
                    }
                }
                else
                {
                    if (!quiet)
                    {
                        Console.WriteLine($"Package \"{System.IO.Path.GetFileName(packagePath)}\" is not signed");
                    }
                    else
                    {
                        Console.WriteLine("Unsigned");
                    }
                }
            }
            else
            {
                Console.WriteLine("No signature information found (no build-info.yaml)");
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading package: {ex.Message}");
            return 1;
        }
    }

    private int ShowComponentPackages(string packagePath, bool quiet)
    {
        if (!quiet)
        {
            Console.WriteLine($"Package \"{System.IO.Path.GetFileName(packagePath)}\" does not contain component packages");
            Console.WriteLine("(Component packages are not applicable to .pkg format)");
        }
        return 0;
    }
}
