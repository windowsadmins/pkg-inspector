using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Win32;
using PkgInspector.Models;
using PkgInspector.Services;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;
using TreeView = System.Windows.Controls.TreeView;

namespace PkgInspector;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Windows API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    
    private readonly PackageInspectorService _inspectorService;
    private PackageData? _currentPackage;
    private ScriptInfo? _selectedScript;
    private System.Windows.Threading.DispatcherTimer? _themeMonitor;
    private bool _lastThemeState;

    public MainWindow()
    {
        InitializeComponent();
        _inspectorService = new PackageInspectorService();
        DataContext = this;
        
        // Set window icon from PNG resource (better alpha channel support than ICO)
        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/app-icon.png");
            var iconBitmap = new System.Windows.Media.Imaging.BitmapImage(iconUri);
            Icon = iconBitmap;
        }
        catch { /* Ignore if icon can't be loaded */ }
        
        // Auto-detect and apply system theme
        DetectAndApplySystemTheme();
        
        // Start monitoring for theme changes
        StartThemeMonitoring();
        
        // Check for command-line arguments
        Loaded += async (s, e) =>
        {
            // Set dark title bar after window handle is created
            SetDarkTitleBar(_lastThemeState);
            await HandleCommandLineArgs();
        };
    }

    private void StartThemeMonitoring()
    {
        _themeMonitor = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _themeMonitor.Tick += (s, e) =>
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var usesDarkMode = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
                if (usesDarkMode != _lastThemeState)
                {
                    _lastThemeState = usesDarkMode;
                    ApplyTheme(usesDarkMode);
                }
            }
            catch { /* Ignore errors */ }
        };
        _themeMonitor.Start();
    }

    private void DetectAndApplySystemTheme()
    {
        try
        {
            // Check Windows registry for dark mode setting
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var usesDarkMode = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            _lastThemeState = usesDarkMode;
            ApplyTheme(usesDarkMode);
        }
        catch
        {
            // Default to light mode if detection fails
            _lastThemeState = false;
            ApplyTheme(false);
        }
    }

    private async Task HandleCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        
        // Skip the executable name
        if (args.Length > 1)
        {
            var packagePath = args[1];
            if (File.Exists(packagePath))
            {
                await LoadPackage(packagePath);
                
                // Check for reveal options from environment
                var revealFile = Environment.GetEnvironmentVariable("PKGINSPECTOR_REVEAL_FILE");
                var revealScripts = Environment.GetEnvironmentVariable("PKGINSPECTOR_REVEAL_SCRIPTS");
                
                if (!string.IsNullOrEmpty(revealScripts))
                {
                    MainTabControl.SelectedIndex = 2; // Scripts tab
                }
                else if (!string.IsNullOrEmpty(revealFile))
                {
                    MainTabControl.SelectedIndex = 1; // Files tab
                    // TODO: Navigate to specific file
                }
            }
        }
    }

    #region Properties

    public bool HasPackage => _currentPackage != null;

    public string PackageName => _currentPackage?.Metadata?.Name ?? "Unknown Package";
    
    public string PackageFileName => _currentPackage?.FileName ?? string.Empty;

    public BuildInfo? PackageInfo => _currentPackage?.Metadata;

    public int FileCount => _currentPackage?.Files.Count ?? 0;

    public int ScriptCount => _currentPackage?.Scripts.Count ?? 0;

    public string FileCountMessage => $"{FileCount} files in payload";

    public bool HasDependencies => _currentPackage?.Metadata?.Dependencies?.Count > 0;

    public List<FileTreeNode> FileTree => _currentPackage?.FileTree ?? new();

    public List<ScriptInfo> Scripts => _currentPackage?.Scripts ?? new();

    public ScriptInfo? SelectedScript
    {
        get => _selectedScript;
        set
        {
            _selectedScript = value;
            OnPropertyChanged(nameof(SelectedScript));
        }
    }

    public string RawMetadata => _currentPackage?.RawMetadata ?? string.Empty;

    public bool IsSigned => _currentPackage?.IsSigned ?? false;

    public string SignedBy => _currentPackage?.SignedBy ?? string.Empty;

    public string SignatureStatus => IsSigned ? "Signed" : "Unsigned";

    public string SignatureDetails => IsSigned ? $"Signed by: {SignedBy}" : "Package is not digitally signed";

    #endregion

    #region Event Handlers

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        // Clear the current package to return to welcome screen
        _currentPackage = null;
        _selectedScript = null;
        
        // Trigger property change notifications to update UI
        OnPropertyChanged(nameof(HasPackage));
        OnPropertyChanged(nameof(PackageName));
        OnPropertyChanged(nameof(PackageFileName));
        OnPropertyChanged(nameof(PackageInfo));
        OnPropertyChanged(nameof(FileTree));
        OnPropertyChanged(nameof(Scripts));
        OnPropertyChanged(nameof(SelectedScript));
        OnPropertyChanged(nameof(RawMetadata));
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(HasDependencies));
        OnPropertyChanged(nameof(IsSigned));
        OnPropertyChanged(nameof(SignatureStatus));
        OnPropertyChanged(nameof(SignedBy));
    }

    private void ApplyTheme(bool isDark)
    {
        var resources = System.Windows.Application.Current.Resources;
        
        if (isDark)
        {
            // Apply dark mode colors
            resources["BackgroundBrush"] = resources["DarkBackgroundBrush"];
            resources["SurfaceBrush"] = resources["DarkSurfaceBrush"];
            resources["BorderBrush"] = resources["DarkBorderBrush"];
            resources["TextPrimaryBrush"] = resources["DarkTextPrimaryBrush"];
            resources["TextSecondaryBrush"] = resources["DarkTextSecondaryBrush"];
            resources["HoverBrush"] = resources["DarkHoverBrush"];
            resources["SelectedBrush"] = resources["DarkSelectedBrush"];
        }
        else
        {
            // Restore light mode colors
            resources["BackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFA, 0xFA, 0xFA));
            resources["SurfaceBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            resources["BorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
            resources["TextPrimaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x21, 0x21, 0x21));
            resources["TextSecondaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x75, 0x75, 0x75));
            resources["HoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xF5, 0xF5));
            resources["SelectedBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE3, 0xF2, 0xFD));
        }
        
        // Apply dark title bar (Windows 10 build 18985+)
        SetDarkTitleBar(isDark);
    }

    private void SetDarkTitleBar(bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int useImmersiveDarkMode = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
            }
        }
        catch
        {
            // Silently fail on older Windows versions
        }
    }

    private async void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Package Files (*.pkg;*.nupkg)|*.pkg;*.nupkg|All Files (*.*)|*.*",
            Title = "Select a Package to Inspect"
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadPackage(dialog.FileName);
        }
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files.Length > 0)
            {
                var file = files[0];
                if (file.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                {
                    _ = LoadPackage(file);
                }
            }
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void FilesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Future: Show file preview or details
    }

    private void ScriptsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScriptsListBox.SelectedItem is ScriptInfo script)
        {
            SelectedScript = script;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            // Silently fail if can't open URL
        }
    }

    #endregion

    #region Methods

    private async Task LoadPackage(string filePath)
    {
        try
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            _currentPackage = await _inspectorService.InspectPackageAsync(filePath);
            
            // Select first script if available
            if (_currentPackage.Scripts.Count > 0)
            {
                SelectedScript = _currentPackage.Scripts[0];
            }

            // Add to recent packages
            WelcomeScreen?.AddRecentPackage(filePath);

            RefreshUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to load package: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void WelcomeScreen_PackageSelected(object? sender, string filePath)
    {
        await LoadPackage(filePath);
    }

    private void RefreshUI()
    {
        OnPropertyChanged(nameof(HasPackage));
        OnPropertyChanged(nameof(PackageName));
        OnPropertyChanged(nameof(PackageFileName));
        OnPropertyChanged(nameof(PackageInfo));
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(ScriptCount));
        OnPropertyChanged(nameof(FileCountMessage));
        OnPropertyChanged(nameof(HasDependencies));
        OnPropertyChanged(nameof(FileTree));
        OnPropertyChanged(nameof(Scripts));
        OnPropertyChanged(nameof(RawMetadata));
        OnPropertyChanged(nameof(IsSigned));
        OnPropertyChanged(nameof(SignedBy));
        OnPropertyChanged(nameof(SignatureStatus));
        OnPropertyChanged(nameof(SignatureDetails));

        // Expand all folders in the file tree after UI updates
        // Force tree expansion with multiple attempts
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            await Task.Delay(100); // Initial delay for rendering
            ExpandAllTreeViewItems();
            await Task.Delay(100); // Second attempt
            ExpandAllTreeViewItems();
            await Task.Delay(200); // Third attempt
            ExpandAllTreeViewItems();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ExpandAllTreeViewItems()
    {
        if (FilesTreeView != null && FilesTreeView.Items.Count > 0)
        {
            // Force container generation and expansion
            FilesTreeView.UpdateLayout();
            FilesTreeView.InvalidateVisual();
            
            foreach (var item in FilesTreeView.Items)
            {
                var treeViewItem = FilesTreeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeViewItem != null)
                {
                    treeViewItem.IsExpanded = true;
                    treeViewItem.UpdateLayout();
                    treeViewItem.ApplyTemplate();
                    ExpandTreeViewItem(treeViewItem);
                }
            }
        }
    }

    private void ExpandTreeViewItem(TreeViewItem item)
    {
        item.IsExpanded = true;
        item.UpdateLayout();
        
        // Give time for containers to generate
        item.ApplyTemplate();

        foreach (var childItem in item.Items)
        {
            var childTreeViewItem = item.ItemContainerGenerator.ContainerFromItem(childItem) as TreeViewItem;
            if (childTreeViewItem != null)
            {
                ExpandTreeViewItem(childTreeViewItem);
            }
        }
    }

    private async void ExportPackage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPackage == null) return;

        // Suppress CA1416 - this is a Windows-only app (net9.0-windows)
        #pragma warning disable CA1416
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to export package contents",
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                // Create subfolder with package name and version
                var packageName = _currentPackage?.Metadata?.Name ?? "Package";
                var packageVersion = _currentPackage?.Metadata?.Version ?? "Unknown";
                
                // Sanitize folder name
                var invalidChars = Path.GetInvalidFileNameChars();
                var safeName = string.Concat(packageName.Split(invalidChars));
                var safeVersion = string.Concat(packageVersion.Split(invalidChars));
                
                var exportFolderName = $"{safeName}-{safeVersion}";
                var exportPath = Path.Combine(dialog.SelectedPath, exportFolderName);
                
                await ExportPackageToFolder(exportPath);
                
                // Open the exported folder immediately
                Process.Start(new ProcessStartInfo
                {
                    FileName = exportPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export package: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        #pragma warning restore CA1416
    }

    private void FilesTreeView_RightClick(object sender, MouseButtonEventArgs e)
    {
        var treeView = sender as TreeView;
        if (treeView == null) return;

        var item = GetTreeViewItemAtPoint(treeView, e.GetPosition(treeView));
        if (item != null && item.DataContext is FileTreeNode node)
        {
            item.IsSelected = true;
            PopulateOpenWithMenu(FileContextMenu.Items[1] as MenuItem ?? new MenuItem(), node.FullPath, false);
        }
    }

    private void ScriptsListBox_RightClick(object sender, MouseButtonEventArgs e)
    {
        PopulateOpenWithMenu(ScriptContextMenu.Items[1] as MenuItem ?? new MenuItem(), "", true);
    }

    private TreeViewItem? GetTreeViewItemAtPoint(System.Windows.Controls.TreeView treeView, System.Windows.Point point)
    {
        var element = treeView.InputHitTest(point) as DependencyObject;
        while (element != null)
        {
            if (element is TreeViewItem item)
                return item;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void PopulateOpenWithMenu(MenuItem parentMenu, string relativePath, bool isPowerShell)
    {
        parentMenu.Items.Clear();

        var apps = new List<(string Name, string Path)>
        {
            ("Notepad", "notepad.exe"),
            ("VS Code", @"C:\Program Files\Microsoft VS Code\Code.exe"),
            ("PowerShell", "powershell.exe"),
            ("PowerShell ISE", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell_ise.exe")
        };

        if (isPowerShell)
        {
            apps.Add(("Windows Terminal", "wt.exe"));
        }

        foreach (var (name, path) in apps)
        {
            // Check if the app exists
            if (File.Exists(path) || CheckProgramInPath(path))
            {
                var menuItem = new MenuItem { Header = name, Tag = path };
                menuItem.Click += (s, e) => OpenItemWith(relativePath, path, isPowerShell);
                parentMenu.Items.Add(menuItem);
            }
        }

        if (parentMenu.Items.Count == 0)
        {
            parentMenu.Items.Add(new MenuItem { Header = "No applications found", IsEnabled = false });
        }
    }

    private bool CheckProgramInPath(string program)
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                var paths = pathEnv.Split(';');
                foreach (var path in paths)
                {
                    var fullPath = Path.Combine(path, program);
                    if (File.Exists(fullPath))
                        return true;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return false;
    }

    private async void OpenItemWith(string relativePath, string appPath, bool isScript)
    {
        if (_currentPackage == null) return;

        try
        {
            string tempFile;
            
            if (isScript && SelectedScript != null)
            {
                // Export script to temp file
                tempFile = Path.Combine(Path.GetTempPath(), $"pkginspector_{Guid.NewGuid()}", SelectedScript.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);
                await File.WriteAllTextAsync(tempFile, SelectedScript.Content);
            }
            else
            {
                // Export file from package
                tempFile = await ExportSingleFile(relativePath);
            }

            // Open with specified application
            Process.Start(new ProcessStartInfo
            {
                FileName = appPath,
                Arguments = $"\"{tempFile}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open item: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void ExportFileItem_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedFileNode();
        if (node == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = node.Name,
            Title = "Export File"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var tempFile = await ExportSingleFile(node.FullPath);
                File.Copy(tempFile, dialog.FileName, true);
                
                MessageBox.Show(
                    $"File exported successfully to:\n{dialog.FileName}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export file: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private async void ExportScriptItem_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedScript == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = SelectedScript.Name,
            DefaultExt = ".ps1",
            Filter = "PowerShell Scripts (*.ps1)|*.ps1|All Files (*.*)|*.*",
            Title = "Export Script"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await File.WriteAllTextAsync(dialog.FileName, SelectedScript.Content);
                
                MessageBox.Show(
                    $"Script exported successfully to:\n{dialog.FileName}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export script: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void CopyFilePath_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedFileNode();
        if (node != null)
        {
            Clipboard.SetText(node.FullPath);
        }
    }

    private FileTreeNode? GetSelectedFileNode()
    {
        // Find the TreeView control
        var treeView = FindVisualChild<TreeView>(this);
        if (treeView != null)
        {
            foreach (var item in GetAllTreeViewItems(treeView))
            {
                if (item.IsSelected && item.DataContext is FileTreeNode node)
                {
                    return node;
                }
            }
        }
        return null;
    }

    private IEnumerable<TreeViewItem> GetAllTreeViewItems(ItemsControl parent)
    {
        for (int i = 0; i < parent.Items.Count; i++)
        {
            var item = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
            if (item != null)
            {
                yield return item;
                foreach (var child in GetAllTreeViewItems(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    private void CopyScriptPath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedScript != null)
        {
            Clipboard.SetText(SelectedScript.RelativePath);
        }
    }

    private Task<string> ExportSingleFile(string relativePath)
    {
        if (_currentPackage == null)
            throw new InvalidOperationException("No package loaded");

        // Extract package to temp location
        var tempDir = Path.Combine(Path.GetTempPath(), $"pkginspector_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        using (var archive = ZipFile.OpenRead(_currentPackage.FilePath))
        {
            var entry = archive.Entries.FirstOrDefault(e => 
                e.FullName.Replace('/', '\\').EndsWith(relativePath.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                var destPath = Path.Combine(tempDir, entry.Name);
                using (var entryStream = entry.Open())
                using (var fileStream = File.Create(destPath))
                {
                    entryStream.CopyTo(fileStream);
                }
                return Task.FromResult(destPath);
            }
        }

        throw new FileNotFoundException($"File not found in package: {relativePath}");
    }

    private async Task ExportPackageToFolder(string folderPath)
    {
        if (_currentPackage == null) return;

        // Create export folder structure
        Directory.CreateDirectory(folderPath);
        var payloadPath = Path.Combine(folderPath, "payload");
        var scriptsPath = Path.Combine(folderPath, "scripts");

        // Export the report
        var reportPath = Path.Combine(folderPath, "package-report.txt");
        await ExportPackageInfo(reportPath, true);

        // Export build-info.yaml
        if (!string.IsNullOrEmpty(RawMetadata) && RawMetadata != "No metadata file found")
        {
            await File.WriteAllTextAsync(Path.Combine(folderPath, "build-info.yaml"), RawMetadata);
        }

        // Export payload files
        if (_currentPackage.Files.Count > 0)
        {
            Directory.CreateDirectory(payloadPath);
            
            using (var archive = ZipFile.OpenRead(_currentPackage.FilePath))
            {
                foreach (var file in _currentPackage.Files.Where(f => !f.IsDirectory))
                {
                    var entryPath = "payload/" + file.RelativePath.Replace("\\", "/");
                    var entry = archive.GetEntry(entryPath);
                    if (entry != null)
                    {
                        var outputPath = Path.Combine(payloadPath, file.RelativePath);
                        var outputDir = Path.GetDirectoryName(outputPath);
                        if (!string.IsNullOrEmpty(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }
                        entry.ExtractToFile(outputPath, true);
                    }
                }
            }
        }

        // Export scripts
        if (_currentPackage.Scripts.Count > 0)
        {
            Directory.CreateDirectory(scriptsPath);
            foreach (var script in _currentPackage.Scripts)
            {
                var scriptPath = Path.Combine(scriptsPath, script.Name);
                await File.WriteAllTextAsync(scriptPath, script.Content);
            }
        }
    }

    private async Task ExportPackageInfo(string filePath, bool includePayload = false)
    {
        if (_currentPackage == null) return;

        var sb = new System.Text.StringBuilder();
        var isMarkdown = filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

        if (isMarkdown)
        {
            sb.AppendLine($"# Package Inspection Report: {PackageName}");
            sb.AppendLine();
            sb.AppendLine($"**Package File:** `{PackageFileName}`");
            sb.AppendLine($"**Inspection Date:** {DateTime.Now:yyyy-MM-DD HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Package Overview");
            sb.AppendLine();
            sb.AppendLine($"| Property | Value |");
            sb.AppendLine($"|----------|-------|");
            sb.AppendLine($"| Name | {PackageInfo?.Name ?? "N/A"} |");
            sb.AppendLine($"| Version | {PackageInfo?.Version ?? "N/A"} |");
            sb.AppendLine($"| Description | {PackageInfo?.Description ?? "N/A"} |");
            sb.AppendLine($"| Author | {PackageInfo?.Author ?? "N/A"} |");
            sb.AppendLine($"| License | {PackageInfo?.License ?? "N/A"} |");
            sb.AppendLine($"| Homepage | {PackageInfo?.Homepage ?? "N/A"} |");
            sb.AppendLine($"| Target | {PackageInfo?.Target ?? "N/A"} |");
            sb.AppendLine($"| Signature | {SignatureStatus} {(IsSigned ? $"({SignedBy})" : "")} |");
            sb.AppendLine();
            sb.AppendLine("## Installation Details");
            sb.AppendLine();
            sb.AppendLine($"- **Install Location:** `{PackageInfo?.InstallLocation ?? "N/A"}`");
            sb.AppendLine($"- **Restart Action:** {PackageInfo?.RestartAction ?? "N/A"}");
            sb.AppendLine($"- **File Count:** {FileCount}");
            sb.AppendLine($"- **Script Count:** {ScriptCount}");
            sb.AppendLine();

            if (HasDependencies && PackageInfo?.Dependencies != null && PackageInfo.Dependencies.Count > 0)
            {
                sb.AppendLine("## Dependencies");
                sb.AppendLine();
                foreach (var dep in PackageInfo.Dependencies)
                {
                    sb.AppendLine($"- {dep}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Files");
            sb.AppendLine();
            foreach (var file in _currentPackage.Files.OrderBy(f => f.RelativePath))
            {
                var icon = file.IsDirectory ? "📁" : "📄";
                sb.AppendLine($"- {icon} `{file.RelativePath}` {(file.IsDirectory ? "" : $"({file.SizeFormatted})")}");
            }
            sb.AppendLine();

            if (_currentPackage.Scripts.Count > 0)
            {
                sb.AppendLine("## Scripts");
                sb.AppendLine();
                foreach (var script in _currentPackage.Scripts)
                {
                    sb.AppendLine($"### {script.Name} ({script.Type})");
                    sb.AppendLine();
                    sb.AppendLine("```powershell");
                    sb.AppendLine(script.Content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("## Raw Metadata");
            sb.AppendLine();
            sb.AppendLine("```yaml");
            sb.AppendLine(RawMetadata);
            sb.AppendLine("```");
        }
        else
        {
            // Plain text format
            sb.AppendLine($"Package Inspection Report: {PackageName}");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();
            sb.AppendLine($"Package File: {PackageFileName}");
            sb.AppendLine($"Inspection Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("PACKAGE OVERVIEW");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"Name: {PackageInfo?.Name ?? "N/A"}");
            sb.AppendLine($"Version: {PackageInfo?.Version ?? "N/A"}");
            sb.AppendLine($"Description: {PackageInfo?.Description ?? "N/A"}");
            sb.AppendLine($"Author: {PackageInfo?.Author ?? "N/A"}");
            sb.AppendLine($"License: {PackageInfo?.License ?? "N/A"}");
            sb.AppendLine($"Homepage: {PackageInfo?.Homepage ?? "N/A"}");
            sb.AppendLine($"Target: {PackageInfo?.Target ?? "N/A"}");
            sb.AppendLine($"Signature: {SignatureStatus} {(IsSigned ? $"({SignedBy})" : "")}");
            sb.AppendLine();
            sb.AppendLine("INSTALLATION DETAILS");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"Install Location: {PackageInfo?.InstallLocation ?? "N/A"}");
            sb.AppendLine($"Restart Action: {PackageInfo?.RestartAction ?? "N/A"}");
            sb.AppendLine($"File Count: {FileCount}");
            sb.AppendLine($"Script Count: {ScriptCount}");
            sb.AppendLine();

            if (HasDependencies && PackageInfo?.Dependencies != null && PackageInfo.Dependencies.Count > 0)
            {
                sb.AppendLine("DEPENDENCIES");
                sb.AppendLine(new string('-', 80));
                foreach (var dep in PackageInfo.Dependencies)
                {
                    sb.AppendLine($"  - {dep}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("FILES");
            sb.AppendLine(new string('-', 80));
            foreach (var file in _currentPackage.Files.OrderBy(f => f.RelativePath))
            {
                var type = file.IsDirectory ? "[DIR]" : "[FILE]";
                sb.AppendLine($"{type} {file.RelativePath} {(file.IsDirectory ? "" : $"({file.SizeFormatted})")}");
            }
            sb.AppendLine();

            if (_currentPackage.Scripts.Count > 0)
            {
                sb.AppendLine("SCRIPTS");
                sb.AppendLine(new string('-', 80));
                foreach (var script in _currentPackage.Scripts)
                {
                    sb.AppendLine();
                    sb.AppendLine($"=== {script.Name} ({script.Type}) ===");
                    sb.AppendLine();
                    sb.AppendLine(script.Content);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("RAW METADATA");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine(RawMetadata);
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
