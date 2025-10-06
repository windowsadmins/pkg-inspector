using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace PkgInspector;

public partial class WelcomeControl : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    public event EventHandler<string>? PackageSelected;
    public event PropertyChangedEventHandler? PropertyChanged;

    private ObservableCollection<RecentPackageInfo> _recentPackages = new();

    public WelcomeControl()
    {
        InitializeComponent();
        DataContext = this;
        LoadRecentPackages();
    }

    public string AppVersion
    {
        get
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                // Format: YYYY.MM.DD.HHMM
                return $"{version.Major}.{version.Minor:D2}.{version.Build:D2}.{version.Revision:D4}";
            }
            return "1.0.0";
        }
    }

    public bool HasRecentPackages => _recentPackages.Count > 0;

    private void LoadRecentPackages()
    {
        _recentPackages.Clear();
        
        // Load from settings/registry (placeholder for now)
        var recentFiles = LoadRecentFilesFromSettings();
        
        foreach (var file in recentFiles)
        {
            if (File.Exists(file))
            {
                _recentPackages.Add(new RecentPackageInfo
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    FolderPath = Path.GetDirectoryName(file) ?? ""
                });
            }
        }

        RecentPackagesList.ItemsSource = _recentPackages;
        OnPropertyChanged(nameof(HasRecentPackages));
    }

    private List<string> LoadRecentFilesFromSettings()
    {
        // TODO: Load from application settings or registry
        // For now, return empty list
        var recent = new List<string>();
        
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PkgInspector",
                "recent.txt"
            );
            
            if (File.Exists(settingsPath))
            {
                recent = File.ReadAllLines(settingsPath).ToList();
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return recent;
    }

    public void AddRecentPackage(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        // Remove if already exists
        var existing = _recentPackages.FirstOrDefault(p => p.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _recentPackages.Remove(existing);
        }

        // Add to top
        _recentPackages.Insert(0, new RecentPackageInfo
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            FolderPath = Path.GetDirectoryName(filePath) ?? ""
        });

        // Keep only last 20
        while (_recentPackages.Count > 20)
        {
            _recentPackages.RemoveAt(_recentPackages.Count - 1);
        }

        SaveRecentPackages();
    }

    private void SaveRecentPackages()
    {
        try
        {
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PkgInspector"
            );
            
            Directory.CreateDirectory(settingsDir);
            
            var settingsPath = Path.Combine(settingsDir, "recent.txt");
            File.WriteAllLines(settingsPath, _recentPackages.Select(p => p.FilePath));
        }
        catch
        {
            // Ignore errors
        }
    }

    private void ChoosePackage_Click(object sender, RoutedEventArgs e)
    {
        OpenPackage_Click(sender, e);
    }

    private void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Package Files (*.pkg;*.nupkg)|*.pkg;*.nupkg|All Files (*.*)|*.*",
            Title = "Select a Package to Inspect"
        };

        if (dialog.ShowDialog() == true)
        {
            PackageSelected?.Invoke(this, dialog.FileName);
        }
    }

    private void RecentPackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string filePath)
        {
            if (File.Exists(filePath))
            {
                PackageSelected?.Invoke(this, filePath);
            }
            else
            {
                MessageBox.Show(
                    $"Package file not found:\n{filePath}",
                    "File Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                
                // Remove from recent list
                var item = _recentPackages.FirstOrDefault(p => p.FilePath == filePath);
                if (item != null)
                {
                    _recentPackages.Remove(item);
                    SaveRecentPackages();
                }
            }
        }
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all recent packages?",
            "Clear Recent Packages",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _recentPackages.Clear();
            SaveRecentPackages();
        }
    }

    private void UserControl_Drop(object sender, System.Windows.DragEventArgs e)
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
                    PackageSelected?.Invoke(this, file);
                }
            }
        }
    }

    private void UserControl_DragOver(object sender, System.Windows.DragEventArgs e)
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

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RecentPackageInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
}
